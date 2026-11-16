using System;

namespace TempWidget
{
    /// <summary>Constrain marked startup and conservatively suppress unmarked packaged onboarding.</summary>
    internal sealed class LaunchOptions
    {
        public bool Startup { get; }
        public bool Setup { get; }
        public bool AutoStart { get; }
        private readonly bool _packaged;
        internal LaunchOptions(string[] args, bool packaged = false)
        {
            _packaged = packaged;
            Startup = Array.IndexOf(args, "--startup") >= 0;
            Setup = !Startup && Array.IndexOf(args, "--setup") >= 0;
            AutoStart = Setup && Array.IndexOf(args, "--autostart") >= 0;
        }

        // Package identity alone cannot distinguish manual launch from MSIX logon
        // activation. Suppress automatic onboarding in both, including after settings reset.
        // Manual full-sensor requests still use the widget's explicit user action.
        internal bool ShouldShowOnboarding(bool onboarded) => !onboarded && !Startup && !_packaged;
    }
}
