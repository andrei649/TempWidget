using System;

namespace TempWidget
{
    /// <summary>Startup never triggers onboarding, driver setup, or further startup configuration.</summary>
    internal sealed class LaunchOptions
    {
        public bool Startup { get; }
        public bool Setup { get; }
        public bool AutoStart { get; }
        internal LaunchOptions(string[] args)
        {
            Startup = Array.IndexOf(args, "--startup") >= 0;
            Setup = !Startup && Array.IndexOf(args, "--setup") >= 0;
            AutoStart = Setup && Array.IndexOf(args, "--autostart") >= 0;
        }
    }
}
