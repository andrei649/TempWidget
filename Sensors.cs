using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using LibreHardwareMonitor.Hardware;

namespace TempWidget
{
    /// <summary>
    /// Real sensors via LibreHardwareMonitor. Only the hardware groups for visible sections are
    /// opened, and slow groups (drive SMART, memory modules, battery) are polled less often.
    /// Nothing here is specific to one machine except the verified-board list for EC fans.
    /// </summary>
    internal sealed class LhmSource : ISensorSource
    {
        private readonly Computer _computer;
        private readonly Settings _s;
        private readonly EcFans _ecFans;
        private readonly PollSchedule _schedule = new PollSchedule();
        private string _netAdapter; // identifier of the network adapter being shown

        public LhmSource(Settings s)
        {
            _s = s;
            // Without administrator rights some device classes may refuse to open. Rather than
            // showing nothing, fall back to the groups that work unelevated.
            try { _computer = Open(s, s.ShowDrives, s.ShowFans); }
            catch (Exception ex)
            {
                Program.Log(ex);
                _computer = Open(s, false, false);
            }

            // Direct EC reads only on laptops where the register layout has been verified,
            // unless the user explicitly opted in. Unknown EC layouts must not be probed automatically.
            int verifiedFans = VerifiedEcFanCount();
            int ecFanCount = s.EcMode == 1 ? 3 : (s.EcMode == 0 ? verifiedFans : 0);
            if (s.ShowFans && ecFanCount > 0 && Program.IsElevated && PawnIoInstalled())
                try { _ecFans = new EcFans(ecFanCount); } catch { _ecFans = null; }
        }

        private static Computer Open(Settings s, bool storage, bool boards)
        {
            var c = new Computer
            {
                IsCpuEnabled = s.ShowCpu,
                IsGpuEnabled = s.ShowGpu,
                IsMemoryEnabled = s.ShowRam,
                IsMotherboardEnabled = boards, // SuperIO / embedded controller: where fan sensors live
                IsControllerEnabled = boards,
                IsStorageEnabled = storage,
                IsBatteryEnabled = s.ShowBattery,
                IsNetworkEnabled = s.ShowNetwork,
                IsPsuEnabled = false,
                IsPowerMonitorEnabled = false,
            };
            try { c.Open(); }
            catch { try { c.Close(); } catch { } throw; }
            return c;
        }

        internal static bool PawnIoInstalled()
        {
            try { return LibreHardwareMonitor.PawnIo.PawnIo.IsInstalled; } catch { return false; }
        }

        /// <summary>Fan count for laptops whose Clevo-layout fan registers were confirmed against real hardware; 0 if unverified.</summary>
        private int VerifiedEcFanCount()
        {
            try
            {
                var sys = _computer.SMBios?.System;
                if (sys == null) return 0;
                // System76 Bonobo WS (bonw16 / Clevo X580) has two physical fans (CPU 0xD0, GPU 0xD2);
                // 0xD4/0xD5 is unpopulated and can hold unrelated EC bytes (e.g. 0x00F0 -> 8984 RPM).
                if (Eq(sys.ManufacturerName, "System76") && Eq(sys.Version, "bonw16")) return 2;
                return 0;
            }
            catch { return 0; }
        }

        private static bool Eq(string a, string b) => string.Equals(a?.Trim(), b, StringComparison.OrdinalIgnoreCase);

        public List<Row> Poll(bool light)
        {
            _schedule.Take(light, out bool slow, out bool medium);

            foreach (IHardware hw in _computer.Hardware)
            {
                var t = hw.HardwareType;
                if (light && t != HardwareType.Cpu && !IsGpu(t)) continue;
                if (t == HardwareType.Storage && !slow) continue;
                if (t == HardwareType.Battery && !medium) continue;
                // Only the adapter on display is read every time; the rest just often enough to notice a switch.
                if (t == HardwareType.Network && !slow && hw.Identifier.ToString() != _netAdapter) continue;
                if (t == HardwareType.Memory)
                {
                    string id = hw.Identifier.ToString();
                    if (id.StartsWith("/vram")) continue;                 // page-file commit, not shown
                    if (id.StartsWith("/memory/dimm") && !slow) continue; // DIMM sensors over SMBus
                }
                try { UpdateDeep(hw); } catch { /* one misbehaving device must not blank the widget */ }
            }

            var rows = new List<Row>();
            var hws = _computer.Hardware;

            if (!Program.IsElevated)
                rows.Add(new Row { Key = "notice.elevate", Section = Section.Notice, Kind = RowKind.Notice, Action = NoticeAction.Elevate,
                                   Label = "Limited mode · click for all sensors" });
            else if (_s.ShowCpu && !PawnIoInstalled())
                rows.Add(new Row { Key = "notice.pawnio", Section = Section.Notice, Kind = RowKind.Notice, Action = NoticeAction.InstallDriver,
                                   Label = "CPU temperature needs a driver · click" });

            var cpus = hws.Where(h => h.HardwareType == HardwareType.Cpu).ToList();
            for (int i = 0; i < cpus.Count; i++) AddCpu(rows, cpus[i], cpus.Count > 1 ? i : -1, _s.Fahrenheit);

            var gpus = hws.Where(h => IsGpu(h.HardwareType) && h.Sensors.Any(x => x.Value.HasValue)).ToList();
            for (int i = 0; i < gpus.Count; i++) AddGpu(rows, gpus[i], i, gpus.Count > 1);

            if (light) return rows;

            AddMemory(rows, hws.Where(h => h.HardwareType == HardwareType.Memory).ToList());
            if (_s.ShowFans) AddFans(rows, hws, _ecFans?.Read());
            AddStorage(rows, hws.Where(h => h.HardwareType == HardwareType.Storage).ToList());
            AddNetwork(rows, hws.Where(h => h.HardwareType == HardwareType.Network).ToList(), slow);
            foreach (var bat in hws.Where(h => h.HardwareType == HardwareType.Battery).Take(1))
                AddBattery(rows, bat);
            return rows;
        }

        private static bool IsGpu(HardwareType t) =>
            t == HardwareType.GpuNvidia || t == HardwareType.GpuAmd || t == HardwareType.GpuIntel;

        private static void UpdateDeep(IHardware hw)
        {
            hw.Update();
            foreach (var sub in hw.SubHardware) UpdateDeep(sub);
        }

        private static IEnumerable<ISensor> AllSensors(IHardware hw) =>
            hw.Sensors.Concat(hw.SubHardware.SelectMany(AllSensors));

        private static ISensor Find(IHardware hw, SensorType type, params string[] names)
        {
            foreach (var n in names)
            {
                var s = hw.Sensors.FirstOrDefault(x => x.SensorType == type && x.Value.HasValue &&
                                                       x.Name.Equals(n, StringComparison.OrdinalIgnoreCase));
                if (s != null) return s;
            }
            return null;
        }

        private static ISensor Real(ISensor s) => s != null && s.Value > 0 && !float.IsInfinity(s.Value.Value) ? s : null;

        private static ISensor CpuTemperature(IHardware cpu, params string[] names)
        {
            foreach (var name in names)
            {
                var sensor = cpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature &&
                    s.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && Real(s) != null);
                if (sensor != null) return sensor;
            }
            return null;
        }

        // ---- sections ----

        internal static void AddCpu(List<Row> rows, IHardware cpu, int index, bool fahrenheit)
        {
            string k = index < 0 ? "cpu" : "cpu" + index;
            var header = Header(Section.Cpu, index < 0 ? "CPU" : "CPU " + (index + 1), Shorten(cpu.Name));
            header.Detail = CoreDetail(cpu, fahrenheit);
            rows.Add(header);

            // Intel: "CPU Package" equals the hottest core, "Core Average" is the steady one.
            // AMD: "Core (Tctl/Tdie)" is the single control temperature.
            // A reading of 0 means the driver could not read it (no admin rights / no PawnIO).
            var package = CpuTemperature(cpu, "CPU Package", "Core (Tctl/Tdie)", "Core (Tctl)", "Core (Tdie)", "Package");
            var coreMax = CpuTemperature(cpu, "Core Max");
            var avg = CpuTemperature(cpu, "Core Average");
            var cores = cpu.Sensors.Where(s => s.SensorType == SensorType.Temperature && s.Value.HasValue &&
                                               s.Value > 0 && !float.IsInfinity(s.Value.Value) && s.Name.Contains("Core #") && !s.Name.Contains("Distance"))
                                   .Select(s => s.Value.Value).ToList();

            float? hot = coreMax?.Value ?? (cores.Count > 0 ? cores.Max() : (float?)null);
            if (package != null && (!hot.HasValue || package.Value > hot)) hot = package.Value;

            if (avg != null && hot.HasValue)
            {
                rows.Add(Temp(Section.Cpu, k + ".avg", "Temp (average)", avg.Value.Value, 75, 90, spark: true));
                rows.Add(Temp(Section.Cpu, k + ".temp", "Temp (hottest)", hot.Value, 85, 100, spark: true, compact: "CPU"));
            }
            else if (hot.HasValue)
                rows.Add(Temp(Section.Cpu, k + ".temp", "Temperature", hot.Value, 80, 95, spark: true, compact: "CPU"));
            else if (avg != null)
                rows.Add(Temp(Section.Cpu, k + ".temp", "Temperature (average)", avg.Value.Value, 75, 90, spark: true, compact: "CPU"));
            else
            {
                // Keep the same identity in full and compact views. NaN is an unavailable
                // reading, never a fabricated temperature or a sample for history/alerts.
                var unavailable = Temp(Section.Cpu, k + ".temp", "Temperature", float.NaN, 80, 95, compact: "CPU");
                unavailable.Detail = "CPU temperature unavailable. The sensor may need administrator rights and PawnIO, or may not be supported.";
                rows.Add(unavailable);
            }

            var load = Find(cpu, SensorType.Load, "CPU Total");
            if (load != null) rows.Add(Load(Section.Cpu, k + ".load", "Load", load.Value.Value, "CPU load"));

            var power = Find(cpu, SensorType.Power, "CPU Package", "Package");
            if (power != null && power.Value > 0.1f) rows.Add(Num(Value(Section.Cpu, k + ".power", "Power", $"{power.Value.Value:0.0} W"), power.Value.Value, "W"));

            var clocks = cpu.Sensors.Where(s => s.SensorType == SensorType.Clock && s.Value > 0 && s.Name.Contains("Core #"))
                                    .Select(s => s.Value.Value).ToList();
            if (clocks.Count > 0) rows.Add(Num(Value(Section.Cpu, k + ".clock", "Clock (max)", $"{clocks.Max() / 1000f:0.00} GHz"), clocks.Max() / 1000f, "GHz"));
        }

        private static void AddGpu(List<Row> rows, IHardware gpu, int index, bool several)
        {
            string k = "gpu" + index;
            rows.Add(Header(Section.Gpu, several ? "GPU " + (index + 1) : "GPU", Shorten(gpu.Name)));

            var core = Find(gpu, SensorType.Temperature, "GPU Core");
            var hot = Find(gpu, SensorType.Temperature, "GPU Hot Spot");
            var mem = Find(gpu, SensorType.Temperature, "GPU Memory Junction", "GPU Memory");
            if (core != null) rows.Add(Temp(Section.Gpu, k + ".temp", "Temperature", core.Value.Value, 75, 87, spark: true, compact: several ? "GPU " + (index + 1) : "GPU"));
            if (hot != null) rows.Add(Temp(Section.Gpu, k + ".hotspot", "Hot spot", hot.Value.Value, 85, 100));
            if (mem != null) rows.Add(Temp(Section.Gpu, k + ".memtemp", "Memory", mem.Value.Value, 85, 100));

            var load = Find(gpu, SensorType.Load, "GPU Core", "D3D 3D");
            if (load != null) rows.Add(Load(Section.Gpu, k + ".load", "Load", load.Value.Value, several ? null : "GPU load"));

            var power = Find(gpu, SensorType.Power, "GPU Package", "GPU Power", "GPU Core");
            if (power != null && power.Value > 0.1f) rows.Add(Num(Value(Section.Gpu, k + ".power", "Power", $"{power.Value.Value:0.0} W"), power.Value.Value, "W"));

            var used = Find(gpu, SensorType.SmallData, "GPU Memory Used", "D3D Dedicated Memory Used");
            var total = Find(gpu, SensorType.SmallData, "GPU Memory Total");
            if (used != null && total != null && total.Value > 0)
            {
                var r = Value(Section.Gpu, k + ".vram", "VRAM", $"{used.Value / 1024f:0.0} / {total.Value / 1024f:0} GB");
                r.Bar = used.Value.Value / total.Value.Value;
                Num(r, used.Value.Value / 1024f, "GB");
                rows.Add(r);
            }
        }

        private static void AddMemory(List<Row> rows, List<IHardware> mems)
        {
            // "/ram" is physical memory; "/vram" is commit charge (includes the page file).
            var mem = mems.FirstOrDefault(h => h.Identifier.ToString().StartsWith("/ram"));
            var dimms = mems.Where(h => h.Identifier.ToString().StartsWith("/memory/dimm")).ToList();
            var load = mem == null ? null : Find(mem, SensorType.Load, "Memory");

            // One row with the hottest module; a module's own "High Limit" is only an event threshold.
            var temps = dimms.Select(d => d.Sensors.FirstOrDefault(x => x.SensorType == SensorType.Temperature && x.Value > 0 &&
                                                                        x.Name.StartsWith("DIMM")))
                             .Where(x => x != null).Select(x => x.Value.Value).ToList();
            if (load == null && temps.Count == 0) return;

            rows.Add(Header(Section.Ram, "RAM", null));
            if (load != null)
            {
                var used = Find(mem, SensorType.Data, "Memory Used");
                var avail = Find(mem, SensorType.Data, "Memory Available");
                string text = used != null && avail != null
                    ? $"{used.Value:0.0} / {used.Value + avail.Value:0} GB"
                    : $"{load.Value:0}%";
                rows.Add(new Row
                {
                    Key = "ram.used", Section = Section.Ram, Kind = RowKind.Value, Label = "Used", Text = text, Compact = "RAM",
                    Level = LoadLevel(load.Value.Value), Bar = load.Value.Value / 100f, Value = load.Value.Value, Unit = "%",
                });
            }
            if (temps.Count > 0)
                rows.Add(Temp(Section.Ram, "ram.temp", temps.Count > 1 ? $"Temp (max of {temps.Count})" : "Temperature", temps.Max(), 70, 85));
        }

        internal static void AddFans(List<Row> rows, IList<IHardware> hws, int[] ec)
        {
            var fans = new List<Row>();
            if (ec != null)
                for (int i = 0; i < ec.Length; i++)
                {
                    var fan = ec[i] >= 0 ? Fan(fans.Count, $"Fan {i + 1}", ec[i])
                        : Value(Section.Fans, "fan." + fans.Count, $"Fan {i + 1}", "N/A");
                    if (ec[i] < 0)
                    {
                        fan.Level = Level.Dim;
                        fan.Unit = "RPM";
                        fan.Compact = fan.Label;
                        fan.Detail = "EC fan reading unavailable; no current RPM measurement. Previous readings are not reused.";
                    }
                    fans.Add(fan);
                }

            // Standard fan sensors (desktop SuperIO chips, GPU fans, fan controllers).
            var std = new List<KeyValuePair<string, float>>();
            foreach (var hw in hws)
                foreach (var s in AllSensors(hw).Where(s => s.SensorType == SensorType.Fan && s.Value.HasValue))
                    std.Add(new KeyValuePair<string, float>((IsGpu(hw.HardwareType) ? "GPU " : "") + s.Name, s.Value.Value));
            // Desktop boards report every header; hide the unpopulated ones unless nothing is spinning at all.
            if (std.Any(f => f.Value > 0)) std = std.Where(f => f.Value > 0).ToList();
            foreach (var f in std) fans.Add(Fan(fans.Count, f.Key, (int)Math.Round(f.Value)));

            if (fans.Count == 0)
            {
                // Some controllers only report duty cycle (%) without RPM.
                foreach (var hw in hws)
                    foreach (var s in AllSensors(hw).Where(s => s.SensorType == SensorType.Control && s.Value.HasValue))
                        fans.Add(Load(Section.Fans, "fan.duty" + fans.Count, (IsGpu(hw.HardwareType) ? "GPU " : "") + s.Name, s.Value.Value, null));
            }

            rows.Add(Header(Section.Fans, "Fans", null));
            if (fans.Count == 0)
                rows.Add(new Row { Key = "fan.none", Section = Section.Fans, Kind = RowKind.Value, Label = "Not reported by this PC", Level = Level.Dim });
            else
                rows.AddRange(fans);
        }

        private static Row Fan(int n, string label, int rpm)
        {
            var r = Value(Section.Fans, "fan." + n, label, rpm == 0 ? "stopped" : $"{rpm} RPM");
            r.Compact = label;
            return Num(r, rpm, "RPM");
        }

        private static void AddStorage(List<Row> rows, List<IHardware> drives)
        {
            var list = new List<Row>();
            // Identical models (e.g. three "990 PRO 4TB") get a #n suffix.
            var dupes = new HashSet<string>(drives.GroupBy(d => Shorten(d.Name)).Where(g => g.Count() > 1).Select(g => g.Key));
            var counters = new Dictionary<string, int>();
            for (int i = 0; i < drives.Count; i++)
            {
                var d = drives[i];
                string name = Shorten(d.Name);
                if (dupes.Contains(name))
                {
                    counters.TryGetValue(name, out int n);
                    counters[name] = ++n;
                    name += " #" + n;
                }

                // "Warning"/"Critical" are the drive's limits, not readings.
                var temp = Find(d, SensorType.Temperature, "Composite Temperature", "Temperature")
                           ?? d.Sensors.FirstOrDefault(x => x.SensorType == SensorType.Temperature && x.Value > 0 &&
                                                            x.Name.IndexOf("Warning", StringComparison.OrdinalIgnoreCase) < 0 &&
                                                            x.Name.IndexOf("Critical", StringComparison.OrdinalIgnoreCase) < 0);
                if (temp == null) continue;
                var row = Temp(Section.Drives, "drive." + i, name, temp.Value.Value, 60, 70);
                var used = Find(d, SensorType.Load, "Used Space");
                var life = Find(d, SensorType.Level, "Life");
                var parts = new List<string>();
                if (used != null) parts.Add($"{used.Value:0}% full");
                if (life != null && life.Value > 0) parts.Add($"{life.Value:0}% life left");
                if (parts.Count > 0) row.Detail = string.Join(", ", parts);
                list.Add(row);
            }
            if (list.Count == 0) return;
            rows.Add(Header(Section.Drives, "Drives", null));
            rows.AddRange(list);
        }

        private void AddNetwork(List<Row> rows, List<IHardware> adapters, bool repick)
        {
            if (adapters.Count == 0) return;
            // Show the adapter that has carried the most data since Windows started: in practice the
            // real connection, not a virtual or idle one.
            IHardware pick = adapters.FirstOrDefault(a => a.Identifier.ToString() == _netAdapter);
            if (pick == null || repick)
            {
                float best = -1;
                foreach (var a in adapters)
                {
                    float total = (Find(a, SensorType.Data, "Data Downloaded")?.Value ?? 0) + (Find(a, SensorType.Data, "Data Uploaded")?.Value ?? 0);
                    if (total > best) { best = total; pick = a; }
                }
                _netAdapter = pick?.Identifier.ToString();
            }
            if (pick == null) return;

            var down = Find(pick, SensorType.Throughput, "Download Speed");
            var up = Find(pick, SensorType.Throughput, "Upload Speed");
            if (down == null && up == null) return;
            rows.Add(Header(Section.Network, "Network", pick.Name.Length > 22 ? pick.Name.Substring(0, 21) + "\u2026" : pick.Name));
            if (down != null) { var r = Num(Value(Section.Network, "net.down", "Download", Speed(down.Value.Value)), down.Value.Value / 1024f, "KB/s"); r.Compact = "Download"; rows.Add(r); }
            if (up != null) { var r = Num(Value(Section.Network, "net.up", "Upload", Speed(up.Value.Value)), up.Value.Value / 1024f, "KB/s"); r.Compact = "Upload"; rows.Add(r); }
        }

        internal static string Speed(float bytesPerSecond)
        {
            float kb = Math.Max(0, bytesPerSecond) / 1024f;
            return kb >= 1000 ? $"{kb / 1024f:0.0} MB/s" : $"{kb:0} KB/s";
        }

        /// <summary>Per-core temperatures for the CPU header tooltip, grouped by core type.</summary>
        private static string CoreDetail(IHardware cpu, bool fahrenheit)
        {
            var groups = new List<KeyValuePair<string, List<float>>>();
            foreach (var s in cpu.Sensors)
            {
                if (s.SensorType != SensorType.Temperature || !(s.Value > 0) || float.IsInfinity(s.Value.Value) || s.Name.Contains("Distance")) continue;
                int hash = s.Name.IndexOf(" #", StringComparison.Ordinal);
                if (hash < 0) continue;
                string group = s.Name.Substring(0, hash).Replace("CPU ", "") + "s"; // "P-Cores", "E-Cores", "Cores"
                var g = groups.FirstOrDefault(x => x.Key == group);
                if (g.Value == null) { g = new KeyValuePair<string, List<float>>(group, new List<float>()); groups.Add(g); }
                g.Value.Add(fahrenheit ? s.Value.Value * 9 / 5 + 32 : s.Value.Value);
            }
            if (groups.Count == 0) return null;
            string unit = fahrenheit ? " \u00B0F" : " \u00B0C";
            return string.Join("\n", groups.Select(g => g.Key + ": " + string.Join("  ", g.Value.Select(v => v.ToString("0"))) + unit));
        }

        private static void AddBattery(List<Row> rows, IHardware bat)
        {
            var charge = bat.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Level && s.Value.HasValue &&
                                                         s.Name.IndexOf("Charge", StringComparison.OrdinalIgnoreCase) >= 0);
            var wear = bat.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Level && s.Value.HasValue &&
                                                       s.Name.IndexOf("Degradation", StringComparison.OrdinalIgnoreCase) >= 0);
            var rate = bat.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Power && s.Value.HasValue && Math.Abs(s.Value.Value) > 0.05f);
            var volt = bat.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Voltage && s.Value.HasValue);
            var time = bat.Sensors.FirstOrDefault(s => s.SensorType.ToString() == "TimeSpan" && s.Value > 0);
            if (charge == null && volt == null) return;

            rows.Add(Header(Section.Battery, "Battery", null));
            if (charge != null)
                rows.Add(new Row
                {
                    Key = "bat.charge", Section = Section.Battery, Kind = RowKind.Value, Label = "Charge", Compact = "Battery",
                    Text = $"{charge.Value:0}%", Level = charge.Value < 20 ? Level.Warm : Level.Normal, Bar = charge.Value.Value / 100f,
                    Value = charge.Value.Value, Unit = "%",
                });

            bool onAc = System.Windows.Forms.SystemInformation.PowerStatus.PowerLineStatus == System.Windows.Forms.PowerLineStatus.Online;
            if (rate != null)
            {
                bool discharging = !onAc || rate.Name.IndexOf("Discharg", StringComparison.OrdinalIgnoreCase) >= 0 || rate.Value < 0;
                rows.Add(Num(Value(Section.Battery, "bat.rate", discharging ? "Discharging" : "Charging", $"{Math.Abs(rate.Value.Value):0.0} W"), discharging ? -Math.Abs(rate.Value.Value) : Math.Abs(rate.Value.Value), "W"));
            }
            else if (onAc && charge != null && charge.Value < 99)
            {
                // Plugged in, below full and no current flowing: a charge limit is holding it.
                var r = Value(Section.Battery, "bat.rate", "Status", "Not charging");
                r.Level = Level.Dim;
                rows.Add(r);
            }
            if (time != null && !onAc)
            {
                var span = TimeSpan.FromSeconds(time.Value.Value);
                rows.Add(Value(Section.Battery, "bat.time", "Time left", span.TotalHours >= 1 ? $"{(int)span.TotalHours} h {span.Minutes:00} min" : $"{span.Minutes} min"));
            }
            if (volt != null) rows.Add(Value(Section.Battery, "bat.volt", "Voltage", $"{volt.Value:0.000} V"));
            if (wear != null) rows.Add(Value(Section.Battery, "bat.wear", "Wear", $"{wear.Value:0.0}%"));
        }

        // ---- helpers ----

        private static readonly Regex Noise = new Regex(
            @"\((R|TM|tm)\)|\bIntel\b|\bCore\b(?! \d)|\bAMD\b|\bNVIDIA\b|\bGeForce\b|\bLaptop GPU\b|\bProcessor\b|\bCPU\b|\bGraphics\b|\bSamsung SSD\b|\b\d+-Core\b|@.*$|\bwith Radeon.*$",
            RegexOptions.Compiled);

        internal static string Shorten(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "";
            string s = Regex.Replace(Noise.Replace(name, " "), @"\s+", " ").Trim();
            return s.Length >= 3 ? s : name.Trim();
        }

        private static Row Header(Section section, string title, string sub) =>
            new Row { Section = section, Kind = RowKind.Header, Label = title, Text = sub ?? "" };

        private static Row Value(Section section, string key, string label, string text) =>
            new Row { Key = key, Section = section, Kind = RowKind.Value, Label = label, Text = text };

        private static Row Temp(Section section, string key, string label, float t, float warm, float hot, bool spark = false, string compact = null) =>
            new Row { Key = key, Section = section, Kind = RowKind.Temp, Label = label, Value = t, Warm = warm, Hot = hot, Spark = spark, Compact = compact };

        private static Row Load(Section section, string key, string label, float pct, string compact) =>
            new Row { Key = key, Section = section, Kind = RowKind.Value, Label = label, Text = $"{pct:0}%", Level = LoadLevel(pct), Bar = pct / 100f, Compact = compact, Value = pct, Unit = "%" };

        private static Row Num(Row r, float value, string unit)
        {
            r.Value = value;
            r.Unit = unit;
            return r;
        }

        private static Level LoadLevel(float p) => p >= 90 ? Level.Hot : p >= 70 ? Level.Warm : Level.Normal;

        public string Report()
        {
            var sb = new StringBuilder();
            sb.AppendLine("TempWidget " + Program.Version + " sensor report");
            sb.AppendLine("Administrator: " + Program.IsElevated + ", PawnIO installed: " + PawnIoInstalled() + ", Store package: " + Program.IsPackaged);
            try
            {
                var sys = _computer.SMBios?.System;
                if (sys != null) sb.AppendLine($"System: {sys.ManufacturerName} / {sys.ProductName} / {sys.Version}");
            }
            catch { }
            sb.AppendLine("EC fan readout: " + (_ecFans != null ? "active" : "off"));
            sb.AppendLine();
            foreach (var hw in _computer.Hardware) Dump(sb, hw, "");
            return sb.ToString();
        }

        private static void Dump(StringBuilder sb, IHardware hw, string indent)
        {
            sb.AppendLine($"{indent}[{hw.HardwareType}] {hw.Name}");
            foreach (var s in hw.Sensors)
                sb.AppendLine($"{indent}  {s.SensorType,-12} {s.Name,-36} {s.Value}");
            foreach (var sub in hw.SubHardware) Dump(sb, sub, indent + "  ");
        }

        public void Dispose()
        {
            _ecFans?.Dispose();
            try { _computer.Close(); } catch { }
        }
    }
}
