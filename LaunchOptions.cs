using System;

namespace TempWidget
{
    /// <summary>Constrain marked startup and conservatively suppress unmarked packaged onboarding.</summary>
    internal sealed class LaunchOptions
    {
        public bool Startup { get; }
        public bool Setup { get; }
        public bool AutoStart { get; }
        public bool Restarted { get; }
        public bool Limited { get; }
        private readonly bool _packaged;
        internal LaunchOptions(string[] args, bool packaged = false)
        {
            _packaged = packaged;
            Startup = Array.IndexOf(args, "--startup") >= 0;
            Setup = !Startup && Array.IndexOf(args, "--setup") >= 0;
            AutoStart = Setup && Array.IndexOf(args, "--autostart") >= 0;
            Restarted = Array.IndexOf(args, "--restarted") >= 0;
            Limited = Array.IndexOf(args, "--limited") >= 0;
        }

        // Package identity alone cannot distinguish manual launch from MSIX logon
        // activation. Suppress automatic onboarding in both, including after settings reset.
        // Manual full-sensor requests still use the widget's explicit user action.
        internal bool ShouldShowOnboarding(bool onboarded) => !onboarded && !Startup && !_packaged;

        // Manual unpackaged launches honor PreferElevated so double-clicking TempWidget opens
        // directly with full sensors; logon (--startup), MSIX, --demo, --restarted and --limited never auto-elevate.
        internal bool ShouldAutoElevate(bool preferElevated, bool isElevated, bool demo) =>
            preferElevated && !isElevated && !demo && !Startup && !_packaged && !Restarted && !Limited;
    }
}
