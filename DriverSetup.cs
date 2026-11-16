using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Windows.Forms;
using Microsoft.Win32;

namespace TempWidget
{
    internal enum DriverResult { Installed, RestartNeeded, FileMissing, FileChanged, Failed }

    /// <summary>
    /// Installs the PawnIO driver from the official, signed installer that ships next to
    /// TempWidget.exe, so users never have to visit a website. Needs administrator rights.
    /// </summary>
    internal static class DriverSetup
    {
        private const string FileName = "PawnIO_setup.exe";
        public const string BundledVersion = "2.2.0";
        // SHA-256 of the official 2.2.0 release. We run it elevated, so refuse anything else.
        private const string Sha256 = "1f519a22e47187f70a1379a48ca604981c4fcf694f4e65b734aaa74a9fba3032";

        public static string BundledPath =>
            Path.Combine(Path.GetDirectoryName(Application.ExecutablePath) ?? "", FileName);

        /// <summary>Reads the registry directly (the sensor library caches its own answer at first use).</summary>
        public static bool IsInstalled()
        {
            try
            {
                using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var key = hklm.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO"))
                    return key?.GetValue("DisplayVersion") is string;
            }
            catch { return false; }
        }

        /// <param name="code">The installer's exit code (a Windows error number) when it fails.</param>
        public static DriverResult Install(out int code)
        {
            code = 0;
            string path = BundledPath;
            try
            {
                if (!File.Exists(path)) return DriverResult.FileMissing;
                using (var sha = SHA256.Create())
                using (var stream = File.OpenRead(path))
                {
                    string hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                    if (hash != Sha256) return DriverResult.FileChanged;
                }

                var psi = new ProcessStartInfo(path, "-install -silent") { UseShellExecute = false, CreateNoWindow = true };
                using (var p = Process.Start(psi))
                {
                    if (!p.WaitForExit(120000)) { code = 1460; return DriverResult.Failed; } // ERROR_TIMEOUT
                    code = p.ExitCode;
                }
                if (code == 3010) return DriverResult.RestartNeeded; // ERROR_SUCCESS_REBOOT_REQUIRED
                if (code == 0 && IsInstalled()) return DriverResult.Installed;
                return DriverResult.Failed;
            }
            catch (Exception ex)
            {
                Program.Log(ex);
                code = -1;
                return DriverResult.Failed;
            }
        }
    }
}
