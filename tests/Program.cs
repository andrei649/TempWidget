using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LibreHardwareMonitor.Hardware;

namespace TempWidget.Tests
{
    internal static class Program
    {
        private static int _passed, _failed;

        private static int Main()
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            Run("Windows x64 CLR4 runtime compatibility", () =>
            {
                True(Environment.Is64BitProcess);
                Equal(PlatformID.Win32NT, Environment.OSVersion.Platform);
                Equal(4, Environment.Version.Major);
            });
            Run("CPU with no temperature sensors keeps an unavailable row", () => UnknownCpu(new FakeHardware()));
            foreach (float? value in new float?[] { null, 0, -1, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                var sample = value;
                Run("CPU invalid reading: " + (sample?.ToString() ?? "null"), () =>
                    UnknownCpu(new FakeHardware { Sensors = new ISensor[] { new FakeSensor("CPU Package", sample) } }));
            }
            Run("CPU valid average and hottest package/core selection", () =>
            {
                var rows = Cpu(new FakeSensor("CPU Package", 72), new FakeSensor("Core Max", 78),
                    new FakeSensor("CPU Core #1", 70), new FakeSensor("Core Average", 65));
                Equal(78f, rows.Single(r => r.Key == "cpu.temp").Value);
                Equal(65f, rows.Single(r => r.Key == "cpu.avg").Value);
            });
            Run("CPU valid alias survives zero primary sensor", () =>
                Equal(62f, Cpu(new FakeSensor("CPU Package", 0), new FakeSensor("Core (Tctl/Tdie)", 62)).Single(r => r.Key == "cpu.temp").Value));
            Run("CPU average-only fallback is visible", () =>
                Equal(57f, Cpu(new FakeSensor("Core Average", 57)).Single(r => r.Key == "cpu.temp").Value));
            Run("CPU invalid core temperatures stay out of header details", () =>
            {
                var rows = Cpu(new FakeSensor("CPU Core #1", float.PositiveInfinity));
                Equal<string>(null, rows.Single(r => r.Kind == RowKind.Header).Detail);
                Equal(false, rows.Single(r => r.Kind == RowKind.Temp).HasValue);
            });
            Run("CPU individual cores exclude distance-to-limit sensors", () =>
                Equal(68f, Cpu(new FakeSensor("CPU Core #1", 61), new FakeSensor("CPU Core #2", 68),
                    new FakeSensor("CPU Core #1 Distance", 99)).Single(r => r.Key == "cpu.temp").Value));
            Run("CPU recovery preserves stable identity and compact visibility", () =>
            {
                var sensor = new FakeSensor("CPU Package", 77);
                var hardware = new FakeHardware { Sensors = new ISensor[] { sensor } };
                foreach (float? value in new float?[] { 77, null, 0, 81 })
                {
                    sensor.Value = value;
                    var rows = new List<Row>();
                    LhmSource.AddCpu(rows, hardware, -1, false);
                    var row = rows.Single(r => r.Key == "cpu.temp");
                    Equal("CPU", row.Compact);
                    Equal(value > 0, row.HasValue);
                }
            });
            Run("Multiple CPUs keep distinct unavailable identities", () =>
            {
                var rows = new List<Row>();
                LhmSource.AddCpu(rows, new FakeHardware(), 1, true);
                Equal("cpu1.temp", rows.Single(r => r.Kind == RowKind.Temp).Key);
            });
            Run("Unavailable temperature formatting works in both units", () =>
            {
                Equal("N/A", ReadingPresentation.Temperature(float.NaN, false));
                Equal("N/A", ReadingPresentation.Temperature(float.NaN, true));
                Equal("N/A", ReadingPresentation.Temperature(float.PositiveInfinity, true));
                Equal("25 \u00B0C", ReadingPresentation.Temperature(25, false));
                Equal("77 \u00B0F", ReadingPresentation.Temperature(25, true));
            });
            Run("Tray skips unavailable readings in every selection mode", () =>
            {
                var cpu = new Row { Value = 70 };
                var gpu = new Row();
                Equal(cpu, ReadingPresentation.TrayTemperature(cpu, gpu, 3));
                Equal(cpu, ReadingPresentation.TrayTemperature(cpu, gpu, 1));
                Equal<Row>(null, ReadingPresentation.TrayTemperature(cpu, gpu, 2));
                Equal<Row>(null, ReadingPresentation.TrayTemperature(cpu, gpu, 0));
                gpu.Value = 80;
                Equal(gpu, ReadingPresentation.TrayTemperature(cpu, gpu, 3));
                cpu.Value = float.NaN;
                Equal(gpu, ReadingPresentation.TrayTemperature(cpu, gpu, 3));
                Equal<Row>(null, ReadingPresentation.TrayTemperature(cpu, gpu, 1));
            });
            Run("History rejects unavailable readings without corrupting statistics", () =>
            {
                var history = new History();
                foreach (float v in new[] { 60f, float.NaN, float.PositiveInfinity, float.NegativeInfinity, 80f }) history.Add(v);
                Equal(2, history.Count); Equal(60f, history.Min); Equal(80f, history.Max); Equal(70f, history.Average);
            });
            Run("Unavailable CPU resets consecutive alert samples", () =>
            {
                var alerts = new Alerts();
                var settings = new Settings { AlertsEnabled = true, AlertCpu = 95 };
                var row = new Row { Kind = RowKind.Temp, Section = Section.Cpu, Key = "cpu.temp", Value = 100 };
                var rows = new List<Row> { row };
                Equal(0, alerts.Check(rows, settings, v => v.ToString()).Count);
                Equal(0, alerts.Check(rows, settings, v => v.ToString()).Count);
                row.Value = float.NaN;
                Equal(0, alerts.Check(rows, settings, v => v.ToString()).Count);
                row.Value = float.PositiveInfinity;
                Equal(0, alerts.Check(rows, settings, v => v.ToString()).Count);
                row.Value = 100;
                Equal(0, alerts.Check(rows, settings, v => v.ToString()).Count);
                Equal(0, alerts.Check(rows, settings, v => v.ToString()).Count);
                Equal(1, alerts.Check(rows, settings, v => v.ToString()).Count);
            });
            Run("CSV preserves unavailable numeric columns as empty cells", () =>
            {
                var rows = Cpu(new FakeSensor("CPU Package", 0), new FakeSensor("CPU Total", 18, SensorType.Load));
                CsvLog.FormatRows(rows, new DateTime(2026, 1, 2, 3, 4, 5), out string header, out string line);
                Equal("Time,\"cpu.load | Cpu | Load (%) | CPU: Synthetic\",\"cpu.temp | Cpu | Temperature (\u00B0C) | CPU: Synthetic\"", header);
                Equal("2026-01-02 03:04:05,18,", line);
            });

            Run("EC contention performs zero port reads and zero releases", () =>
            {
                var device = new FakeEcDevice { Acquire = false };
                var readings = new EcFans(device).Read();
                Equal(2, readings.Length); AllUnknown(readings);
                Equal(0, device.Reads); Equal(0, device.Releases); Equal(1, device.Acquisitions);
            });
            Run("EC mutex failure performs zero port reads", () =>
            {
                var device = new FakeEcDevice { ThrowAcquire = true };
                AllUnknown(new EcFans(device).Read());
                Equal(0, device.Reads); Equal(0, device.Releases);
            });
            Run("EC owns lock through six byte reads and releases once", () =>
            {
                var device = EcDevice();
                device.Set(2, 1000);
                var result = new EcFans(device).Read();
                Equal(3, result.Length); Equal(2156, result[0]); Equal(1078, result[1]); Equal(2156, result[2]);
                Equal(6, device.Reads); Equal(1, device.Releases);
            });
            Run("EC successful values are cleared after contention and recover", () =>
            {
                var device = EcDevice(); var fans = new EcFans(device);
                Equal(2156, fans.Read()[0]);
                int reads = device.Reads;
                device.Acquire = false; AllUnknown(fans.Read()); Equal(reads, device.Reads);
                device.Acquire = true; Equal(2156, fans.Read()[0]);
            });
            Run("EC failed low byte clears only that fan's current value", () =>
            {
                var device = EcDevice(); var fans = new EcFans(device); fans.Read();
                device.Fail.Add(0xD1);
                var result = fans.Read(); Equal(-1, result[0]); Equal(1078, result[1]); Equal(2, device.Releases);
            });
            Run("EC driver exception releases mutex and drops old values", () =>
            {
                var device = EcDevice(); var fans = new EcFans(device); fans.Read();
                device.ThrowRead = true; AllUnknown(fans.Read()); Equal(2, device.Releases);
            });
            Run("EC rejects implausible and all-ones sentinel readings", () =>
            {
                var device = EcDevice(); device.Set(0, 1); device.Set(1, 0xFFFF);
                AllUnknown(new EcFans(device).Read());
            });
            Run("EC near-sentinel period is no tach signal and does not reveal fan 3", () =>
            {
                // Seen on a System76 bonw16: the empty fan-3 slot reads 0xF7BA, which the formula turns into 34 RPM.
                var device = EcDevice(); device.Set(0, 0xF7BA); device.Set(2, 0xF7BA);
                var result = new EcFans(device).Read();
                Equal(2, result.Length); Equal(0, result[0]); Equal(1078, result[1]);
            });
            Run("EC zero period reports a stopped fan", () =>
            {
                var device = EcDevice(); device.Set(0, 0);
                Equal(0, new EcFans(device).Read()[0]);
            });
            Run("EC third fan stays visible as unknown after later failures", () =>
            {
                var device = EcDevice(); device.Set(2, 1000); var fans = new EcFans(device);
                var first = fans.Read(); Equal(3, first.Length); first[0] = 123;
                Equal(2156, fans.Read()[0]);
                device.Acquire = false; var unknown = fans.Read(); Equal(3, unknown.Length); AllUnknown(unknown);
                fans.Dispose(); Equal(1, device.Disposals);
            });
            Run("EC unavailable rows expose N/A and no numeric/loggable RPM", () =>
            {
                var rows = new List<Row>();
                LhmSource.AddFans(rows, new IHardware[0], new[] { -1, 0, 2156 });
                var unavailable = rows.Single(r => r.Key == "fan.0");
                Equal("N/A", unavailable.Text); Equal(Level.Dim, unavailable.Level);
                Equal(false, unavailable.HasValue); Equal("Fan 1", unavailable.Compact);
                True(unavailable.Detail.Contains("unavailable"));
                Equal("stopped", rows.Single(r => r.Key == "fan.1").Text);
            });

            foreach (int refresh in new[] { 500, 2000, 5000, 60000 })
            {
                int interval = refresh;
                Run("Elapsed-time deadlines at refresh " + interval + "ms", () => ScheduleAt(interval));
            }
            Run("Hidden polls do not consume initial or overdue deadlines", () =>
            {
                var now = TimeSpan.Zero; var schedule = new PollSchedule(() => now);
                schedule.Take(true, out bool slow, out bool battery); True(!slow && !battery);
                now = TimeSpan.FromSeconds(31); schedule.Take(true, out slow, out battery); True(!slow && !battery);
                schedule.Take(false, out slow, out battery); True(slow && battery);
                now += TimeSpan.FromMinutes(2); schedule.Take(true, out slow, out battery); True(!slow && !battery);
                schedule.Take(false, out slow, out battery); True(slow && battery);
                schedule.Take(false, out slow, out battery); True(!slow && !battery);
            });
            Run("Refresh changes and long pauses do not cause catch-up bursts", () =>
            {
                var now = TimeSpan.Zero; var schedule = new PollSchedule(() => now);
                schedule.Take(false, out bool slow, out bool battery); True(slow && battery);
                for (int i = 0; i < 100; i++) { schedule.Take(false, out slow, out battery); True(!slow && !battery); }
                now = TimeSpan.FromSeconds(10); schedule.Take(false, out slow, out battery); True(!slow && battery);
                now = TimeSpan.FromDays(1); schedule.Take(false, out slow, out battery); True(slow && battery);
                schedule.Take(false, out slow, out battery); True(!slow && !battery);
            });
            SafetyTests.Register(Run);
            MsixStartupTests.Register(Run);
            Console.WriteLine($"RESULT: {_passed} passed, {_failed} failed. Synthetic tests only; no hardware/application startup.");
            return _failed == 0 ? 0 : 1;
        }

        private static List<Row> Cpu(params ISensor[] sensors)
        {
            var rows = new List<Row>();
            LhmSource.AddCpu(rows, new FakeHardware { Sensors = sensors }, -1, false);
            return rows;
        }
        private static void UnknownCpu(FakeHardware hardware)
        {
            var rows = new List<Row>(); LhmSource.AddCpu(rows, hardware, -1, false);
            var row = rows.Single(r => r.Key == "cpu.temp");
            Equal(RowKind.Temp, row.Kind); Equal(false, row.HasValue); Equal(Level.Dim, row.TempLevel);
            Equal("CPU", row.Compact); True(row.Detail.Contains("unavailable"));
        }
        private static FakeEcDevice EcDevice()
        {
            var device = new FakeEcDevice(); device.Set(0, 1000); device.Set(1, 2000); return device;
        }
        private static void AllUnknown(int[] readings) { True(readings.All(v => v == -1)); }
        private static void ScheduleAt(int refresh)
        {
            var now = TimeSpan.Zero; var schedule = new PollSchedule(() => now);
            int slowCount = 0, batteryCount = 0; TimeSpan? previousSlow = null, previousBattery = null;
            for (int ms = 0; ms <= 120000; ms += refresh)
            {
                now = TimeSpan.FromMilliseconds(ms);
                schedule.Take(false, out bool slow, out bool battery);
                if (slow)
                {
                    if (previousSlow.HasValue) True(now - previousSlow.Value >= PollSchedule.SlowInterval);
                    previousSlow = now; slowCount++;
                }
                if (battery)
                {
                    if (previousBattery.HasValue) True(now - previousBattery.Value >= PollSchedule.BatteryInterval);
                    previousBattery = now; batteryCount++;
                }
            }
            Equal(refresh == 60000 ? 3 : 5, slowCount);
            Equal(refresh == 60000 ? 3 : 13, batteryCount);
        }
        private static void Run(string name, Action action)
        {
            try { action(); _passed++; Console.WriteLine("PASS " + name); }
            catch (Exception ex) { _failed++; Console.WriteLine("FAIL " + name + ": " + ex); }
        }
        private static void True(bool condition) { if (!condition) throw new Exception("Assertion failed"); }
        private static void Equal<T>(T expected, T actual)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}, got {actual}");
        }
    }
}
