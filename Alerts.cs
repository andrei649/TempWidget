using System;
using System.Collections.Generic;

namespace TempWidget
{
    /// <summary>
    /// Raises one notification when a temperature stays above its limit for several readings
    /// in a row, then stays quiet for that sensor until it has cooled down or five minutes pass.
    /// </summary>
    internal sealed class Alerts
    {
        private const int ReadingsNeeded = 3;
        private static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(5);

        private sealed class State { public int Over; public DateTime LastAlert = DateTime.MinValue; public bool Armed = true; }
        private readonly Dictionary<string, State> _states = new Dictionary<string, State>();

        /// <summary>Returns the messages to show for this set of readings (usually none).</summary>
        public List<string> Check(List<Row> rows, Settings s, Func<float, string> formatTemp)
        {
            var messages = new List<string>();
            if (!s.AlertsEnabled) return messages;
            foreach (var r in rows)
            {
                if (r.Kind != RowKind.Temp || r.Key == null) continue;
                int limit;
                string what;
                if (r.Section == Section.Cpu && r.Key.EndsWith(".temp")) { limit = s.AlertCpu; what = "CPU"; }
                else if (r.Section == Section.Gpu && r.Key.EndsWith(".temp")) { limit = s.AlertGpu; what = "GPU"; }
                else if (r.Section == Section.Drives) { limit = s.AlertDrive; what = r.Label; }
                else continue;

                if (!_states.TryGetValue(r.Key, out var st)) _states[r.Key] = st = new State();
                if (!r.HasValue) { st.Over = 0; continue; }
                if (r.Value >= limit)
                {
                    st.Over++;
                    bool cooledDown = DateTime.UtcNow - st.LastAlert > Cooldown;
                    if (st.Over >= ReadingsNeeded && (st.Armed || cooledDown))
                    {
                        messages.Add($"{what} is at {formatTemp(r.Value)} (limit {formatTemp(limit)})");
                        st.LastAlert = DateTime.UtcNow;
                        st.Armed = false;
                    }
                }
                else
                {
                    st.Over = 0;
                    if (r.Value < limit - 5) st.Armed = true;
                }
            }
            return messages;
        }
    }
}
