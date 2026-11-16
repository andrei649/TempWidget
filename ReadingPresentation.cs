using System;

namespace TempWidget
{
    internal static class ReadingPresentation
    {
        internal static string Temperature(float celsius, bool fahrenheit) =>
            float.IsNaN(celsius) || float.IsInfinity(celsius) ? "N/A" :
            fahrenheit ? $"{celsius * 9 / 5 + 32:0} \u00B0F" : $"{celsius:0} \u00B0C";

        internal static Row TrayTemperature(Row cpu, Row gpu, int mode)
        {
            if (cpu != null && !cpu.HasValue) cpu = null;
            if (gpu != null && !gpu.HasValue) gpu = null;
            return mode == 1 ? cpu : mode == 2 ? gpu
                : mode == 3 ? (cpu == null ? gpu : gpu == null || cpu.Value >= gpu.Value ? cpu : gpu) : null;
        }
    }
}
