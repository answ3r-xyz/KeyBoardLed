using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace KeyboardLed.Services
{
    /// <summary>
    /// Optimalizált icon helper - előre generált cached ikonokkal (8 állapot)
    /// </summary>
    public static class IconHelper
    {
        // Cached icons - 8 lehetséges állapot (2^3)
        private static readonly Icon[] _cachedIcons = new Icon[KeyboardState.StateCount];
        private static bool _initialized;

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr hIcon);
        
        // Cached colors
        private static readonly Color OnColor = Color.FromArgb(50, 255, 100);
        private static readonly Color OffColor = Color.FromArgb(60, 60, 65);
        private static readonly Color BgColor = Color.FromArgb(30, 30, 35);
        
        /// <summary>
        /// Inicializálja az összes lehetséges ikont (8 állapot)
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;
            
            for (int i = 0; i < KeyboardState.StateCount; i++)
            {
                _cachedIcons[i] = CreateIconInternal(KeyboardState.FromIndex(i));
            }
            _initialized = true;
        }
        
        /// <summary>
        /// Visszaad egy cached ikont az állapot alapján - NINCS allokáció!
        /// </summary>
        public static Icon GetTrayIcon(KeyboardState state)
        {
            if (!_initialized) Initialize();
            return _cachedIcons[state.ToIndex()];
        }
        
        /// <summary>
        /// Visszaad egy cached ikont bool értékek alapján
        /// </summary>
        public static Icon GetTrayIcon(bool numLock, bool capsLock, bool scrollLock)
            => GetTrayIcon(new KeyboardState { NumLock = numLock, CapsLock = capsLock, ScrollLock = scrollLock });
        
        // Legacy compatibility
        public static Icon CreateTrayIcon(bool numLock = true, bool capsLock = false, bool scrollLock = false)
            => GetTrayIcon(numLock, capsLock, scrollLock);
        
        private static Icon CreateIconInternal(KeyboardState state)
        {
            using var bitmap = new Bitmap(16, 16);
            using var g = Graphics.FromImage(bitmap);
            
            g.SmoothingMode = SmoothingMode.HighSpeed;
            g.Clear(BgColor);
            
            // LED bars - egyszerűsített rajzolás
            const int barHeight = 3;
            const int barWidth = 12;
            const int x = 2;
            const int gap = 2;
            
            using var onBrush = new SolidBrush(OnColor);
            using var offBrush = new SolidBrush(OffColor);
            
            g.FillRectangle(state.NumLock ? onBrush : offBrush, x, 2, barWidth, barHeight);
            g.FillRectangle(state.CapsLock ? onBrush : offBrush, x, 2 + barHeight + gap, barWidth, barHeight);
            g.FillRectangle(state.ScrollLock ? onBrush : offBrush, x, 2 + (barHeight + gap) * 2, barWidth, barHeight);

            // Icon.FromHandle nem birtokolja a HICON-t, ezért Dispose nem szabadítaná fel.
            // Clone() saját (owned) másolatot készít, az eredeti handle-t azonnal felszabadítjuk.
            IntPtr hIcon = bitmap.GetHicon();
            try
            {
                using var unowned = Icon.FromHandle(hIcon);
                return (Icon)unowned.Clone();
            }
            finally
            {
                DestroyIcon(hIcon);
            }
        }
        
        /// <summary>
        /// Felszabadítja a cached ikonokat
        /// </summary>
        public static void Dispose()
        {
            if (!_initialized) return;
            
            foreach (var icon in _cachedIcons)
            {
                icon?.Dispose();
            }
            Array.Clear(_cachedIcons);
            _initialized = false;
        }
    }
}
