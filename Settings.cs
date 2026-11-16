using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace TempWidget
{
    /// <summary>Key=value settings file in %APPDATA%\TempWidget. Reads v1 files and upgrades them.</summary>
    internal sealed class Settings
    {
        public static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TempWidget");
        private static readonly string FilePath = Path.Combine(Dir, "settings.ini");

        // window
        public int X = -1, Y = -1;
        public bool TopMost = true;
        public bool Locked;
        public bool ClickThrough;
        public bool SnapEdges = true;
        public bool AnchorRight, AnchorBottom; // keep that edge fixed when the widget changes size
        public double Opacity = 0.9;
        public int ScalePercent = 100;
        public int Theme;              // 0 dark, 1 light, 2 match Windows

        // content
        public bool ShowCpu = true, ShowGpu = true, ShowRam = true, ShowFans = true, ShowDrives = true, ShowBattery = true;
        public bool ShowNetwork = true;
        public bool Compact;
        public bool Sparklines = true;
        public bool ShowHints = true;
        public bool Fahrenheit;
        public int EcMode;             // 0 auto (verified laptops only), 1 on (experimental), 2 off

        // behaviour
        public int IntervalMs = 2000;
        public bool BatterySaver = true;   // refresh more slowly on battery power
        public int TrayMode = 1;           // tray icon shows: 0 app icon, 1 CPU, 2 GPU, 3 the hotter of the two
        public bool LogCsv;                // write every reading to a daily CSV file
        public bool Background;            // keep polling CPU/GPU while hidden (tray temperature + alerts)
        public bool PreferElevated;        // relaunch as admin on start for full sensors
        public bool Onboarded;             // the welcome screen has been answered

        // alerts (thresholds in °C)
        public bool AlertsEnabled;
        public int AlertCpu = 95, AlertGpu = 87, AlertDrive = 70;

        public Settings Clone() => (Settings)MemberwiseClone();

        public bool SectionEnabled(Section s)
        {
            switch (s)
            {
                case Section.Cpu: return ShowCpu;
                case Section.Gpu: return ShowGpu;
                case Section.Ram: return ShowRam;
                case Section.Fans: return ShowFans;
                case Section.Drives: return ShowDrives;
                case Section.Battery: return ShowBattery;
                case Section.Network: return ShowNetwork;
                default: return true;
            }
        }

        /// <summary>True when a change between the two needs the sensor backend rebuilt.</summary>
        public bool SensorsDiffer(Settings o) =>
            ShowCpu != o.ShowCpu || ShowGpu != o.ShowGpu || ShowRam != o.ShowRam || ShowFans != o.ShowFans ||
            ShowDrives != o.ShowDrives || ShowBattery != o.ShowBattery || EcMode != o.EcMode ||
            ShowNetwork != o.ShowNetwork || Fahrenheit != o.Fahrenheit;

        public static Settings Load()
        {
            var s = new Settings();
            try
            {
                if (!File.Exists(FilePath)) return s;
                var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var line in File.ReadAllLines(FilePath))
                {
                    int i = line.IndexOf('=');
                    if (i > 0) d[line.Substring(0, i).Trim()] = line.Substring(i + 1).Trim();
                }

                bool v1 = !d.ContainsKey("Version");
                s.Onboarded = Bool(d, "Onboarded", true); // an existing settings file means an existing user
                s.X = Int(d, "X", s.X);
                s.Y = Int(d, "Y", s.Y);
                s.TopMost = Bool(d, "TopMost", s.TopMost);
                s.Locked = Bool(d, "Locked", s.Locked);
                s.ClickThrough = Bool(d, "ClickThrough", s.ClickThrough);
                s.SnapEdges = Bool(d, "SnapEdges", s.SnapEdges);
                s.AnchorRight = Bool(d, "AnchorRight", false);
                s.AnchorBottom = Bool(d, "AnchorBottom", false);
                s.ScalePercent = Clamp(Int(d, "ScalePercent", s.ScalePercent), 70, 200);
                s.Theme = Clamp(Int(d, "Theme", s.Theme), 0, 2);
                if (d.TryGetValue("Opacity", out var o) && double.TryParse(o, NumberStyles.Float, CultureInfo.InvariantCulture, out var op))
                    s.Opacity = Math.Max(0.3, Math.Min(1, op));

                s.ShowCpu = Bool(d, "ShowCpu", true);
                s.ShowGpu = Bool(d, "ShowGpu", Bool(d, "MonitorGpu", true)); // v1 name
                s.ShowRam = Bool(d, "ShowRam", true);
                s.ShowFans = Bool(d, "ShowFans", true);
                s.ShowDrives = Bool(d, "ShowDrives", true);
                s.ShowBattery = Bool(d, "ShowBattery", true);
                s.Compact = Bool(d, "Compact", s.Compact);
                s.Sparklines = Bool(d, "Sparklines", s.Sparklines);
                s.ShowHints = Bool(d, "ShowHints", s.ShowHints);
                s.Fahrenheit = Bool(d, "Fahrenheit", s.Fahrenheit);
                s.EcMode = Clamp(Int(d, "EcMode", v1 && !Bool(d, "EcFans", true) ? 2 : 0), 0, 2);

                s.IntervalMs = Clamp(Int(d, "IntervalMs", s.IntervalMs), 500, 60000);
                s.BatterySaver = Bool(d, "BatterySaver", s.BatterySaver);
                s.TrayMode = Clamp(Int(d, "TrayMode", Bool(d, "TrayTemp", true) ? 1 : 0), 0, 3);
                s.ShowNetwork = Bool(d, "ShowNetwork", true);
                s.LogCsv = Bool(d, "LogCsv", false);
                s.Background = Bool(d, "Background", s.Background);
                // v1 always ran as administrator, so keep that behaviour for people upgrading.
                s.PreferElevated = Bool(d, "PreferElevated", v1);

                s.AlertsEnabled = Bool(d, "AlertsEnabled", s.AlertsEnabled);
                s.AlertCpu = Clamp(Int(d, "AlertCpu", s.AlertCpu), 40, 120);
                s.AlertGpu = Clamp(Int(d, "AlertGpu", s.AlertGpu), 40, 120);
                s.AlertDrive = Clamp(Int(d, "AlertDrive", s.AlertDrive), 30, 100);
            }
            catch { /* fall back to defaults */ }
            return s;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                var lines = new[]
                {
                    "Version=2",
                    "X=" + X, "Y=" + Y, B("TopMost", TopMost), B("Locked", Locked), B("ClickThrough", ClickThrough),
                    B("SnapEdges", SnapEdges), B("AnchorRight", AnchorRight), B("AnchorBottom", AnchorBottom), "Opacity=" + Opacity.ToString(CultureInfo.InvariantCulture),
                    "ScalePercent=" + ScalePercent, "Theme=" + Theme,
                    B("ShowCpu", ShowCpu), B("ShowGpu", ShowGpu), B("ShowRam", ShowRam), B("ShowFans", ShowFans),
                    B("ShowDrives", ShowDrives), B("ShowBattery", ShowBattery), B("Compact", Compact),
                    B("Sparklines", Sparklines), B("ShowHints", ShowHints), B("Fahrenheit", Fahrenheit), "EcMode=" + EcMode,
                    "IntervalMs=" + IntervalMs, B("BatterySaver", BatterySaver), "TrayMode=" + TrayMode, B("ShowNetwork", ShowNetwork), B("LogCsv", LogCsv),
                    B("Background", Background), B("PreferElevated", PreferElevated), B("Onboarded", Onboarded),
                    B("AlertsEnabled", AlertsEnabled), "AlertCpu=" + AlertCpu, "AlertGpu=" + AlertGpu, "AlertDrive=" + AlertDrive,
                };
                // Write to a temp file first so a crash mid-write cannot leave a half-written settings file.
                string tmp = FilePath + ".tmp";
                File.WriteAllLines(tmp, lines);
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(tmp, FilePath);
            }
            catch { }
        }

        private static string B(string k, bool v) => k + "=" + (v ? 1 : 0);
        private static int Clamp(int v, int lo, int hi) => Math.Max(lo, Math.Min(hi, v));

        private static int Int(Dictionary<string, string> d, string k, int def) =>
            d.TryGetValue(k, out var v) && int.TryParse(v, out var n) ? n : def;

        private static bool Bool(Dictionary<string, string> d, string k, bool def) => Int(d, k, def ? 1 : 0) == 1;
    }
}
