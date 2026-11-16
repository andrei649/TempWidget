using System;
using System.Runtime.InteropServices;
using System.Text;

namespace TempWidget
{
    /// <summary>Win32 calls. Every wrapper swallows failures so missing APIs never crash the widget.</summary>
    internal static class Native
    {
        public const int WM_NCLBUTTONDOWN = 0xA1, HTCAPTION = 2, WM_DPICHANGED = 0x02E0, WM_SETTINGCHANGE = 0x001A;

        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wp, IntPtr lp);
        [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr hIcon);
        [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);
        [DllImport("psapi.dll")] private static extern bool EmptyWorkingSet(IntPtr hProcess);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern int GetCurrentPackageFullName(ref int length, StringBuilder name);

        /// <summary>Adds the UAC shield to a button (FlatStyle.System) to signal the admin prompt.</summary>
        public static void ShowShield(IntPtr button)
        {
            try { SendMessage(button, 0x160C, IntPtr.Zero, (IntPtr)1); } catch { } // BCM_SETSHIELD
        }

        public static void BeginDrag(IntPtr hwnd)
        {
            try { ReleaseCapture(); SendMessage(hwnd, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero); } catch { }
        }

        public static void RoundCorners(IntPtr hwnd)
        {
            try { int round = 2; DwmSetWindowAttribute(hwnd, 33, ref round, 4); } catch { } // Windows 11 only
        }

        /// <summary>DPI of the monitor the window is on; 0 when the API is unavailable.</summary>
        public static int WindowDpi(IntPtr hwnd)
        {
            try { return (int)GetDpiForWindow(hwnd); } catch { return 0; }
        }

        public static void FreeIcon(IntPtr hIcon)
        {
            try { if (hIcon != IntPtr.Zero) DestroyIcon(hIcon); } catch { }
        }

        public static void TrimWorkingSet()
        {
            try { GC.Collect(); GC.WaitForPendingFinalizers(); EmptyWorkingSet(System.Diagnostics.Process.GetCurrentProcess().Handle); } catch { }
        }

        /// <summary>True when running from an MSIX package (Microsoft Store install).</summary>
        public static bool IsPackaged()
        {
            try
            {
                int len = 0;
                return GetCurrentPackageFullName(ref len, null) != 15700; // APPMODEL_ERROR_NO_PACKAGE
            }
            catch { return false; }
        }
    }
}
