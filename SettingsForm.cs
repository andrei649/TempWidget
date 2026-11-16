using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace TempWidget
{
    internal sealed class SettingsForm : Form
    {
        private readonly WidgetForm _owner;
        private readonly TabControl _tabs = new TabControl();
        private readonly bool _autoStartWas;

        private ComboBox _interval, _size, _theme, _unit, _ec, _tray;
        private TrackBar _opacity;
        private Label _opacityText;
        private CheckBox _top, _locked, _snap, _through, _autoStart, _battery;
        private CheckBox _cpu, _gpu, _ram, _fans, _drives, _bat, _compact, _spark, _hints, _background, _net, _log;
        private CheckBox _alerts;
        private NumericUpDown _alertCpu, _alertGpu, _alertDrive;

        private static readonly int[] Intervals = { 1000, 2000, 3000, 5000, 10000 };
        private static readonly int[] Sizes = { 85, 100, 115, 130, 150, 175 };

        public int TabCount => _tabs.TabCount;
        public void SelectTab(int i) => _tabs.SelectedIndex = i;

        public SettingsForm(WidgetForm owner)
        {
            _owner = owner;
            _autoStartWas = !Program.Demo && AutoStart.IsEnabled();

            Text = "TempWidget settings";
            SuspendLayout(); // controls are laid out for 96 DPI; scaling happens once, in ResumeLayout
            Font = new Font("Segoe UI", 9f);
            AutoScaleDimensions = new SizeF(96f, 96f);
            AutoScaleMode = AutoScaleMode.Dpi;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(440, 570);

            _tabs.SetBounds(10, 10, 420, 510);
            _tabs.TabPages.Add(GeneralTab());
            _tabs.TabPages.Add(DisplayTab());
            _tabs.TabPages.Add(AlertsTab());
            _tabs.TabPages.Add(AboutTab());
            Controls.Add(_tabs);

            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
            var apply = new Button { Text = "Apply" };
            ok.SetBounds(190, 532, 76, 27);
            cancel.SetBounds(272, 532, 76, 27);
            apply.SetBounds(354, 532, 76, 27);
            ok.Click += (s, e) => { Apply(); Close(); };
            cancel.Click += (s, e) => Close();
            apply.Click += (s, e) => Apply();
            Controls.AddRange(new Control[] { ok, cancel, apply });
            AcceptButton = ok;
            CancelButton = cancel;

            ResumeLayout(false);
            PerformLayout();

            LoadFrom(owner.CurrentSettings);
        }

        // ---------- pages ----------

        private TabPage GeneralTab()
        {
            var p = Page("General");
            int y = 16;
            _interval = Combo(p, "Refresh every", ref y, "1 second", "2 seconds", "3 seconds", "5 seconds", "10 seconds");
            _size = Combo(p, "Size", ref y, "85%", "100%", "115%", "130%", "150%", "175%");
            _theme = Combo(p, "Theme", ref y, "Dark", "Light", "Match Windows");
            _unit = Combo(p, "Temperature unit", ref y, "Celsius (°C)", "Fahrenheit (°F)");

            p.Controls.Add(Lbl("Opacity", 16, y + 4, 130));
            _opacity = new TrackBar { Minimum = 30, Maximum = 100, TickFrequency = 10, SmallChange = 5, LargeChange = 10, AutoSize = false };
            _opacity.SetBounds(150, y - 2, 190, 30);
            _opacityText = Lbl("", 346, y + 4, 50);
            _opacity.ValueChanged += (s, e) => _opacityText.Text = _opacity.Value + "%";
            p.Controls.Add(_opacity);
            p.Controls.Add(_opacityText);
            y += 42;

            _top = Check(p, "Always on top", ref y);
            _locked = Check(p, "Lock position", ref y);
            _snap = Check(p, "Snap to screen edges", ref y);
            _through = Check(p, "Click-through (turn it off again from the tray icon)", ref y);
            y += 8;
            _autoStart = Check(p, "Start with Windows", ref y);
            p.Controls.Add(Note("Startup uses basic sensors. Enable full sensors from the widget each session.", 16, y, 390, 34));
            y += 38;
            _battery = Check(p, "Refresh more slowly on battery power", ref y);
            return p;
        }

        private TabPage DisplayTab()
        {
            var p = Page("Display");
            int y = 14;
            p.Controls.Add(Heading("Sections", 16, y)); y += 24;
            int y2 = y;
            _cpu = Check(p, "CPU", ref y);
            _gpu = Check(p, "GPU", ref y);
            _ram = Check(p, "RAM", ref y);
            _net = Check(p, "Network", ref y);
            _fans = Check(p, "Fans", ref y2, 200);
            _drives = Check(p, "Drives", ref y2, 200);
            _bat = Check(p, "Battery", ref y2, 200);
            y += 8;

            p.Controls.Add(Heading("Layout", 16, y)); y += 24;
            _compact = Check(p, "Compact mode (one line per key reading)", ref y);
            _spark = Check(p, "Temperature history lines", ref y);
            _hints = Check(p, "Show hints about missing sensors", ref y);
            y += 8;

            p.Controls.Add(Heading("Tray icon", 16, y)); y += 24;
            _tray = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _tray.Items.AddRange(new object[] { "App icon only", "CPU temperature", "GPU temperature", "Hotter of CPU and GPU" });
            _tray.SetBounds(16, y, 290, 23);
            p.Controls.Add(_tray);
            y += 30;
            _background = Check(p, "Keep monitoring while the widget is hidden", ref y);
            p.Controls.Add(Note("Needed for the tray temperature and alerts while hidden. Reads only CPU and GPU, every 5 seconds or slower.", 34, y, 360, 32));
            y += 40;

            p.Controls.Add(Heading("Laptop fan readout", 16, y)); y += 24;
            _ec = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _ec.Items.AddRange(new object[] { "Automatic (verified laptops only)", "On – experimental, Clevo-based laptops", "Off" });
            _ec.SetBounds(16, y, 290, 23);
            p.Controls.Add(_ec);
            y += 28;
            p.Controls.Add(Note("Reads fan speed from the laptop's embedded controller (read-only). Leave on Automatic unless your laptop is Clevo-based and shows no fans.", 16, y, 380, 50));
            return p;
        }

        private TabPage AlertsTab()
        {
            var p = Page("Alerts and log");
            int y = 16;
            _alerts = Check(p, "Notify me when a temperature stays too high", ref y);
            y += 6;
            _alertCpu = Number(p, "CPU above", ref y, 40, 120);
            _alertGpu = Number(p, "GPU above", ref y, 40, 120);
            _alertDrive = Number(p, "Any drive above", ref y, 30, 100);
            y += 6;
            p.Controls.Add(Note("A Windows notification appears after three readings in a row above the limit, and at most once every five minutes per sensor.\r\n\r\n" +
                                "While the widget is hidden, alerts need “Keep monitoring while the widget is hidden” on the Display tab.", 16, y, 380, 96));
            y += 104;
            p.Controls.Add(Heading("Logging", 16, y)); y += 24;
            _log = Check(p, "Save every reading to a CSV file (one file per day)", ref y);
            var folder = new Button { Text = "Open log folder" };
            folder.SetBounds(34, y + 2, 130, 27);
            folder.Click += (s, e) =>
            {
                try { System.IO.Directory.CreateDirectory(CsvLog.Dir); Process.Start(new ProcessStartInfo(CsvLog.Dir) { UseShellExecute = true }); } catch { }
            };
            p.Controls.Add(folder);
            p.Controls.Add(Note("The files open in Excel. Files older than 30 days are deleted.", 34, y + 36, 360, 18));
            _alerts.CheckedChanged += (s, e) => _alertCpu.Enabled = _alertGpu.Enabled = _alertDrive.Enabled = _alerts.Checked;
            return p;
        }

        private TabPage AboutTab()
        {
            var p = Page("About");
            int y = 16;
            p.Controls.Add(new Label { Text = "TempWidget " + Program.Version, Font = new Font("Segoe UI Semibold", 12f), AutoSize = true, Location = new Point(16, y) });
            y += 34;
            string mode = Program.Demo ? "Demo (simulated readings)" : Program.IsElevated ? "Full sensors (administrator)" : "Limited (no administrator rights)";
            p.Controls.Add(Lbl("Mode: " + mode, 16, y, 380)); y += 20;
            if (!Program.Demo) { p.Controls.Add(Lbl("PawnIO driver: " + (DriverSetup.IsInstalled() ? "installed" : "not installed"), 16, y, 380)); y += 20; }
            p.Controls.Add(Lbl("Install type: " + (Program.IsPackaged ? "Microsoft Store" : "portable"), 16, y, 380)); y += 30;

            var report = new Button { Text = "Copy sensor report" };
            var pawn = new Button { Text = "PawnIO website" };
            var folder = new Button { Text = "Settings folder" };
            report.SetBounds(16, y, 130, 27);
            pawn.SetBounds(152, y, 110, 27);
            folder.SetBounds(268, y, 110, 27);
            report.Click += (s, e) =>
            {
                report.Enabled = false;
                _owner.RequestReport(text =>
                {
                    try { Clipboard.SetText(text); } catch { }
                    if (!report.IsDisposed) { report.Enabled = true; report.Text = "Copied"; }
                });
            };
            pawn.Click += (s, e) => WidgetForm.OpenUrl("https://pawnio.eu");
            folder.Click += (s, e) => { try { Process.Start(new ProcessStartInfo(Settings.Dir) { UseShellExecute = true }); } catch { } };
            p.Controls.AddRange(new Control[] { report, pawn, folder });
            y += 40;

            var notes = new TextBox
            {
                Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.FixedSingle,
                Text = "Privacy\r\nTempWidget reads hardware sensors on this PC and shows them. It makes no network connections, " +
                       "collects nothing and sends nothing. Settings are stored in a local file.\r\n\r\n" +
                       "The sensor report contains hardware model names and sensor values only; it is copied to the clipboard and goes nowhere unless you paste it.\r\n\r\n" +
                       "Open-source components\r\n" +
                       "LibreHardwareMonitorLib, DiskInfoToolkit, RAMSPDToolkit, BlackSharp.Core – Mozilla Public License 2.0\r\n" +
                       "HidSharp – Apache License 2.0\r\n" +
                       ".NET libraries – MIT License\r\n" +
                       "Details and licence texts are in THIRD-PARTY-NOTICES.txt next to the program.\r\n\r\n" +
                       "CPU sensors use the PawnIO driver (pawnio.eu, GPL-2.0). Its official installer is included and is only run when you ask for full sensors.",
            };
            notes.SetBounds(16, y, 380, 470 - y);
            p.Controls.Add(notes);
            return p;
        }

        // ---------- load / apply ----------

        private void LoadFrom(Settings s)
        {
            _interval.SelectedIndex = Nearest(Intervals, s.IntervalMs);
            _size.SelectedIndex = Nearest(Sizes, s.ScalePercent);
            _theme.SelectedIndex = s.Theme;
            _unit.SelectedIndex = s.Fahrenheit ? 1 : 0;
            _opacity.Value = Math.Max(30, Math.Min(100, (int)Math.Round(s.Opacity * 100)));
            _opacityText.Text = _opacity.Value + "%";
            _top.Checked = s.TopMost;
            _locked.Checked = s.Locked;
            _snap.Checked = s.SnapEdges;
            _through.Checked = s.ClickThrough;
            _autoStart.Checked = _autoStartWas;
            _battery.Checked = s.BatterySaver;

            _cpu.Checked = s.ShowCpu; _gpu.Checked = s.ShowGpu; _ram.Checked = s.ShowRam;
            _fans.Checked = s.ShowFans; _drives.Checked = s.ShowDrives; _bat.Checked = s.ShowBattery;
            _compact.Checked = s.Compact;
            _spark.Checked = s.Sparklines;
            _hints.Checked = s.ShowHints;
            _tray.SelectedIndex = s.TrayMode;
            _net.Checked = s.ShowNetwork;
            _log.Checked = s.LogCsv;
            _background.Checked = s.Background;
            _ec.SelectedIndex = s.EcMode;

            _alerts.Checked = s.AlertsEnabled;
            _alertCpu.Value = s.AlertCpu;
            _alertGpu.Value = s.AlertGpu;
            _alertDrive.Value = s.AlertDrive;
            _alertCpu.Enabled = _alertGpu.Enabled = _alertDrive.Enabled = s.AlertsEnabled;
        }

        private void Apply()
        {
            var old = _owner.CurrentSettings;
            var s = old.Clone();
            s.IntervalMs = Intervals[_interval.SelectedIndex];
            s.ScalePercent = Sizes[_size.SelectedIndex];
            s.Theme = _theme.SelectedIndex;
            s.Fahrenheit = _unit.SelectedIndex == 1;
            s.Opacity = _opacity.Value / 100.0;
            s.TopMost = _top.Checked;
            s.Locked = _locked.Checked;
            s.SnapEdges = _snap.Checked;
            s.ClickThrough = _through.Checked;
            s.BatterySaver = _battery.Checked;

            s.ShowCpu = _cpu.Checked; s.ShowGpu = _gpu.Checked; s.ShowRam = _ram.Checked;
            s.ShowFans = _fans.Checked; s.ShowDrives = _drives.Checked; s.ShowBattery = _bat.Checked;
            s.Compact = _compact.Checked;
            s.Sparklines = _spark.Checked;
            s.ShowHints = _hints.Checked;
            s.TrayMode = _tray.SelectedIndex;
            s.ShowNetwork = _net.Checked;
            s.LogCsv = _log.Checked;
            s.Background = _background.Checked;
            s.EcMode = _ec.SelectedIndex;

            s.AlertsEnabled = _alerts.Checked;
            s.AlertCpu = (int)_alertCpu.Value;
            s.AlertGpu = (int)_alertGpu.Value;
            s.AlertDrive = (int)_alertDrive.Value;
            _owner.ApplySettings(s);

            if (!Program.Demo && _autoStart.Checked != AutoStart.IsEnabled())
            {
                string problem = AutoStart.Set(_autoStart.Checked);
                if (problem != null) MessageBox.Show(this, problem, "TempWidget", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _autoStart.Checked = AutoStart.IsEnabled();
            }

        }

        // ---------- small builders ----------

        private static TabPage Page(string title) => new TabPage(title) { UseVisualStyleBackColor = true };

        private static Label Lbl(string text, int x, int y, int w) =>
            new Label { Text = text, Location = new Point(x, y), Size = new Size(w, 18) };

        private static Label Heading(string text, int x, int y) =>
            new Label { Text = text, Font = new Font("Segoe UI Semibold", 9f), Location = new Point(x, y), AutoSize = true };

        private static Label Note(string text, int x, int y, int w, int h) =>
            new Label { Text = text, ForeColor = SystemColors.GrayText, Location = new Point(x, y), Size = new Size(w, h) };

        private static CheckBox Check(TabPage p, string text, ref int y, int x = 16)
        {
            var c = new CheckBox { Text = text, Location = new Point(x, y), Size = new Size(x == 16 && text.Length > 12 ? 380 : 170, 22) };
            p.Controls.Add(c);
            y += 24;
            return c;
        }

        private static ComboBox Combo(TabPage p, string label, ref int y, params string[] items)
        {
            p.Controls.Add(Lbl(label, 16, y + 4, 130));
            var c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            c.Items.AddRange(items);
            c.SetBounds(150, y, 190, 23);
            p.Controls.Add(c);
            y += 32;
            return c;
        }

        private static NumericUpDown Number(TabPage p, string label, ref int y, int min, int max)
        {
            p.Controls.Add(Lbl(label, 34, y + 3, 110));
            var n = new NumericUpDown { Minimum = min, Maximum = max };
            n.SetBounds(150, y, 60, 23);
            p.Controls.Add(n);
            p.Controls.Add(Lbl("°C", 216, y + 3, 40));
            y += 30;
            return n;
        }

        private static int Nearest(int[] options, int value)
        {
            int best = 0;
            for (int i = 1; i < options.Length; i++)
                if (Math.Abs(options[i] - value) < Math.Abs(options[best] - value)) best = i;
            return best;
        }
    }
}
