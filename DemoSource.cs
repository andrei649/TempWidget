using System;
using System.Collections.Generic;

namespace TempWidget
{
    /// <summary>
    /// Simulated readings (start with --demo). Used for screenshots and for checking the
    /// layout on machines without sensors; never used unless that switch is given.
    /// </summary>
    internal sealed class DemoSource : ISensorSource
    {
        private readonly Settings _s;
        private int _tick;

        public DemoSource(Settings s) { _s = s; }

        public List<Row> Poll(bool light)
        {
            double t = _tick++ * 0.35;
            float wave = (float)(Math.Sin(t) * 0.5 + 0.5), wave2 = (float)(Math.Sin(t * 0.37 + 1) * 0.5 + 0.5);
            float cpuLoad = 18 + 55 * wave * wave2, gpuLoad = 30 + 60 * wave2;
            var rows = new List<Row>();
            if (Array.IndexOf(Program.Args, "--demo-limited") >= 0)
                rows.Add(new Row { Key = "notice.elevate", Section = Section.Notice, Kind = RowKind.Notice, Action = NoticeAction.Elevate,
                                   Label = "Limited mode \u00B7 click for all sensors" });

            if (_s.ShowCpu)
            {
                var cpu = H(Section.Cpu, "CPU", "Ryzen 9 7950X");
                cpu.Detail = "Cores: 61  63  58  66  60  59  62  64 \u00B0C";
                rows.Add(cpu);
                rows.Add(T(Section.Cpu, "cpu.avg", "Temp (average)", 52 + 22 * wave2 + 3 * wave, 75, 90, true, null));
                rows.Add(T(Section.Cpu, "cpu.temp", "Temp (hottest)", 52 + 22 * wave2 + 3 * wave + 4 + 14 * wave, 85, 100, true, "CPU"));
                rows.Add(L(Section.Cpu, "cpu.load", "Load", cpuLoad, "CPU load"));
                rows.Add(V(Section.Cpu, "cpu.power", "Power", $"{20 + cpuLoad * 1.1f:0.0} W"));
                rows.Add(V(Section.Cpu, "cpu.clock", "Clock (max)", $"{3.2 + 2.3 * wave:0.00} GHz"));
            }
            if (_s.ShowGpu)
            {
                rows.Add(H(Section.Gpu, "GPU", "RTX 4080"));
                rows.Add(T(Section.Gpu, "gpu0.temp", "Temperature", 48 + 26 * wave2, 75, 87, true, "GPU"));
                rows.Add(T(Section.Gpu, "gpu0.hotspot", "Hot spot", 56 + 30 * wave2, 85, 100, false, null));
                rows.Add(L(Section.Gpu, "gpu0.load", "Load", gpuLoad, "GPU load"));
                rows.Add(V(Section.Gpu, "gpu0.power", "Power", $"{35 + gpuLoad * 2.4f:0.0} W"));
                var vram = V(Section.Gpu, "gpu0.vram", "VRAM", $"{4.2 + 6 * wave2:0.0} / 16 GB");
                vram.Bar = (4.2f + 6 * wave2) / 16f;
                rows.Add(vram);
            }
            if (light) return rows;

            if (_s.ShowRam)
            {
                rows.Add(H(Section.Ram, "RAM", null));
                rows.Add(new Row { Key = "ram.used", Section = Section.Ram, Kind = RowKind.Value, Label = "Used", Text = "21.4 / 64 GB", Bar = 0.33f, Compact = "RAM" });
                rows.Add(T(Section.Ram, "ram.temp", "Temp (max of 2)", 44 + 4 * wave2, 70, 85, false, null));
            }
            if (_s.ShowFans)
            {
                rows.Add(H(Section.Fans, "Fans", null));
                var f1 = V(Section.Fans, "fan.0", "Fan #1", $"{900 + 900 * wave2:0} RPM"); f1.Compact = "Fan #1"; rows.Add(f1);
                var f2 = V(Section.Fans, "fan.1", "GPU Fan", $"{1100 + 1200 * wave2:0} RPM"); f2.Compact = "GPU Fan"; rows.Add(f2);
            }
            if (_s.ShowDrives)
            {
                rows.Add(H(Section.Drives, "Drives", null));
                var d0 = T(Section.Drives, "drive.0", "990 PRO 2TB", 41 + 3 * wave, 60, 70, false, null);
                d0.Detail = "62% full, 98% life left";
                rows.Add(d0);
                rows.Add(T(Section.Drives, "drive.1", "WD Black SN850X 4TB", 38 + 2 * wave2, 60, 70, false, null));
            }
            if (_s.ShowNetwork)
            {
                rows.Add(H(Section.Network, "Network", "Wi-Fi"));
                var d = V(Section.Network, "net.down", "Download", LhmSource.Speed(40000 + 9000000 * wave * wave)); d.Compact = "Download"; rows.Add(d);
                var u = V(Section.Network, "net.up", "Upload", LhmSource.Speed(12000 + 600000 * wave2)); u.Compact = "Upload"; rows.Add(u);
            }
            if (_s.ShowBattery)
            {
                rows.Add(H(Section.Battery, "Battery", null));
                rows.Add(new Row { Key = "bat.charge", Section = Section.Battery, Kind = RowKind.Value, Label = "Charge", Text = "80%", Bar = 0.8f, Compact = "Battery" });
                var st = V(Section.Battery, "bat.rate", "Status", "Not charging"); st.Level = Level.Dim; rows.Add(st);
                rows.Add(V(Section.Battery, "bat.wear", "Wear", "3.2%"));
            }
            return rows;
        }

        public string Report() => "Demo mode: simulated readings, no hardware was read.";
        public void Dispose() { }

        private static Row H(Section s, string title, string sub) => new Row { Section = s, Kind = RowKind.Header, Label = title, Text = sub ?? "" };
        private static Row V(Section s, string key, string label, string text) => new Row { Key = key, Section = s, Kind = RowKind.Value, Label = label, Text = text };
        private static Row T(Section s, string key, string label, float t, float warm, float hot, bool spark, string compact) =>
            new Row { Key = key, Section = s, Kind = RowKind.Temp, Label = label, Value = t, Warm = warm, Hot = hot, Spark = spark, Compact = compact };
        private static Row L(Section s, string key, string label, float pct, string compact) =>
            new Row { Key = key, Section = s, Kind = RowKind.Value, Label = label, Text = $"{pct:0}%", Bar = pct / 100f, Compact = compact,
                      Level = pct >= 90 ? Level.Hot : pct >= 70 ? Level.Warm : Level.Normal };
    }
}
