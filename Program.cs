using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;

namespace TempWidget
{
    internal static class Program
    {
        public static bool IsElevated { get; private set; }
        public static bool IsPackaged { get; private set; }
        public static bool Demo { get; private set; }
        public static string Version { get; private set; } = "2.0.0";
        public static string[] Args { get; private set; } = new string[0];

        /// <summary>Set when this (elevated) run should install the driver and, optionally, the startup entry.</summary>
        public static bool SetupRequested { get; private set; }
        public static bool SetupAutoStart { get; private set; }

        [STAThread]
        private static int Main(string[] args)
        {
            Args = args;
            Demo = args.Contains("--demo");
            try { Version = Assembly.GetExecutingAssembly().GetName().Version.ToString(3); } catch { }
            try
            {
                using (var id = WindowsIdentity.GetCurrent())
                    IsElevated = new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch { }
            IsPackaged = Native.IsPackaged();

            Application.ThreadException += (s, e) => Log(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => Log(e.ExceptionObject as Exception);

            Mutex mutex = null;
            bool owned = false;
            try
            {
                if (!Demo)
                {
                    try { mutex = new Mutex(false, @"Local\TempWidget.SingleInstance"); }
                    catch (UnauthorizedAccessException) { return 0; } // an elevated copy is already running
                    try
                    {
                        // After an elevated restart the previous instance needs a moment to exit.
                        owned = mutex.WaitOne(args.Contains("--restarted") ? 10000 : 0);
                    }
                    catch (AbandonedMutexException) { owned = true; }
                    if (!owned) return 0; // already running
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                int welcomeShot = Array.IndexOf(args, "--shot-welcome");
                if (Demo && welcomeShot >= 0 && welcomeShot + 1 < args.Length) return ScreenshotWelcome(args[welcomeShot + 1]);

                var settings = Settings.Load();
                var launch = new LaunchOptions(args, IsPackaged);
                SetupRequested = launch.Setup;
                SetupAutoStart = launch.AutoStart;

                if (!Demo && launch.ShouldShowOnboarding(settings.Onboarded))
                {
                    bool full, autoStart;
                    using (var welcome = new WelcomeForm())
                    {
                        if (welcome.ShowDialog() != DialogResult.OK) return 0; // closed the window: not now
                        full = welcome.FullSensors;
                        autoStart = welcome.StartWithWindows;
                    }
                    settings.Onboarded = true;
                    settings.PreferElevated = full;
                    settings.Save();

                    if (full && IsElevated) { SetupRequested = true; SetupAutoStart = autoStart; }
                    else if (full)
                    {
                        if (owned) { mutex.ReleaseMutex(); owned = false; }
                        if (RelaunchElevated("--setup" + (autoStart ? " --autostart" : ""))) return 0;
                        // The administrator prompt was declined: carry on with basic sensors.
                        try { owned = mutex.WaitOne(0); } catch (AbandonedMutexException) { owned = true; }
                        if (!owned) return 0;
                        if (autoStart) ConfigureStartup();
                    }
                    else if (autoStart) ConfigureStartup();
                }
                else if (launch.ShouldAutoElevate(settings.PreferElevated, IsElevated, Demo))
                {
                    if (owned) { mutex.ReleaseMutex(); owned = false; }
                    if (RelaunchElevated(DriverSetup.IsInstalled() ? null : "--setup")) return 0;
                    // UAC declined: continue with limited sensors for this session without looping.
                    try { owned = mutex.WaitOne(0); } catch (AbandonedMutexException) { owned = true; }
                    if (!owned) return 0;
                }

                // Logon (--startup) and MSIX launches never cause unattended elevation.

                try { Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal; } catch { }
                Application.Run(new WidgetForm(settings));
                return 0;
            }
            catch (Exception ex)
            {
                Log(ex);
                return 1;
            }
            finally
            {
                if (owned) try { mutex.ReleaseMutex(); } catch { }
                mutex?.Dispose();
            }
        }

        /// <summary>Path that starts this app: the stable alias for Store installs, the exe otherwise.</summary>
        public static string LaunchPath()
        {
            if (IsPackaged)
            {
                string alias = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                            @"Microsoft\WindowsApps\TempWidget.exe");
                if (File.Exists(alias)) return alias;
            }
            return Application.ExecutablePath;
        }

        private static void ConfigureStartup()
        {
            string problem = AutoStart.Set(true);
            if (problem != null) MessageBox.Show(problem, "Startup settings", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>Starts an elevated copy (UAC prompt). False if the user declined or it failed.</summary>
        public static bool RelaunchElevated(string extraArgs)
        {
            try
            {
                string exe = LaunchPath();
                string arguments = "--restarted" + (string.IsNullOrEmpty(extraArgs) ? "" : " " + extraArgs);
                Process.Start(new ProcessStartInfo(exe)
                {
                    Verb = "runas",
                    UseShellExecute = true,
                    Arguments = arguments,
                    WorkingDirectory = Path.GetDirectoryName(exe) ?? "",
                });
                return true;
            }
            catch { return false; }
        }

        /// <summary>Starts a fresh copy with the same rights; the caller then exits.</summary>
        public static bool Restart()
        {
            try
            {
                string exe = LaunchPath();
                Process.Start(new ProcessStartInfo(exe)
                {
                    UseShellExecute = true,
                    Arguments = "--restarted",
                    WorkingDirectory = Path.GetDirectoryName(exe) ?? "",
                });
                return true;
            }
            catch { return false; }
        }

        private static int ScreenshotWelcome(string path)
        {
            using (var w = new WelcomeForm())
            {
                w.Show();
                for (int i = 0; i < 10; i++) { Application.DoEvents(); Thread.Sleep(100); }
                using (var bmp = new System.Drawing.Bitmap(w.Width, w.Height))
                {
                    try { using (var g = System.Drawing.Graphics.FromImage(bmp)) g.CopyFromScreen(w.Location, System.Drawing.Point.Empty, w.Size); }
                    catch { w.DrawToBitmap(bmp, new System.Drawing.Rectangle(System.Drawing.Point.Empty, w.Size)); }
                    bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                }
            }
            return 0;
        }

        public static void Log(Exception ex)
        {
            if (ex == null) return;
            try
            {
                Directory.CreateDirectory(Settings.Dir);
                string path = Path.Combine(Settings.Dir, "error.log");
                if (File.Exists(path) && new FileInfo(path).Length > 256 * 1024) File.Delete(path);
                File.AppendAllText(path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} v{Version}\r\n{ex}\r\n\r\n");
            }
            catch { }
        }
    }
}
