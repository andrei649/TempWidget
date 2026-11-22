using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace TempWidget
{
    internal sealed class WidgetForm : Form
    {
        private volatile Settings _settings;
        private Theme _theme;
        private readonly NotifyIcon _tray;
        private readonly ContextMenuStrip _menu;
        private readonly ToolTip _tip = new ToolTip { InitialDelay = 400, ReshowDelay = 100, AutoPopDelay = 8000 };
        private readonly AutoResetEvent _wake = new AutoResetEvent(false);
        private readonly Thread _worker;
        private readonly Alerts _alerts = new Alerts();
        private readonly Dictionary<string, History> _history = new Dictionary<string, History>();
        private DateTime _rangeSince = DateTime.Now;
        private SettingsForm _settingsForm;
        private Row _setupNotice; // result of driver setup, shown above everything else
        private Row _startupNotice;

        // UI-thread state
        private List<Row> _lastRows = new List<Row> { new Row { Kind = RowKind.Value, Label = "Starting sensors…", Level = Level.Dim } };
        private List<Row> _visible = new List<Row>();
        private readonly List<int> _rowTops = new List<int>();
        private Font _font, _headerFont;
        private int _dpi = 96;
        private float _scale = 1f;
        private int _hoverRow = -1;

        // tray icon
        private readonly Icon _appIcon;
        private IntPtr _trayHandle = IntPtr.Zero;
        private int _trayShown = int.MinValue;
        private Level _trayLevel;

        // shared with the worker thread
        private volatile bool _shown, _sessionLocked, _exiting, _rebuild;
        private volatile Action<string> _reportCallback;

        public WidgetForm(Settings settings)
        {
            _settings = settings;
            _theme = Theme.Resolve(settings.Theme);

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            DoubleBuffered = true;
            Text = "TempWidget";
            BackColor = _theme.Bg;
            TopMost = settings.TopMost;
            Opacity = EffectiveOpacity(settings);

            _appIcon = LoadAppIcon();
            Icon = _appIcon;

            // Per-monitor DPI where Windows offers it; the Graphics value is the system-wide fallback.
            _dpi = Native.WindowDpi(Handle);
            if (_dpi <= 0) using (var g = CreateGraphics()) _dpi = (int)Math.Round(g.DpiX);
            RebuildFonts();
            Relayout();
            PlaceOnScreen();

            _menu = BuildMenu();
            ContextMenuStrip = _menu;

            _tray = new NotifyIcon { Icon = _appIcon, Text = "TempWidget", Visible = true, ContextMenuStrip = _menu };
            _tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) ToggleVisible(); };

            try
            {
                SystemEvents.SessionSwitch += OnSessionSwitch;
                SystemEvents.PowerModeChanged += OnPowerModeChanged;
            }
            catch { }

            _worker = new Thread(WorkerLoop) { IsBackground = true, Priority = ThreadPriority.BelowNormal, Name = "sensors" };
            _worker.Start();

            int shot = Array.IndexOf(Program.Args, "--shot");
            if (Program.Demo && shot >= 0 && shot + 1 < Program.Args.Length) ScheduleScreenshots(Program.Args[shot + 1]);
        }

        public Settings CurrentSettings => _settings;

        private int S(float px) => (int)Math.Round(px * _scale);

        // ---------- polling (worker thread) ----------

        private void WorkerLoop()
        {
            ISensorSource source = null;
            if (!Program.Demo) PostStartupNotice(AutoStart.StartupWarning());
            if (Program.SetupRequested && !Program.Demo) RunSetup();
            bool trim = false, librariesChecked = Program.Demo;
            CsvLog log = null;
            int failures = 0, warmup = Program.Demo ? History.Capacity : 0;
            try
            {
                while (!_exiting)
                {
                    var s = _settings;
                    bool visible = _shown && !_sessionLocked;
                    // Hidden: nothing is read at all, unless the user wants the tray temperature and alerts kept alive.
                    bool active = visible || (s.Background && !_sessionLocked);
                    bool light = !visible;

                    if (active || _reportCallback != null)
                    {
                        try
                        {
                            if (!librariesChecked) { WaitForLibraries(); librariesChecked = true; }
                            if (_rebuild || source == null)
                            {
                                _rebuild = false;
                                source?.Dispose();
                                source = null;
                                source = Program.Demo ? (ISensorSource)new DemoSource(s) : new LhmSource(s);
                                trim = true;
                            }
                            if (active)
                            {
                                var rows = source.Poll(light);
                                if (s.LogCsv && !light && !Program.Demo) (log ?? (log = new CsvLog())).Write(rows);
                                Post(rows, light);
                            }
                            failures = 0;

                            var report = _reportCallback;
                            if (report != null)
                            {
                                _reportCallback = null;
                                string text = source.Report();
                                if (IsHandleCreated && !_exiting) BeginInvoke((Action)(() => report(text)));
                            }
                        }
                        catch (Exception ex)
                        {
                            if (failures++ == 0) Program.Log(ex);
                            try { source?.Dispose(); } catch { }
                            source = null;
                            _reportCallback = null;
                            // A program file that Windows would not load stays unloadable until the app restarts.
                            bool blocked = ex is System.IO.FileLoadException || ex is System.IO.FileNotFoundException || ex is BadImageFormatException;
                            Post(new List<Row> { new Row
                            {
                                Kind = RowKind.Notice, Action = blocked ? NoticeAction.FileBlocked : NoticeAction.ShowLog,
                                Label = blocked ? "A program file is blocked \u00B7 click" : "Sensors unavailable \u00B7 click for details",
                            } }, false);
                        }
                        if (trim) { Native.TrimWorkingSet(); trim = false; }
                    }

                    int wait = !active ? Timeout.Infinite : light ? Math.Max(5000, s.IntervalMs) : s.IntervalMs;
                    if (active && s.BatterySaver && OnBattery()) wait = wait * 5 / 2;
                    if (active && failures > 0) wait = Math.Max(wait, 15000);
                    if (warmup > 0) { warmup--; wait = 0; } // demo: fill the history immediately
                    _wake.WaitOne(wait);
                }
            }
            finally { try { source?.Dispose(); } catch { } }
        }

        /// <summary>First-run setup on the worker thread, before any sensor is opened.</summary>
        private void RunSetup()
        {
            if (Program.IsElevated && !DriverSetup.IsInstalled())
            {
                Post(new List<Row> { new Row { Kind = RowKind.Value, Label = "Installing sensor driver\u2026", Level = Level.Dim } }, false);
                var result = DriverSetup.Install(out int code);
                var notice = NoticeFor(result);
                if (notice != null && IsHandleCreated)
                    try { BeginInvoke((Action)(() => { _setupNotice = notice; Relayout(); })); } catch (InvalidOperationException) { }
            }
            if (Program.SetupAutoStart) PostStartupNotice(AutoStart.Set(true));
        }

        private void PostStartupNotice(string problem)
        {
            if (!IsHandleCreated || _exiting) return;
            try { BeginInvoke((Action)(() =>
            {
                _startupNotice = problem == null ? null : new Row { Kind = RowKind.Notice, Action = NoticeAction.StartupSafety,
                    Label = "Review startup settings - click", Detail = problem };
                Relayout();
            })); } catch (InvalidOperationException) { }
        }

        private static Row NoticeFor(DriverResult result)
        {
            switch (result)
            {
                case DriverResult.Installed: return null;
                case DriverResult.RestartNeeded:
                    return new Row { Kind = RowKind.Notice, Action = NoticeAction.Dismiss, Label = "Restart Windows to finish sensor setup" };
                default:
                    return new Row { Kind = RowKind.Notice, Action = NoticeAction.InstallDriver, Label = "Driver setup failed \u00B7 click to retry" };
            }
        }

        /// <summary>
        /// Waits until every library next to the exe can be opened. Antivirus briefly locks freshly
        /// installed files, and .NET remembers a failed load for the rest of the run, so it is
        /// worth waiting a little before the first attempt.
        /// </summary>
        private void WaitForLibraries()
        {
            try
            {
                string dir = System.IO.Path.GetDirectoryName(Application.ExecutablePath);
                var deadline = DateTime.UtcNow.AddSeconds(45);
                bool told = false;
                while (!_exiting && DateTime.UtcNow < deadline)
                {
                    bool locked = false;
                    foreach (string file in System.IO.Directory.GetFiles(dir, "*.dll"))
                    {
                        try
                        {
                            using (System.IO.File.Open(file, System.IO.FileMode.Open, System.IO.FileAccess.Read,
                                                       System.IO.FileShare.ReadWrite | System.IO.FileShare.Delete)) { }
                        }
                        catch (UnauthorizedAccessException) { locked = true; break; }
                        catch (System.IO.IOException) { locked = true; break; }
                    }
                    if (!locked) return;
                    if (!told)
                    {
                        Post(new List<Row> { new Row { Kind = RowKind.Value, Label = "Waiting for Windows security scan\u2026", Level = Level.Dim } }, false);
                        told = true;
                    }
                    Thread.Sleep(750);
                }
            }
            catch { }
        }

        private static bool OnBattery()
        {
            try { return SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Offline; } catch { return false; }
        }

        private void Post(List<Row> rows, bool light)
        {
            if (!IsHandleCreated || _exiting) return;
            try { BeginInvoke((Action)(() => OnReadings(rows, light))); } catch (InvalidOperationException) { }
        }

        public void RequestReport(Action<string> done)
        {
            _reportCallback = done;
            _wake.Set();
        }

        // ---------- readings (UI thread) ----------

        private void OnReadings(List<Row> rows, bool light)
        {
            foreach (var r in rows)
            {
                if (r.Kind != RowKind.Temp || r.Key == null || !r.HasValue) continue;
                if (!_history.TryGetValue(r.Key, out var h)) _history[r.Key] = h = new History();
                h.Add(r.Value);
            }

            var messages = _alerts.Check(rows, _settings, FormatTemp);
            if (messages.Count > 0)
                try { _tray.ShowBalloonTip(8000, "TempWidget", string.Join("\n", messages), ToolTipIcon.Warning); } catch { }

            UpdateTray(rows);
            if (light) return; // widget is hidden: keep the last full set for when it comes back
            _lastRows = rows;
            Relayout();
        }

        private void UpdateTray(List<Row> rows)
        {
            var cpu = rows.FirstOrDefault(r => r.Kind == RowKind.Temp && r.Section == Section.Cpu && r.Key != null && r.Key.EndsWith(".temp"));
            var gpu = rows.FirstOrDefault(r => r.Kind == RowKind.Temp && r.Section == Section.Gpu && r.Key != null && r.Key.EndsWith(".temp"));
            string text = "TempWidget";
            if (cpu != null) text += " · CPU " + FormatTemp(cpu.Value);
            if (gpu != null) text += " · GPU " + FormatTemp(gpu.Value);
            try { _tray.Text = text.Length > 63 ? text.Substring(0, 63) : text; } catch { }

            int mode = _settings.TrayMode;
            Row shown = ReadingPresentation.TrayTemperature(cpu, gpu, mode);
            if (shown != null && shown.HasValue) ShowTrayTemp(shown.Value, shown.TempLevel);
            else ShowStaticTray();
        }

        private void ShowStaticTray()
        {
            if (_trayShown == int.MinValue) return;
            _trayShown = int.MinValue;
            _tray.Icon = _appIcon;
            Native.FreeIcon(_trayHandle);
            _trayHandle = IntPtr.Zero;
        }

        private void ShowTrayTemp(float celsius, Level level)
        {
            int shown = (int)Math.Round(_settings.Fahrenheit ? celsius * 9 / 5 + 32 : celsius);
            if (shown == _trayShown && level == _trayLevel) return;
            try
            {
                using (var bmp = RenderTrayBitmap(shown, level))
                {
                    IntPtr handle = bmp.GetHicon();
                    _tray.Icon = Icon.FromHandle(handle);
                    Native.FreeIcon(_trayHandle); // the previous one, now unused
                    _trayHandle = handle;
                }
                _trayShown = shown;
                _trayLevel = level;
            }
            catch { }
        }

        internal static Bitmap RenderTrayBitmap(int shown, Level level)
        {
            var bmp = new Bitmap(32, 32);
            using (var g = Graphics.FromImage(bmp))
            using (var bg = new SolidBrush(Color.FromArgb(24, 26, 34)))
            using (var fg = new SolidBrush(level == Level.Hot ? Theme.Dark.Hot : level == Level.Warm ? Theme.Dark.Warm : Color.White))
            using (var font = new Font("Segoe UI", shown > 99 || shown < -9 ? 15f : 21f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                using (var path = RoundedRect(new Rectangle(0, 0, 31, 31), 7)) g.FillPath(bg, path);
                g.DrawString(shown.ToString(), font, fg, new RectangleF(0, 1, 32, 32), fmt);
            }
            return bmp;
        }

        private static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            int d = radius * 2;
            var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        private string FormatTemp(float celsius) => ReadingPresentation.Temperature(celsius, _settings.Fahrenheit);

        // ---------- layout ----------

        private int RowHeight => S(19);
        private int HeaderGap => S(7);
        private int Pad => S(10);

        private void RebuildFonts()
        {
            _scale = _dpi / 96f * _settings.ScalePercent / 100f;
            _font?.Dispose();
            _headerFont?.Dispose();
            _font = new Font("Segoe UI", 12f * _scale, GraphicsUnit.Pixel);
            _headerFont = new Font("Segoe UI Semibold", 10.7f * _scale, GraphicsUnit.Pixel);
        }

        /// <summary>Applies the display settings to the latest readings and resizes the window to fit.</summary>
        private void Relayout()
        {
            var s = _settings;
            var rows = new List<Row>();
            if (_setupNotice != null) rows.Add(_setupNotice);
            if (_startupNotice != null) rows.Add(_startupNotice);
            foreach (var r in _lastRows)
            {
                // The setup result already says what is wrong; don't repeat it with the generic hint.
                if (_setupNotice != null && r.Kind == RowKind.Notice && r.Action == NoticeAction.InstallDriver) continue;
                if (r.Kind == RowKind.Notice)
                {
                    bool error = r.Action == NoticeAction.FileBlocked || r.Action == NoticeAction.ShowLog;
                    if (s.ShowHints || error) rows.Add(r); // errors are never hidden
                    continue;
                }
                bool status = r.Key == null && r.Kind != RowKind.Header; // e.g. "Starting sensors..."
                if (!status)
                {
                    if (!s.SectionEnabled(r.Section)) continue;
                    if (s.Compact && (r.Kind == RowKind.Header || r.Compact == null)) continue;
                }
                rows.Add(r);
            }
            if (rows.Count == 0)
                rows.Add(new Row { Kind = RowKind.Value, Label = "Nothing to show · right-click", Level = Level.Dim });
            _visible = rows;

            int h = Pad;
            for (int i = 0; i < rows.Count; i++)
                h += RowHeight + (rows[i].Kind == RowKind.Header && i > 0 ? HeaderGap : 0);
            h += Pad - S(2);
            int w = S(s.Compact ? 176 : 248);

            if (w != Width || h != Height)
            {
                // Keep a snapped right/bottom edge where it is while the size changes.
                int right = Right, bottom = Bottom;
                var b = new Rectangle(Left, Top, w, h);
                if (s.AnchorRight) b.X = right - w;
                if (s.AnchorBottom) b.Y = bottom - h;
                Bounds = b;
            }
            Invalidate();
        }

        private void PlaceOnScreen()
        {
            var pt = new Point(_settings.X, _settings.Y);
            bool onScreen = Screen.AllScreens.Any(sc => sc.WorkingArea.Contains(new Point(pt.X + S(20), pt.Y + S(20))));
            if (!onScreen)
            {
                var wa = Screen.PrimaryScreen.WorkingArea;
                pt = new Point(wa.Right - Width - S(16), wa.Top + S(16));
            }
            Location = pt;
        }

        private void SnapAndSave()
        {
            var s = _settings;
            if (s.SnapEdges)
            {
                var wa = Screen.FromControl(this).WorkingArea;
                int margin = S(8), reach = S(18);
                int x = Left, y = Top;
                bool right = false, bottom = false;
                if (Math.Abs(Left - wa.Left) <= reach) x = wa.Left + margin;
                else if (Math.Abs(wa.Right - Right) <= reach) { x = wa.Right - margin - Width; right = true; }
                if (Math.Abs(Top - wa.Top) <= reach) y = wa.Top + margin;
                else if (Math.Abs(wa.Bottom - Bottom) <= reach) { y = wa.Bottom - margin - Height; bottom = true; }
                Location = new Point(x, y);
                s.AnchorRight = right;
                s.AnchorBottom = bottom;
            }
            else s.AnchorRight = s.AnchorBottom = false;
            s.X = Left;
            s.Y = Top;
            s.Save();
        }

        // ---------- painting ----------

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var s = _settings;
            var t = _theme;
            var rows = _visible;
            int y = Pad, left = Pad, right = Width - Pad;
            const TextFormatFlags L = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
            const TextFormatFlags R = TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;

            _rowTops.Clear();
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                if (r.Kind == RowKind.Header)
                {
                    if (i > 0)
                    {
                        y += HeaderGap;
                        using (var p = new Pen(t.Line)) g.DrawLine(p, left, y - HeaderGap / 2, right, y - HeaderGap / 2);
                    }
                    _rowTops.Add(y);
                    var rect = new Rectangle(left, y, right - left, RowHeight);
                    TextRenderer.DrawText(g, r.Label.ToUpperInvariant(), _headerFont, rect, t.Accent, L);
                    TextRenderer.DrawText(g, r.Text, _headerFont, rect, t.Dim, R);
                    y += RowHeight;
                    continue;
                }

                _rowTops.Add(y);
                if (r.Kind == RowKind.Notice)
                {
                    using (var b = new SolidBrush(t.NoticeBg)) g.FillRectangle(b, left - S(5), y + S(1), right - left + S(10), RowHeight - S(2));
                    TextRenderer.DrawText(g, r.Label, _font, new Rectangle(left, y, right - left, RowHeight), t.Warm, L);
                    y += RowHeight;
                    continue;
                }

                string label = s.Compact && r.Compact != null ? r.Compact : r.Label;
                string value = r.Kind == RowKind.Temp ? FormatTemp(r.Value) : r.Text;
                Color color = t.For(r.Kind == RowKind.Temp ? r.TempLevel : r.Level);
                bool noValue = string.IsNullOrEmpty(value);
                int valueW = noValue ? 0 : S(s.Compact ? 84 : r.Kind == RowKind.Temp ? 58 : 92);
                int labelW = right - left - valueW;

                if (!s.Compact && r.Bar >= 0)
                {
                    labelW = S(76);
                    int bx = left + S(78), bw = right - valueW - bx - S(6), bh = S(4), by = y + (RowHeight - bh) / 2;
                    if (bw > S(10))
                    {
                        using (var b = new SolidBrush(t.BarBg)) g.FillRectangle(b, bx, by, bw, bh);
                        using (var b = new SolidBrush(color)) g.FillRectangle(b, bx, by, (int)(bw * Math.Max(0, Math.Min(1, r.Bar))), bh);
                    }
                }
                else if (!s.Compact && r.Spark && s.Sparklines && r.Key != null && _history.TryGetValue(r.Key, out var hist) && hist.Count > 1)
                {
                    labelW = S(102);
                    var area = new Rectangle(left + S(106), y + S(4), right - valueW - S(8) - (left + S(106)), RowHeight - S(8));
                    if (area.Width > S(16)) DrawSpark(g, hist, area, color);
                }

                TextRenderer.DrawText(g, label, _font, new Rectangle(left, y, labelW, RowHeight), noValue && r.Level == Level.Dim ? t.Dim : noValue ? color : t.Label, L);
                if (!noValue) TextRenderer.DrawText(g, value, _font, new Rectangle(right - valueW, y, valueW, RowHeight), color, R);
                y += RowHeight;
            }
        }

        private void DrawSpark(Graphics g, History h, Rectangle area, Color color)
        {
            int n = h.Count;
            float lo = float.MaxValue, hi = float.MinValue;
            for (int i = 0; i < n; i++) { lo = Math.Min(lo, h[i]); hi = Math.Max(hi, h[i]); }
            // At least a 12 degree window, so idle jitter stays a flat line instead of filling the height.
            float span = Math.Max(12f, hi - lo), bottom = (hi + lo) / 2 - span / 2;
            float step = area.Width / (float)(History.Capacity - 1);
            var pts = new PointF[n];
            for (int i = 0; i < n; i++)
                pts[i] = new PointF(area.Right - (n - 1 - i) * step, area.Bottom - (h[i] - bottom) / span * area.Height);

            var old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var pen = new Pen(Color.FromArgb(190, color), Math.Max(1f, 1.2f * _scale))) g.DrawLines(pen, pts);
            g.SmoothingMode = old;
        }

        // ---------- mouse ----------

        private int RowAt(int y)
        {
            for (int i = _rowTops.Count - 1; i >= 0; i--)
                if (y >= _rowTops[i]) return y < _rowTops[i] + RowHeight && i < _visible.Count ? i : -1;
            return -1;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            int i = RowAt(e.Y);
            if (i >= 0 && _visible[i].Kind == RowKind.Notice) { RunNotice(_visible[i].Action); return; }
            if (_settings.Locked) return;
            Native.BeginDrag(Handle); // returns when the drag ends
            SnapAndSave();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int i = RowAt(e.Y);
            if (i == _hoverRow) return;
            _hoverRow = i;
            var r = i >= 0 ? _visible[i] : null;
            Cursor = r != null && r.Kind == RowKind.Notice && r.Action != NoticeAction.None ? Cursors.Hand : Cursors.Default;

            string tip = null;
            if (r != null && r.Kind == RowKind.Temp && r.Key != null && _history.TryGetValue(r.Key, out var h) && h.Count > 0 && h.Min <= h.Max)
                tip = $"{r.Label} since {_rangeSince:HH:mm}: lowest {FormatTemp(h.Min)}, average {FormatTemp(h.Average)}, highest {FormatTemp(h.Max)}";
            else if (r != null && r.Kind == RowKind.Notice)
                tip = r.Action == NoticeAction.Elevate ? "CPU temperature, drive temperatures and some fans need administrator rights."
                    : r.Action == NoticeAction.InstallDriver ? "PawnIO is the signed driver used to read CPU temperature, power and clock. Click to install it."
                    : r.Action == NoticeAction.Dismiss ? "Click to hide this message."
                    : r.Action == NoticeAction.FileBlocked ? "Windows refused to open one of TempWidget's files. Click for what to do."
                    : r.Action == NoticeAction.ShowLog ? "Click to open the error log." : null;
            if (r != null && r.Detail != null) tip = tip == null ? r.Detail : tip + "\n" + r.Detail;
            _tip.SetToolTip(this, tip);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hoverRow = -1;
        }

        private void RunNotice(NoticeAction action)
        {
            if (action == NoticeAction.Elevate) EnableFullSensors();
            else if (action == NoticeAction.StartupSafety)
                MessageBox.Show(this, _startupNotice?.Detail ?? AutoStart.LegacyWarning, "Startup safety", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            else if (action == NoticeAction.Dismiss) { _setupNotice = null; Relayout(); }
            else if (action == NoticeAction.InstallDriver) InstallDriver();
            else if (action == NoticeAction.ShowLog) OpenUrl(System.IO.Path.Combine(Settings.Dir, "error.log"));
            else if (action == NoticeAction.FileBlocked)
            {
                var answer = MessageBox.Show(
                    "Windows would not let TempWidget open one of its own program files. This usually happens when antivirus is " +
                    "still scanning a freshly installed copy, and a restart of TempWidget fixes it.\n\n" +
                    "If it keeps happening, open Windows Security > Protection history and allow the blocked TempWidget file.\n\n" +
                    "Restart TempWidget now?", "TempWidget", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (answer == DialogResult.Yes && Program.Restart()) ExitApp();
            }
        }

        /// <summary>Installs the bundled sensor driver on request, then restarts so the sensors pick it up.</summary>
        private void InstallDriver()
        {
            if (!Program.IsElevated) { EnableFullSensors(); return; }
            if (MessageBox.Show("CPU temperature, power and clock speed are read through PawnIO, a free, open-source, signed driver.\n\n" +
                                "Install PawnIO " + DriverSetup.BundledVersion + " now? Nothing is downloaded; the installer is part of TempWidget.",
                                "TempWidget", MessageBoxButtons.OKCancel, MessageBoxIcon.Information) != DialogResult.OK) return;

            _setupNotice = new Row { Kind = RowKind.Notice, Action = NoticeAction.None, Label = "Installing sensor driver\u2026" };
            Relayout();
            ThreadPool.QueueUserWorkItem(_ =>
            {
                var result = DriverSetup.Install(out int code);
                if (!IsHandleCreated || _exiting) return;
                try { BeginInvoke((Action)(() => DriverInstalled(result, code))); } catch (InvalidOperationException) { }
            });
        }

        private void DriverInstalled(DriverResult result, int code)
        {
            _setupNotice = NoticeFor(result);
            Relayout();
            if (result == DriverResult.Installed)
            {
                SavePosition();
                if (Program.Restart()) ExitApp(); // the sensor library only looks for the driver at start
                return;
            }
            if (result == DriverResult.RestartNeeded) return;

            string why = result == DriverResult.FileMissing ? "The installer file PawnIO_setup.exe is missing from the TempWidget folder."
                       : result == DriverResult.FileChanged ? "The installer file PawnIO_setup.exe is not the official one, so it was not run."
                       : "The PawnIO installer reported error " + code + ". Restarting Windows and trying again often helps.";
            if (MessageBox.Show(why + "\n\nOpen the PawnIO website to install it by hand?", "TempWidget",
                                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                OpenUrl("https://pawnio.eu");
        }

        internal static void OpenUrl(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
        }

        /// <summary>Explicitly requests administrator rights for this session.</summary>
        public void EnableFullSensors()
        {
            if (Program.IsElevated) return;
            _settings.PreferElevated = true;
            SavePosition();
            // Also install the driver in the elevated copy if this PC does not have it yet.
            if (Program.RelaunchElevated(DriverSetup.IsInstalled() ? null : "--setup")) ExitApp();
        }

        // ---------- window behaviour ----------

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x80; // WS_EX_TOOLWINDOW: keep out of Alt+Tab
                if (_settings != null && _settings.ClickThrough) cp.ExStyle |= 0x20; // WS_EX_TRANSPARENT: clicks pass through
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.RoundCorners(Handle);
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == Native.WM_DPICHANGED)
            {
                // Moved to a monitor with a different scale: redo fonts and size for it.
                int dpi = (int)((long)m.WParam & 0xFFFF);
                if (dpi > 0 && dpi != _dpi) { _dpi = dpi; RebuildFonts(); Relayout(); }
            }
            else if (m.Msg == Native.WM_SETTINGCHANGE && _settings.Theme == 2)
            {
                var t = Theme.Resolve(2);
                if (t != _theme) { _theme = t; BackColor = t.Bg; Invalidate(); }
            }
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            _shown = Visible;
            _wake.Set();
        }

        private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            // No point reading sensors while nobody can see them.
            if (e.Reason == SessionSwitchReason.SessionLock) _sessionLocked = true;
            else if (e.Reason == SessionSwitchReason.SessionUnlock) { _sessionLocked = false; _wake.Set(); }
        }

        private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            // Driver handles can go stale across sleep; reopen the sensors on wake.
            if (e.Mode == PowerModes.Resume) { _rebuild = true; _wake.Set(); }
        }

        private void ToggleVisible()
        {
            if (Visible) Hide();
            else { Show(); if (_settings.TopMost) { TopMost = false; TopMost = true; } }
        }

        private void SavePosition()
        {
            _settings.X = Left;
            _settings.Y = Top;
            _settings.Save();
        }

        /// <summary>Replaces the settings and applies every visible consequence.</summary>
        public void ApplySettings(Settings n)
        {
            var old = _settings;
            n.X = Left;
            n.Y = Top;
            _settings = n;
            n.Save();

            TopMost = n.TopMost;
            double opacity = EffectiveOpacity(n);
            if (Math.Abs(Opacity - opacity) > 0.001) Opacity = opacity;
            _theme = Theme.Resolve(n.Theme);
            BackColor = _theme.Bg;
            if (n.ClickThrough != old.ClickThrough) UpdateStyles(); // re-reads CreateParams
            if (n.ScalePercent != old.ScalePercent) RebuildFonts();
            if (n.SensorsDiffer(old)) _rebuild = true;
            if (n.Fahrenheit != old.Fahrenheit) _trayShown = int.MinValue + 1; // force the tray number to redraw
            if (n.TrayMode != old.TrayMode) { ShowStaticTray(); _trayShown = int.MinValue; }
            Relayout();
            _wake.Set();
        }

        // Click-through only works on a layered window, which WinForms creates for any opacity below 100%.
        private static double EffectiveOpacity(Settings s) => s.ClickThrough ? Math.Min(s.Opacity, 0.99) : s.Opacity;

        private void Change(Action<Settings> edit)
        {
            var n = _settings.Clone();
            edit(n);
            ApplySettings(n);
        }

        public void ShowSettings()
        {
            if (_settingsForm == null || _settingsForm.IsDisposed) _settingsForm = new SettingsForm(this) { Icon = _appIcon };
            _settingsForm.Show();
            _settingsForm.Activate();
        }

        public void ResetRange()
        {
            foreach (var h in _history.Values) h.ResetRange();
            _rangeSince = DateTime.Now;
        }

        // ---------- menu ----------

        private ContextMenuStrip BuildMenu()
        {
            var m = new ContextMenuStrip();
            var show = new ToolStripMenuItem("Hide widget", null, (s, e) => ToggleVisible());
            var settings = new ToolStripMenuItem("Settings…", null, (s, e) => ShowSettings());
            var compact = new ToolStripMenuItem("Compact mode", null, (s, e) => Change(n => n.Compact = !n.Compact));
            var top = new ToolStripMenuItem("Always on top", null, (s, e) => Change(n => n.TopMost = !n.TopMost));
            var locked = new ToolStripMenuItem("Lock position", null, (s, e) => Change(n => n.Locked = !n.Locked));
            var through = new ToolStripMenuItem("Click-through", null, (s, e) => Change(n => n.ClickThrough = !n.ClickThrough))
            { ToolTipText = "Mouse clicks pass through the widget. Turn it off again from the tray icon." };
            var reset = new ToolStripMenuItem("Reset lowest / highest", null, (s, e) => ResetRange());
            var elevate = new ToolStripMenuItem("Enable full sensors (administrator)…", null, (s, e) => EnableFullSensors());
            var exit = new ToolStripMenuItem("Exit", null, (s, e) => ExitApp());

            m.Opening += (s, e) =>
            {
                var c = _settings;
                show.Text = Visible ? (c.Background ? "Hide widget" : "Hide widget (pauses sensors)") : "Show widget";
                compact.Checked = c.Compact;
                top.Checked = c.TopMost;
                locked.Checked = c.Locked;
                through.Checked = c.ClickThrough;
                elevate.Visible = !Program.IsElevated && !Program.Demo;
            };

            m.Items.AddRange(new ToolStripItem[]
            {
                show, settings, new ToolStripSeparator(), compact, top, locked, through, reset,
                new ToolStripSeparator(), elevate, exit,
            });
            return m;
        }

        private static Icon LoadAppIcon()
        {
            try
            {
                var icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (icon != null) return icon;
            }
            catch { }
            return SystemIcons.Application;
        }

        public void ExitApp()
        {
            _exiting = true;
            Close();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _exiting = true;
            try
            {
                SystemEvents.SessionSwitch -= OnSessionSwitch;
                SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            }
            catch { }
            if (!Program.Demo) SavePosition();
            _tray.Visible = false;
            _wake.Set();
            _worker.Join(3000); // let the worker close the driver cleanly
            Native.FreeIcon(_trayHandle);
            base.OnFormClosing(e);
        }

        // ---------- demo screenshots (--demo --shot <file.png>) ----------

        private void ScheduleScreenshots(string path)
        {
            var timer = new System.Windows.Forms.Timer { Interval = 1500 };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                try
                {
                    Snapshot(this, path);
                    using (var a = RenderTrayBitmap(68, Level.Good)) a.Save(path + ".tray68.png");
                    using (var a = RenderTrayBitmap(104, Level.Hot)) a.Save(path + ".tray104.png");
                    string stem = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path) ?? "", System.IO.Path.GetFileNameWithoutExtension(path));
                    if (Program.Args.Contains("--shot-settings"))
                    {
                        ShowSettings();
                        for (int tab = 0; tab < _settingsForm.TabCount; tab++)
                        {
                            _settingsForm.SelectTab(tab);
                            Application.DoEvents();
                            Thread.Sleep(300);
                            Application.DoEvents();
                            Snapshot(_settingsForm, stem + "-settings" + tab + ".png");
                        }
                        _settingsForm.Close();
                    }
                }
                catch (Exception ex) { Program.Log(ex); }
                ExitApp();
            };
            timer.Start();
        }

        private static void Snapshot(Form f, string path)
        {
            f.Refresh();
            Application.DoEvents();
            using (var bmp = new Bitmap(f.Width, f.Height))
            {
                try { using (var g = Graphics.FromImage(bmp)) g.CopyFromScreen(f.Location, Point.Empty, f.Size); }
                catch { f.DrawToBitmap(bmp, new Rectangle(Point.Empty, f.Size)); }
                bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            }
        }
    }
}
