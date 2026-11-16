using System.Drawing;
using Microsoft.Win32;

namespace TempWidget
{
    internal sealed class Theme
    {
        public Color Bg, Line, BarBg, Label, Text, Dim, Accent, Good, Warm, Hot, NoticeBg;

        public static readonly Theme Dark = new Theme
        {
            Bg = Color.FromArgb(22, 22, 26),
            Line = Color.FromArgb(45, 45, 52),
            BarBg = Color.FromArgb(50, 50, 58),
            Label = Color.FromArgb(185, 185, 195),
            Text = Color.FromArgb(230, 230, 230),
            Dim = Color.FromArgb(140, 140, 150),
            Accent = Color.FromArgb(150, 170, 255),
            Good = Color.FromArgb(120, 220, 140),
            Warm = Color.FromArgb(255, 190, 70),
            Hot = Color.FromArgb(255, 90, 80),
            NoticeBg = Color.FromArgb(52, 44, 24),
        };

        public static readonly Theme Light = new Theme
        {
            Bg = Color.FromArgb(248, 248, 250),
            Line = Color.FromArgb(222, 222, 228),
            BarBg = Color.FromArgb(214, 214, 222),
            Label = Color.FromArgb(72, 72, 84),
            Text = Color.FromArgb(24, 24, 30),
            Dim = Color.FromArgb(118, 118, 130),
            Accent = Color.FromArgb(52, 84, 200),
            Good = Color.FromArgb(22, 130, 58),
            Warm = Color.FromArgb(176, 104, 0),
            Hot = Color.FromArgb(200, 40, 40),
            NoticeBg = Color.FromArgb(255, 240, 200),
        };

        public Color For(Level level)
        {
            switch (level)
            {
                case Level.Dim: return Dim;
                case Level.Good: return Good;
                case Level.Warm: return Warm;
                case Level.Hot: return Hot;
                default: return Text;
            }
        }

        /// <param name="setting">0 dark, 1 light, 2 match the Windows app theme.</param>
        public static Theme Resolve(int setting)
        {
            if (setting == 1) return Light;
            if (setting == 2 && WindowsUsesLightTheme()) return Light;
            return Dark;
        }

        private static bool WindowsUsesLightTheme()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                    return key?.GetValue("AppsUseLightTheme") is int v && v != 0;
            }
            catch { return false; }
        }
    }
}
