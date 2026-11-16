using System;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32;

namespace TempWidget
{
    /// <summary>
    /// Start with Windows.
    ///  - Running as admin: a Task Scheduler task with "highest privileges", so full sensors
    ///    are available at logon without a UAC prompt.
    ///  - Running without admin: a per-user Run entry (limited sensors at logon).
    /// </summary>
    internal static class AutoStart
    {
        private const string TaskName = "TempWidget";
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public static bool IsEnabled() => TaskExists() || RunValue() != null;

        /// <summary>Returns null on success, otherwise a message for the user.</summary>
        public static string Set(bool enable)
        {
            if (!enable)
            {
                try { using (var k = Registry.CurrentUser.OpenSubKey(RunKey, true)) k?.DeleteValue(TaskName, false); } catch { }
                if (TaskExists() && Schtasks($"/Delete /TN \"{TaskName}\" /F") != 0)
                    return "The startup task was created with administrator rights. Enable full sensors first, then turn this off again.";
                return null;
            }

            if (Program.IsElevated)
            {
                try { using (var k = Registry.CurrentUser.OpenSubKey(RunKey, true)) k?.DeleteValue(TaskName, false); } catch { }
                return CreateTask() ? null : "Could not create the startup task.";
            }

            if (Program.IsPackaged)
            {
                // Store installs manage unelevated startup through Windows itself.
                try { Process.Start(new ProcessStartInfo("ms-settings:startupapps") { UseShellExecute = true }); } catch { }
                return "Turn on TempWidget in the Windows Startup apps page that just opened.\n\n" +
                       "For full sensors at startup, enable full sensors first and then tick this option again.";
            }

            try
            {
                using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
                    k.SetValue(TaskName, "\"" + Program.LaunchPath() + "\"");
                return null;
            }
            catch { return "Could not write the startup entry."; }
        }

        /// <summary>Re-points an existing startup entry at the current program location.</summary>
        public static void Repair()
        {
            try
            {
                string path = Program.LaunchPath();
                string run = RunValue();
                if (run != null && run.IndexOf(path, StringComparison.OrdinalIgnoreCase) < 0)
                    using (var k = Registry.CurrentUser.CreateSubKey(RunKey)) k.SetValue(TaskName, "\"" + path + "\"");

                if (!Program.IsElevated) return;
                string xml = SchtasksOutput($"/Query /TN \"{TaskName}\" /XML");
                if (xml != null && xml.IndexOf(SecurityElement.Escape(path), StringComparison.OrdinalIgnoreCase) < 0)
                    CreateTask();
            }
            catch { }
        }

        private static string RunValue()
        {
            try { using (var k = Registry.CurrentUser.OpenSubKey(RunKey)) return k?.GetValue(TaskName) as string; }
            catch { return null; }
        }

        private static bool TaskExists() => Schtasks($"/Query /TN \"{TaskName}\"") == 0;

        private static bool CreateTask()
        {
            string user = SecurityElement.Escape(WindowsIdentity.GetCurrent().Name);
            string exe = SecurityElement.Escape(Program.LaunchPath());
            string xml = $@"<?xml version=""1.0"" encoding=""UTF-16""?>
<Task version=""1.2"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
  <Triggers>
    <LogonTrigger><Enabled>true</Enabled><UserId>{user}</UserId><Delay>PT10S</Delay></LogonTrigger>
  </Triggers>
  <Principals>
    <Principal id=""Author""><UserId>{user}</UserId><LogonType>InteractiveToken</LogonType><RunLevel>HighestAvailable</RunLevel></Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
    <Priority>7</Priority>
  </Settings>
  <Actions Context=""Author""><Exec><Command>{exe}</Command><Arguments>--restarted</Arguments></Exec></Actions>
</Task>";
            string tmp = Path.Combine(Path.GetTempPath(), "TempWidget-task.xml");
            try
            {
                File.WriteAllText(tmp, xml, Encoding.Unicode);
                return Schtasks($"/Create /TN \"{TaskName}\" /XML \"{tmp}\" /F") == 0;
            }
            catch { return false; }
            finally { try { File.Delete(tmp); } catch { } }
        }

        /// <summary>Standard output of schtasks, or null if it failed (e.g. the task does not exist).</summary>
        private static string SchtasksOutput(string args)
        {
            try
            {
                var psi = new ProcessStartInfo("schtasks.exe", args)
                {
                    CreateNoWindow = true, UseShellExecute = false,
                    RedirectStandardOutput = true, RedirectStandardError = true,
                };
                using (var p = Process.Start(psi))
                {
                    string output = p.StandardOutput.ReadToEnd();
                    p.StandardError.ReadToEnd();
                    p.WaitForExit();
                    return p.ExitCode == 0 ? output : null;
                }
            }
            catch { return null; }
        }

        private static int Schtasks(string args)
        {
            try
            {
                var psi = new ProcessStartInfo("schtasks.exe", args)
                {
                    CreateNoWindow = true, UseShellExecute = false,
                    RedirectStandardOutput = true, RedirectStandardError = true,
                };
                using (var p = Process.Start(psi))
                {
                    p.StandardOutput.ReadToEnd();
                    p.StandardError.ReadToEnd();
                    p.WaitForExit();
                    return p.ExitCode;
                }
            }
            catch { return -1; }
        }
    }
}
