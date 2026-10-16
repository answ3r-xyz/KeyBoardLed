using System;
using System.Runtime.InteropServices;
using KeyboardLed.Models;

namespace KeyboardLed.Services
{
    /// <summary>
    /// Monitor és DPI információk (Per-Monitor V2 DPI aware módban minden érték fizikai pixel)
    /// </summary>
    public static class DisplayInfo
    {
        private const uint MONITOR_DEFAULTTOPRIMARY = 1;
        private const uint MONITOR_DEFAULTTONEAREST = 2;
        private const int MDT_EFFECTIVE_DPI = 0;
        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;
        private const int SM_CXSMICON = 49;

        /// <summary>
        /// Rendszer DPI szorzó (a régi, DIP-ben tárolt pozíciók átváltásához)
        /// </summary>
        public static double SystemScale
        {
            get
            {
                try { return GetDpiForSystem() / 96.0; }
                catch (EntryPointNotFoundException) { return 1.0; }
            }
        }

        public static PixelRect GetWorkArea(OverlayMonitor mode)
        {
            IntPtr monitor = IntPtr.Zero;
            switch (mode)
            {
                case OverlayMonitor.MouseCursor:
                    if (GetCursorPos(out var cursor))
                    {
                        monitor = MonitorFromPoint(cursor, MONITOR_DEFAULTTONEAREST);
                    }
                    break;
                case OverlayMonitor.ActiveWindow:
                    var foreground = GetForegroundWindow();
                    if (foreground != IntPtr.Zero)
                    {
                        monitor = MonitorFromWindow(foreground, MONITOR_DEFAULTTONEAREST);
                    }
                    break;
            }
            return GetWorkArea(monitor != IntPtr.Zero ? monitor : PrimaryMonitor());
        }

        /// <summary>
        /// A ponthoz legközelebbi monitor teljes területe (a tálcát is beleértve)
        /// </summary>
        public static PixelRect GetMonitorAreaNearest(int x, int y) =>
            GetWorkArea(MonitorFromPoint(new POINT { X = x, Y = y }, MONITOR_DEFAULTTONEAREST), fullMonitor: true);

        public static PixelRect GetWorkAreaForRect(PixelRect rect)
        {
            var r = new RECT { Left = rect.Left, Top = rect.Top, Right = rect.Right, Bottom = rect.Bottom };
            return GetWorkArea(MonitorFromRect(ref r, MONITOR_DEFAULTTONEAREST));
        }

        /// <summary>
        /// Tálcaikon mérete pixelben az elsődleges monitor (tálca) DPI-je alapján
        /// </summary>
        public static int GetTrayIconSize()
        {
            uint dpi = 96;
            try
            {
                if (GetDpiForMonitor(PrimaryMonitor(), MDT_EFFECTIVE_DPI, out uint dpiX, out _) == 0 && dpiX > 0)
                {
                    dpi = dpiX;
                }
                int size = GetSystemMetricsForDpi(SM_CXSMICON, dpi);
                if (size > 0) return size;
            }
            catch (Exception ex) when (ex is EntryPointNotFoundException || ex is DllNotFoundException)
            {
                // Régi Windows: fallback lent
            }
            return (int)Math.Round(16 * dpi / 96.0);
        }

        private static IntPtr PrimaryMonitor() => MonitorFromPoint(new POINT { X = 0, Y = 0 }, MONITOR_DEFAULTTOPRIMARY);

        private static PixelRect GetWorkArea(IntPtr monitor, bool fullMonitor = false)
        {
            var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (monitor != IntPtr.Zero && GetMonitorInfo(monitor, ref info))
            {
                var r = fullMonitor ? info.rcMonitor : info.rcWork;
                return new PixelRect(r.Left, r.Top, r.Right, r.Bottom);
            }
            return new PixelRect(0, 0, GetSystemMetrics(SM_CXSCREEN), GetSystemMetrics(SM_CYSCREEN));
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromRect(ref RECT lprc, uint dwFlags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetricsForDpi(int nIndex, uint dpi);

        [DllImport("user32.dll")]
        private static extern uint GetDpiForSystem();

        [DllImport("shcore.dll")]
        private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);
    }
}
