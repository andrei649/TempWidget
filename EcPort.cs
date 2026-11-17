using System;
using System.Diagnostics;
using LibreHardwareMonitor.PawnIo;

namespace TempWidget
{
    /// <summary>Test seam: fake implementations never create a driver or touch ports.</summary>
    internal interface IEcFanDevice : IDisposable
    {
        bool TryAcquire(int timeoutMs);
        bool TryRead(byte register, out byte value);
        void Release();
    }

    internal sealed class PawnIoEcFanDevice : IEcFanDevice
    {
        private readonly System.Threading.Mutex _mutex;
        private readonly LpcAcpiEc _ec;

        public PawnIoEcFanDevice()
        {
            // A usable shared mutex is required before even opening the EC transport.
            _mutex = new System.Threading.Mutex(false, @"Global\Access_EC");
            try { _ec = new LpcAcpiEc(); }
            catch { _mutex.Dispose(); throw; }
        }

        public bool TryAcquire(int timeoutMs)
        {
            try { return _mutex.WaitOne(timeoutMs); }
            catch (System.Threading.AbandonedMutexException) { return true; } // ownership was transferred to us
        }

        public bool TryRead(byte register, out byte value) => EcPort.TryRead(_ec, register, out value);
        public void Release() => _mutex.ReleaseMutex();
        public void Dispose()
        {
            try { _ec.Close(); } finally { _mutex.Dispose(); }
        }
    }

    /// <summary>
    /// Read-only access to the ACPI embedded controller: only the standard "read byte"
    /// command (0x80) is sent; nothing is ever written into EC RAM. (LibreHardwareMonitor's
    /// own EC reader writes a bank register at 0xFF, which is board-specific, so it is not used.)
    /// </summary>
    internal static class EcPort
    {
        private const byte DataPort = 0x62, CommandPort = 0x66;
        private const byte OutputBufferFull = 0x01, InputBufferFull = 0x02;
        private const byte ReadCommand = 0x80;

        internal static bool TryRead(LpcAcpiEc ec, byte register, out byte value)
        {
            for (int attempt = 0; attempt < 5; attempt++)
            {
                if (WaitStatus(ec, InputBufferFull, false))
                {
                    ec.WritePort(CommandPort, ReadCommand);
                    if (WaitStatus(ec, InputBufferFull, false))
                    {
                        ec.WritePort(DataPort, register);
                        if (WaitStatus(ec, OutputBufferFull, true))
                        {
                            value = ec.ReadPort(DataPort);
                            return true;
                        }
                    }
                }
            }
            value = 0;
            return false;
        }

        private static bool WaitStatus(LpcAcpiEc ec, byte flag, bool set)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < 5)
            {
                bool isSet = (ec.ReadPort(CommandPort) & flag) != 0;
                if (isSet == set) return true;
            }
            return false;
        }
    }
}

namespace TempWidget
{
    /// <summary>
    /// Fan tachometers in the Clevo EC (verified on System76 bonw16): 16-bit big-endian period at 0xD0 (fan 1),
    /// 0xD2 (fan 2), 0xD4 (fan 3, if present). RPM = 2156220 / raw. Read-only, 2 bytes per fan.
    /// </summary>
    internal sealed class EcFans : System.IDisposable
    {
        private static readonly byte[] Registers = { 0xD0, 0xD2, 0xD4 };
        // Laptop fans either stop (period 0) or spin at several hundred RPM or more. A period this long
        // (e.g. 34 RPM from 0xF7xx on an empty fan-3 slot) is the controller's "no tach signal", not a speed.
        private const int MinRpm = 200;
        private readonly IEcFanDevice _device;
        private bool _fan3Seen;

        public EcFans() : this(new PawnIoEcFanDevice()) { }
        internal EcFans(IEcFanDevice device) { _device = device ?? throw new ArgumentNullException(nameof(device)); }

        /// <summary>RPM per fan; -1 when unknown. Fan 3 only once it has ever reported a plausible speed.</summary>
        public int[] Read()
        {
            // Every call starts unknown: failed/contended reads never look like fresh RPM.
            var current = new[] { -1, -1, -1 };
            bool owned = false;
            try
            {
                owned = _device.TryAcquire(50);
                if (owned)
                {
                    for (int i = 0; i < Registers.Length; i++)
                    {
                        if (!_device.TryRead(Registers[i], out byte hi) || !_device.TryRead((byte)(Registers[i] + 1), out byte lo))
                            continue;
                        int raw = (hi << 8) | lo;
                        if (raw == 0xFFFF) continue; // invalid/unavailable register, not a stopped fan
                        int rpm = raw == 0 ? 0 : 2156220 / raw;
                        if (rpm > 9000) continue; // implausible/torn sample: unknown this time
                        if (rpm < MinRpm) rpm = 0; // no tach signal: treat as not spinning
                        if (i == 2 && rpm > 0) _fan3Seen = true; // a real third fan has reported a plausible speed
                        current[i] = rpm;
                    }
                }
            }
            catch { /* driver or mutex failure: unmeasured fans remain unknown */ }
            finally { if (owned) try { _device.Release(); } catch { } }
            return _fan3Seen ? current : new[] { current[0], current[1] };
        }

        public void Dispose()
        {
            try { _device.Dispose(); } catch { }
        }
    }
}
