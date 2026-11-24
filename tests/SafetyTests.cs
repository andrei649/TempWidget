using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace TempWidget.Tests
{
    internal static class SafetyTests
    {
        private static readonly DateTime SampleTime = new DateTime(2026, 11, 16, 12, 34, 56);

        internal static void Register(Action<string, Action> run)
        {
            run("Launch from inside a zip is detected by missing companion files", () =>
            {
                string dir = Sandbox();
                Equal(2, TempWidget.Program.MissingInstallFiles(dir).Count);
                Equal(2, TempWidget.Program.MissingInstallFiles(null).Count);
                File.WriteAllText(Path.Combine(dir, "LibreHardwareMonitorLib.dll"), "");
                Equal("TempWidget.exe.config", TempWidget.Program.MissingInstallFiles(dir).Single());
                File.WriteAllText(Path.Combine(dir, "TempWidget.exe.config"), "");
                Equal(0, TempWidget.Program.MissingInstallFiles(dir).Count);
            });
            run("Startup only requests a quoted per-user basic-mode command", () =>
            {
                var store = new FakeStartupStore();
                Equal<string>(null, AutoStart.Configure(true, false, @"C:\Portable Apps\TempWidget.exe", store));
                Equal("\"C:\\Portable Apps\\TempWidget.exe\" --startup", store.Command);
                Equal(1, store.Writes); Equal(0, store.Deletes);
            });
            foreach (var status in new[] { LegacyStartupTask.Present, LegacyStartupTask.Unknown })
            {
                var task = status;
                run("Startup fails closed for legacy task state " + task, () =>
                {
                    var store = new FakeStartupStore { Legacy = task };
                    True(AutoStart.Configure(true, false, @"C:\TempWidget.exe", store) != null);
                    Equal(0, store.Writes); Equal(0, store.Deletes);
                });
            }
            run("Disabling startup only removes this app's per-user Run entry", () =>
            {
                var store = new FakeStartupStore { Legacy = LegacyStartupTask.Present };
                True(AutoStart.Configure(false, false, @"C:\TempWidget.exe", store) != null);
                Equal(0, store.Writes); Equal(1, store.Deletes);
                // The interface has no scheduler mutation operation at all.
                Equal(LegacyStartupTask.Present, store.Legacy);
            });
            run("Packaged startup is left to Windows Settings without a Run write", () =>
            {
                var store = new FakeStartupStore();
                True(AutoStart.Configure(true, true, @"C:\TempWidget.exe", store) != null);
                Equal(0, store.Writes); Equal(0, store.Deletes);
            });
            run("Startup probe failure cannot enable startup", () =>
            {
                var store = new FakeStartupStore { ThrowProbe = true };
                True(AutoStart.Configure(true, false, @"C:\TempWidget.exe", store) != null);
                Equal(0, store.Writes);
            });
            run("Startup COM probe classifies FileNotFoundException as Absent and other errors as Unknown", () =>
            {
                Equal(LegacyStartupTask.Absent, WindowsStartupStore.ClassifyTaskProbeError(new FileNotFoundException()));
                Equal(LegacyStartupTask.Absent, WindowsStartupStore.ClassifyTaskProbeError(new System.Runtime.InteropServices.COMException("missing", unchecked((int)0x80070002))));
                Equal(LegacyStartupTask.Absent, WindowsStartupStore.ClassifyTaskProbeError(new System.Reflection.TargetInvocationException(new FileNotFoundException())));
                Equal(LegacyStartupTask.Unknown, WindowsStartupStore.ClassifyTaskProbeError(new UnauthorizedAccessException()));
                Equal(LegacyStartupTask.Unknown, WindowsStartupStore.ClassifyTaskProbeError(new System.Runtime.InteropServices.COMException("denied", unchecked((int)0x80070005))));
            });
            run("Startup rejects quote/control/relative/network/overlong commands", () =>
            {
                foreach (string path in new[] { "TempWidget.exe", @"\\server\share\TempWidget.exe", "C:\\evil\" --setup.exe",
                    "C:\\evil\n.exe", "C:\\evil\r.exe", "C:\\evil\0.exe", "C:\\" + new string('a', 250) + ".exe" })
                {
                    var store = new FakeStartupStore();
                    True(AutoStart.Configure(true, false, path, store) != null);
                    Equal(0, store.Writes);
                }
            });
            run("Startup marker suppresses setup and chained autostart flags", () =>
            {
                foreach (var args in new[] { new[] { "--startup" }, new[] { "--setup", "--autostart", "--startup" } })
                {
                    var options = new LaunchOptions(args);
                    True(options.Startup); True(!options.Setup); True(!options.AutoStart);
                }
                var explicitSetup = new LaunchOptions(new[] { "--setup", "--autostart" });
                True(!explicitSetup.Startup && explicitSetup.Setup && explicitSetup.AutoStart);
                True(!new LaunchOptions(new[] { "--autostart" }).AutoStart);
            });
            run("PreferElevated auto-elevates only manual unpackaged launches", () =>
            {
                True(new LaunchOptions(new string[0], packaged: false).ShouldAutoElevate(preferElevated: true, isElevated: false, demo: false));
                True(!new LaunchOptions(new string[0], packaged: false).ShouldAutoElevate(preferElevated: false, isElevated: false, demo: false));
                True(!new LaunchOptions(new string[0], packaged: false).ShouldAutoElevate(preferElevated: true, isElevated: true, demo: false));
                True(!new LaunchOptions(new[] { "--demo" }, packaged: false).ShouldAutoElevate(preferElevated: true, isElevated: false, demo: true));
                True(!new LaunchOptions(new[] { "--startup" }, packaged: false).ShouldAutoElevate(preferElevated: true, isElevated: false, demo: false));
                True(!new LaunchOptions(new string[0], packaged: true).ShouldAutoElevate(preferElevated: true, isElevated: false, demo: false));
                True(!new LaunchOptions(new[] { "--restarted" }, packaged: false).ShouldAutoElevate(preferElevated: true, isElevated: false, demo: false));
                True(!new LaunchOptions(new[] { "--limited" }, packaged: false).ShouldAutoElevate(preferElevated: true, isElevated: false, demo: false));
            });
            run("CSV row order cannot reorder a schema's columns", () =>
            {
                var rows = Rows(); Format(rows, out string h1, out string l1); rows.Reverse();
                Format(rows, out string h2, out string l2); Equal(h1, h2); Equal(l1, l2);
            });
            run("CSV missing and nonfinite readings preserve columns with empty cells", () =>
            {
                var rows = Rows(); Format(rows, out string before, out _);
                foreach (float missing in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                {
                    rows[0].Value = missing; Format(rows, out string header, out string line);
                    Equal(before, header); True(line.EndsWith(",,1000", StringComparison.Ordinal));
                }
            });
            run("CSV distinguishes renamed, added, removed and unit-changed sensors", () =>
            {
                var rows = Rows(); Format(rows, out string initial, out _);
                var names = new HashSet<string> { CsvLog.SchemaFileName(SampleTime, initial) };
                rows[0].Label = "Another CPU"; Format(rows, out string renamed, out _);
                True(names.Add(CsvLog.SchemaFileName(SampleTime, renamed)));
                rows.Add(new Row { Key = "gpu0.temp", Kind = RowKind.Temp, Section = Section.Gpu, Label = "Temperature", Value = 65 });
                Format(rows, out string added, out _); True(names.Add(CsvLog.SchemaFileName(SampleTime, added)));
                rows.RemoveAt(0); Format(rows, out string removed, out _); True(names.Add(CsvLog.SchemaFileName(SampleTime, removed)));
                rows[0].Unit = "%"; Format(rows, out string unit, out _); True(names.Add(CsvLog.SchemaFileName(SampleTime, unit)));
            });
            run("CSV key identity distinguishes identical device labels", () =>
            {
                var rows = Rows(); rows[1] = new Row { Key = "cpu1.temp", Kind = RowKind.Temp, Section = Section.Cpu, Label = rows[0].Label, Value = 66 };
                Format(rows, out string header, out _); True(header.Contains("cpu.temp |")); True(header.Contains("cpu1.temp |"));
            });
            run("CSV hardware header changes split a reused display-index schema", () =>
            {
                var rows = Rows(); var device = new Row { Kind = RowKind.Header, Label = "CPU", Text = "Synthetic model A" };
                rows.Insert(0, device); Format(rows, out string first, out _);
                device.Text = "Synthetic model B"; Format(rows, out string second, out _);
                True(first != second);
                True(CsvLog.SchemaFileName(SampleTime, first) != CsvLog.SchemaFileName(SampleTime, second));
            });
            run("CSV sorting preserves each hardware group's context", () =>
            {
                var a = new Row { Kind = RowKind.Temp, Key = "cpu0.temp", Section = Section.Cpu, Value = 60, Label = "Temperature" };
                var b = new Row { Kind = RowKind.Temp, Key = "cpu1.temp", Section = Section.Cpu, Value = 70, Label = "Temperature" };
                var ha = new Row { Kind = RowKind.Header, Label = "CPU", Text = "Model A" };
                var hb = new Row { Kind = RowKind.Header, Label = "CPU", Text = "Model B" };
                Format(new List<Row> { ha, a, hb, b }, out string first, out string l1);
                Format(new List<Row> { hb, b, ha, a }, out string second, out string l2);
                Equal(first, second); Equal(l1, l2); True(first.Contains("Model A")); True(first.Contains("Model B"));
            });
            run("CSV rejects duplicate sensor keys before writing anything", () =>
            {
                var rows = Rows(); rows.Add(rows[0]);
                bool rejected = false;
                try { Format(rows, out _, out _); } catch (ArgumentException) { rejected = true; }
                True(rejected);
            });
            run("Production EC fan rows keep the CSV schema through a failed reading", () =>
            {
                var valid = new List<Row>(); var missing = new List<Row>();
                LhmSource.AddFans(valid, new LibreHardwareMonitor.Hardware.IHardware[0], new[] { 1000, 0 });
                LhmSource.AddFans(missing, new LibreHardwareMonitor.Hardware.IHardware[0], new[] { -1, 0 });
                Format(valid, out string h1, out _); Format(missing, out string h2, out string line);
                Equal(h1, h2); True(line.EndsWith(",,0", StringComparison.Ordinal));
            });
            run("CSV escapes metadata, neutralizes formulas and removes embedded line breaks", () =>
            {
                var rows = Rows(); rows[0].Key = "=unsafe"; rows[0].Label = "comma,quote\"\r\nline";
                Format(rows, out string header, out _);
                True(header.Contains("\"'=unsafe |")); True(header.Contains("quote\"\"  line"));
                True(!header.Contains("\r") && !header.Contains("\n"));
            });
            run("CSV uses invariant numbers under a comma-decimal culture", () =>
            {
                var previous = CultureInfo.CurrentCulture;
                try
                {
                    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ro-RO");
                    var rows = Rows(); rows[0].Value = 60.25f; Format(rows, out _, out string line);
                    True(line.Contains(",60.25,1000"));
                }
                finally { CultureInfo.CurrentCulture = previous; }
            });
            run("CSV starts a separate file for each schema and day, including restarts", () =>
            {
                string directory = Sandbox(); var now = SampleTime; var rows = Rows();
                var log = new CsvLog(directory, () => now); log.Write(rows); log.Write(rows);
                rows[0].Value = float.NaN; log.Write(rows);
                new CsvLog(directory, () => now).Write(rows);
                var initial = Directory.GetFiles(directory, "*.csv").Single();
                Equal(5, File.ReadAllLines(initial).Length); // one header, four data rows
                rows.Add(new Row { Key = "gpu0.temp", Section = Section.Gpu, Kind = RowKind.Temp, Value = 50, Label = "GPU" });
                log.Write(rows); Equal(2, Directory.GetFiles(directory, "*.csv").Length);
                now = now.AddDays(1); log.Write(rows); Equal(3, Directory.GetFiles(directory, "*.csv").Length);
                foreach (string file in Directory.GetFiles(directory, "*.csv"))
                    Equal(1, File.ReadAllLines(file).Count(line => line.StartsWith("Time,", StringComparison.Ordinal)));
            });
            run("CSV leaves legacy logs and unrelated expired files untouched", () =>
            {
                string directory = Sandbox();
                string legacy = Path.Combine(directory, "TempWidget-2020-01-01.csv");
                string unrelated = Path.Combine(directory, "TempWidget-v2-backup.csv");
                foreach (string path in new[] { legacy, unrelated }) { File.WriteAllText(path, "untouched"); File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-40)); }
                new CsvLog(directory, () => SampleTime).Write(Rows());
                Equal("untouched", File.ReadAllText(legacy)); Equal("untouched", File.ReadAllText(unrelated));
            });
            run("CSV refuses to append to a mismatched or partial existing file", () =>
            {
                foreach (string content in new[] { "wrong header\r\n", "partial" })
                {
                    string directory = Sandbox(); var rows = Rows(); Format(rows, out string header, out string line);
                    string file = Path.Combine(directory, CsvLog.SchemaFileName(SampleTime, header));
                    string original = content == "partial" ? header + Environment.NewLine + line : content;
                    File.WriteAllText(file, original, new System.Text.UTF8Encoding(false));
                    new CsvLog(directory, () => SampleTime).Write(rows);
                    Equal(original, File.ReadAllText(file));
                }
            });
            run("CSV does not create an empty file for nonnumeric rows", () =>
            {
                string directory = Sandbox();
                new CsvLog(directory, () => SampleTime).Write(new List<Row> { new Row { Label = "Notice", Kind = RowKind.Notice } });
                Equal(0, Directory.GetFiles(directory).Length);
            });
            run("Alert hot samples cannot span a disappearing sensor or disabled alerts", () =>
            {
                foreach (bool disable in new[] { false, true })
                {
                    var alerts = new Alerts(); var settings = new Settings { AlertsEnabled = true, AlertCpu = 95 };
                    var rows = new List<Row> { new Row { Key = "cpu.temp", Kind = RowKind.Temp, Section = Section.Cpu, Value = 100 } };
                    Equal(0, alerts.Check(rows, settings, v => v.ToString()).Count);
                    Equal(0, alerts.Check(rows, settings, v => v.ToString()).Count);
                    settings.AlertsEnabled = !disable;
                    alerts.Check(disable ? rows : new List<Row>(), settings, v => v.ToString());
                    settings.AlertsEnabled = true;
                    Equal(0, alerts.Check(rows, settings, v => v.ToString()).Count);
                    Equal(0, alerts.Check(rows, settings, v => v.ToString()).Count);
                    Equal(1, alerts.Check(rows, settings, v => v.ToString()).Count);
                }
            });
            run("LhmSource hardware disposal closes Computer even when EC disposal throws", () =>
            {
                int closed = 0;
                LhmSource.DisposeHardwareResources(new ThrowingDisposable(), () => closed++);
                Equal(1, closed);
            });
            run("SensorWorker deterministic stop disposes active source once on normal exit", () =>
            {
                var source = new FakeSensorSource();
                var opened = new System.Threading.ManualResetEventSlim(false);
                var worker = new SensorWorker();
                worker.Start(() =>
                {
                    try
                    {
                        var current = worker.ReplaceSource(() => source);
                        opened.Set();
                        while (!worker.Stopping && current != null)
                        {
                            current.Poll(false);
                            worker.WaitNext(200);
                        }
                    }
                    finally { worker.ReleaseCurrentSource(); }
                });
                True(opened.Wait(2000));
                True(worker.Stop(2000));
                worker.Dispose();
                Equal(1, source.Disposals);
            });
            run("SensorWorker deterministic stop still disposes active source on worker join timeout", () =>
            {
                var source = new FakeSensorSource();
                var entered = new System.Threading.ManualResetEventSlim(false);
                var unblock = new System.Threading.ManualResetEventSlim(false);
                var finished = new System.Threading.ManualResetEventSlim(false);
                var worker = new SensorWorker();
                worker.Start(() =>
                {
                    try
                    {
                        worker.ReplaceSource(() => source);
                        entered.Set();
                        unblock.Wait(5000);
                    }
                    finally
                    {
                        worker.ReleaseCurrentSource();
                        finished.Set();
                    }
                });
                True(entered.Wait(2000));
                bool joined = worker.Stop(40);
                Equal(false, joined);
                // Even though Join(40) timed out, active source must already be deterministically disposed.
                Equal(1, source.Disposals);
                unblock.Set();
                True(finished.Wait(2000));
                // Late worker completion must not double-dispose the source.
                Equal(1, source.Disposals);
                worker.Dispose();
            });
            run("SensorWorker racing source creation during shutdown immediately disposes late-opened source", () =>
            {
                var lateSource = new FakeSensorSource();
                var worker = new SensorWorker();
                var accepted = worker.ReplaceSource(() =>
                {
                    worker.Stop(0);
                    return lateSource;
                });
                Equal<ISensorSource>(null, accepted);
                Equal(1, lateSource.Disposals);
                Equal<ISensorSource>(null, worker.ReplaceSource(() => new FakeSensorSource()));
            });
        }

        private static List<Row> Rows() => new List<Row>
        {
            new Row { Key = "cpu.temp", Kind = RowKind.Temp, Section = Section.Cpu, Label = "Temperature", Value = 60 },
            new Row { Key = "fan.0", Kind = RowKind.Value, Section = Section.Fans, Label = "Fan 1", Value = 1000, Unit = "RPM" }
        };
        private static void Format(List<Row> rows, out string header, out string line) => CsvLog.FormatRows(rows, SampleTime, out header, out line);
        private static string Sandbox()
        {
            // Test-created files are confined to the test output directory in the workspace.
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "synthetic-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path); return path;
        }
        private static void True(bool value) { if (!value) throw new Exception("Assertion failed"); }
        private static void Equal<T>(T expected, T actual)
        { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}, got {actual}"); }
        private sealed class FakeStartupStore : IStartupStore
        {
            public LegacyStartupTask Legacy;
            public bool ThrowProbe;
            public string Command;
            public int Writes, Deletes;
            public LegacyStartupTask ReadLegacyTask() { if (ThrowProbe) throw new Exception("Synthetic probe error"); return Legacy; }
            public string ReadRunValue() => Command;
            public void WriteRunValue(string value) { Writes++; Command = value; }
            public void DeleteRunValue() { Deletes++; Command = null; }
        }
        private sealed class ThrowingDisposable : IDisposable
        {
            public void Dispose() { throw new InvalidOperationException("Synthetic EC dispose failure"); }
        }
        private sealed class FakeSensorSource : ISensorSource
        {
            public int Disposals;
            public List<Row> Poll(bool light) => new List<Row>();
            public string Report() => "Synthetic";
            public void Dispose() { System.Threading.Interlocked.Increment(ref Disposals); }
        }
    }
}
