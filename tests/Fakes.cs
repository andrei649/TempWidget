using System;
using System.Collections.Generic;
using LibreHardwareMonitor.Hardware;

namespace TempWidget.Tests
{
    // These objects implement the actual 0.9.6 interfaces. No Computer, driver, port,
    // registry, settings file, Form or application entry point is constructed.
    internal sealed class FakeHardware : IHardware
    {
        public HardwareType HardwareType => HardwareType.Cpu;
        public Identifier Identifier => new Identifier("synthetic", "cpu");
        public string Name { get; set; } = "Synthetic CPU";
        public IHardware Parent => null;
        public ISensor[] Sensors { get; set; } = new ISensor[0];
        public IHardware[] SubHardware => new IHardware[0];
        public IDictionary<string, string> Properties => new Dictionary<string, string>();
        public event SensorEventHandler SensorAdded { add { } remove { } }
        public event SensorEventHandler SensorRemoved { add { } remove { } }
        public string GetReport() => "Synthetic hardware only";
        public void Update() { throw new InvalidOperationException("Tests must not update hardware"); }
        public void Accept(IVisitor visitor) { }
        public void Traverse(IVisitor visitor) { }
    }

    internal sealed class FakeSensor : ISensor
    {
        public FakeSensor(string name, float? value, SensorType type = SensorType.Temperature)
        { Name = name; Value = value; SensorType = type; }
        public IControl Control => null;
        public IHardware Hardware => null;
        public Identifier Identifier => new Identifier("synthetic", "sensor");
        public int Index => 0;
        public bool IsDefaultHidden => false;
        public float? Min => Value;
        public float? Max => Value;
        public string Name { get; set; }
        public IReadOnlyList<IParameter> Parameters => new IParameter[0];
        public SensorType SensorType { get; }
        public float? Value { get; set; }
        public IEnumerable<SensorValue> Values => new SensorValue[0];
        public TimeSpan ValuesTimeWindow { get; set; }
        public void ResetMin() { }
        public void ResetMax() { }
        public void ClearValues() { }
        public void Accept(IVisitor visitor) { }
        public void Traverse(IVisitor visitor) { }
    }

    internal sealed class FakeEcDevice : IEcFanDevice
    {
        public bool Acquire = true, ThrowAcquire, ThrowRead;
        public int Reads, Releases, Disposals, Acquisitions;
        public readonly Dictionary<byte, byte> Bytes = new Dictionary<byte, byte>();
        public readonly HashSet<byte> Fail = new HashSet<byte>();
        public bool TryAcquire(int timeoutMs)
        {
            if (timeoutMs != 50) throw new Exception("Unexpected lock timeout");
            Acquisitions++;
            if (ThrowAcquire) throw new InvalidOperationException("Synthetic lock failure");
            return Acquire;
        }
        public bool TryRead(byte register, out byte value)
        {
            if (!Acquire || ThrowAcquire) throw new Exception("Read without ownership");
            Reads++;
            if (ThrowRead) throw new InvalidOperationException("Synthetic driver failure");
            value = 0;
            return !Fail.Contains(register) && Bytes.TryGetValue(register, out value);
        }
        public void Set(int fan, int raw)
        {
            byte address = (byte)(0xD0 + fan * 2);
            Bytes[address] = (byte)(raw >> 8);
            Bytes[(byte)(address + 1)] = (byte)raw;
        }
        public void Release() { Releases++; }
        public void Dispose() { Disposals++; }
    }
}
