using System;
using System.Collections.Generic;

namespace TempWidget
{
    internal enum Section { Notice, Cpu, Gpu, Ram, Fans, Drives, Network, Battery }

    internal enum RowKind { Header, Temp, Value, Notice }

    internal enum Level { Dim, Normal, Good, Warm, Hot }

    internal enum NoticeAction { None, Elevate, InstallDriver, Dismiss, FileBlocked, ShowLog, StartupSafety }

    /// <summary>One line of the widget. Sensor sources produce these; the form only formats and paints them.</summary>
    internal sealed class Row
    {
        public string Key;                 // stable id, e.g. "cpu.temp": used for history, min/max and alerts
        public Section Section;
        public RowKind Kind;
        public string Label;
        public string Text = "";           // right-hand text (headers: device name; values: preformatted)
        public float Value = float.NaN;    // °C for Temp rows; for other rows the plain number, used by the CSV log
        public string Unit;                // unit of Value for the CSV log, e.g. "W"
        public string Detail;              // extra lines shown in the tooltip
        public float Warm = 75, Hot = 90;  // colour thresholds for Temp rows
        public Level Level = Level.Normal; // colour for Value rows
        public float Bar = -1;             // 0..1, or -1 for none
        public bool Spark;                 // draw temperature history next to the value
        public string Compact;             // short label when shown in compact mode; null = hidden there
        public NoticeAction Action;

        public bool HasValue => !float.IsNaN(Value) && !float.IsInfinity(Value);
        public Level TempLevel => !HasValue ? Level.Dim : Value >= Hot ? Level.Hot : Value >= Warm ? Level.Warm : Level.Good;
    }

    internal interface ISensorSource : IDisposable
    {
        /// <param name="light">Only CPU and GPU (used while the widget is hidden).</param>
        List<Row> Poll(bool light);

        /// <summary>Plain-text list of every sensor, for support requests.</summary>
        string Report();
    }

    /// <summary>Fixed-size temperature history plus min/max since start (or since the last reset).</summary>
    internal sealed class History
    {
        public const int Capacity = 60;
        private readonly float[] _values = new float[Capacity];
        private int _next;
        public int Count { get; private set; }
        public float Min { get; private set; } = float.MaxValue;
        public float Max { get; private set; } = float.MinValue;
        private double _sum;
        private int _samples;
        public float Average => _samples > 0 ? (float)(_sum / _samples) : float.NaN;

        public void Add(float v)
        {
            if (float.IsNaN(v) || float.IsInfinity(v)) return;
            _values[_next] = v;
            _next = (_next + 1) % Capacity;
            if (Count < Capacity) Count++;
            if (v < Min) Min = v;
            if (v > Max) Max = v;
            _sum += v;
            _samples++;
        }

        /// <summary>i = 0 is the oldest retained sample.</summary>
        public float this[int i] => _values[(_next - Count + i + Capacity * 2) % Capacity];

        public void ResetRange()
        {
            Min = float.MaxValue;
            Max = float.MinValue;
            _sum = 0;
            _samples = 0;
        }
    }
}
