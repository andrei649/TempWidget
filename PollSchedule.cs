using System;
using System.Diagnostics;

namespace TempWidget
{
    /// <summary>Elapsed-time deadlines, independent of refresh rate and battery saver.</summary>
    internal sealed class PollSchedule
    {
        internal static readonly TimeSpan SlowInterval = TimeSpan.FromSeconds(30);
        internal static readonly TimeSpan BatteryInterval = TimeSpan.FromSeconds(10);
        private readonly Func<TimeSpan> _elapsed;
        private TimeSpan? _lastSlow, _lastBattery;

        public PollSchedule() : this(CreateClock()) { }
        internal PollSchedule(Func<TimeSpan> elapsed) { _elapsed = elapsed; }

        private static Func<TimeSpan> CreateClock()
        {
            var clock = Stopwatch.StartNew();
            return () => clock.Elapsed;
        }

        internal void Take(bool light, out bool slow, out bool battery)
        {
            slow = battery = false;
            // CPU/GPU-only polls must not consume deadlines for skipped devices.
            if (light) return;
            var now = _elapsed();
            slow = !_lastSlow.HasValue || now - _lastSlow.Value >= SlowInterval;
            battery = !_lastBattery.HasValue || now - _lastBattery.Value >= BatteryInterval;
            if (slow) _lastSlow = now;
            if (battery) _lastBattery = now;
        }
    }
}
