using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace KeyboardLed.Services
{
    /// <summary>
    /// Optimalizált icon helper - előre generált cached ikonokkal (8 állapot)
    /// </summary>
    public static class IconHelper
    {
        // Cached icons - 8 lehetséges állapot (2^3)
        private static readonly Icon[] _cachedIcons = new Icon[8];
        private static bool _initialized;
        
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
            
            for (int i = 0; i < 8; i++)
            {
                bool num = (i & 1) != 0;
                bool caps = (i & 2) != 0;
                bool scroll = (i & 4) != 0;
                _cachedIcons[i] = CreateIconInternal(num, caps, scroll);
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
        {
            if (!_initialized) Initialize();
            int index = (numLock ? 1 : 0) | (capsLock ? 2 : 0) | (scrollLock ? 4 : 0);
            return _cachedIcons[index];
        }
        
        // Legacy compatibility
        public static Icon CreateTrayIcon(bool numLock = true, bool capsLock = false, bool scrollLock = false)
            => GetTrayIcon(numLock, capsLock, scrollLock);
        
        private static Icon CreateIconInternal(bool numLock, bool capsLock, bool scrollLock)
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
            
            g.FillRectangle(numLock ? onBrush : offBrush, x, 2, barWidth, barHeight);
            g.FillRectangle(capsLock ? onBrush : offBrush, x, 2 + barHeight + gap, barWidth, barHeight);
            g.FillRectangle(scrollLock ? onBrush : offBrush, x, 2 + (barHeight + gap) * 2, barWidth, barHeight);
            
            IntPtr hIcon = bitmap.GetHicon();
            return Icon.FromHandle(hIcon);
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
            _initialized = false;
        }
    }
}
