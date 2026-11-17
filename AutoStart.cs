using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace TempWidget
{
    internal enum LegacyStartupTask { Absent, Present, Unknown }

    internal interface IStartupStore
    {
        LegacyStartupTask ReadLegacyTask();
        string ReadRunValue();
        void WriteRunValue(string command);
        void DeleteRunValue();
    }

    /// <summary>No scheduled task creation, elevation or automatic repair of startup entries.</summary>
    internal static class AutoStart
    {
        internal const string LegacyWarning = "An older TempWidget scheduled task exists, or could not be checked. " +
            "Review and remove that task in Windows Task Scheduler before enabling startup here. " +
            "An elevated task pointing to a writable folder can run replaced program files with administrator rights. " +
            "TempWidget will not rewrite or remove that task automatically.";

        public static bool IsEnabled()
        {
            var store = new WindowsStartupStore();
            return store.ReadRunValue() != null || store.ReadLegacyTask() != LegacyStartupTask.Absent;
        }

        public static string StartupWarning() => new WindowsStartupStore().ReadLegacyTask() == LegacyStartupTask.Absent ? null : LegacyWarning;

        public static string Set(bool enable) => Configure(enable, Program.IsPackaged, Program.LaunchPath(), new WindowsStartupStore());

        internal static string Configure(bool enable, bool packaged, string executable, IStartupStore store)
        {
            try
            {
                var legacy = store.ReadLegacyTask();
                if (!enable)
                {
                    store.DeleteRunValue();
                    return legacy == LegacyStartupTask.Absent ? null : LegacyWarning;
                }
                if (legacy != LegacyStartupTask.Absent) return LegacyWarning;
                if (packaged) return "Manage TempWidget under Windows Settings > Apps > Startup. Startup uses basic sensors; enable full sensors manually for each session.";
                store.WriteRunValue(StartupCommand(executable));
                return null;
            }
            catch { return "Could not update the per-user startup entry. No elevated startup task was created."; }
        }

        internal static string StartupCommand(string executable)
        {
            if (string.IsNullOrWhiteSpace(executable) || executable.Length < 4 ||
                !char.IsLetter(executable[0]) || executable[1] != ':' || executable[2] != '\\' ||
                !executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                executable.IndexOfAny(new[] { '"', '\r', '\n', '\0' }) >= 0)
                throw new ArgumentException("Startup requires a local absolute executable path.");
            string command = "\"" + executable + "\" --startup";
            if (command.Length > 260) throw new ArgumentException("Startup command exceeds the Windows Run key limit.");
            return command;
        }
    }

    internal sealed class WindowsStartupStore : IStartupStore
    {
        private const string Name = "TempWidget";
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public string ReadRunValue()
        {
            try { using (var key = Registry.CurrentUser.OpenSubKey(RunKey)) return key?.GetValue(Name) as string; }
            catch { return null; }
        }
        public void WriteRunValue(string command)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(RunKey)) key.SetValue(Name, command, RegistryValueKind.String);
        }
        public void DeleteRunValue()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunKey, true)) key?.DeleteValue(Name, false);
        }

        public LegacyStartupTask ReadLegacyTask()
        {
            // Read-only COM query: no scheduler subprocess, temporary task XML, or task writes.
            object service = null, folder = null, task = null;
            try
            {
                var type = Type.GetTypeFromProgID("Schedule.Service");
                if (type == null) return LegacyStartupTask.Unknown;
                service = Activator.CreateInstance(type);
                ((dynamic)service).Connect();
                folder = ((dynamic)service).GetFolder("\\");
                task = ((dynamic)folder).GetTask(Name);
                return LegacyStartupTask.Present;
            }
            catch (COMException ex) when (ex.ErrorCode == unchecked((int)0x80070002)) { return LegacyStartupTask.Absent; }
            catch { return LegacyStartupTask.Unknown; }
            finally
            {
                foreach (object item in new[] { task, folder, service })
                    if (item != null && Marshal.IsComObject(item))
                        try { Marshal.FinalReleaseComObject(item); } catch { }
            }
        }
    }
}
