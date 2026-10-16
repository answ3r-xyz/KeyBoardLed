using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using KeyboardLed.Models;

namespace KeyboardLed.Services
{
    /// <summary>
    /// Előre generált, cache-elt tálcaikonok (16 állapot) a tálca DPI-jéhez és témájához igazítva
    /// </summary>
    public sealed class TrayIconRenderer : IDisposable
    {
        private static readonly Color DarkBackground = Color.FromArgb(30, 30, 35);
        private static readonly Color DarkOff = Color.FromArgb(70, 70, 76);
        private static readonly Color DarkOn = Color.FromArgb(50, 255, 100);
        private static readonly Color LightBackground = Color.FromArgb(235, 235, 240);
        private static readonly Color LightOff = Color.FromArgb(185, 185, 192);
        private static readonly Color LightOn = Color.FromArgb(20, 170, 70);

        private Icon[] _icons = Array.Empty<Icon>();

        public int Size { get; private set; }
        public bool LightTheme { get; private set; }
        public bool IncludeInsert { get; private set; }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        /// <summary>
        /// Újragenerálja az ikonokat, ha bármelyik paraméter változott.
        /// Visszaadja a régi ikonokat: a hívó dobja el őket, miután a tálca már az újat mutatja.
        /// </summary>
        public Icon[]? Update(int size, bool lightTheme, bool includeInsert)
        {
            if (_icons.Length > 0 && size == Size && lightTheme == LightTheme && includeInsert == IncludeInsert)
            {
                return null;
            }

            var icons = new Icon[KeyboardState.StateCount];
            for (int i = 0; i < icons.Length; i++)
            {
                icons[i] = CreateIcon(KeyboardState.FromIndex(i), size, lightTheme, includeInsert);
            }

            var previous = _icons;
            _icons = icons;
            Size = size;
            LightTheme = lightTheme;
            IncludeInsert = includeInsert;
            return previous;
        }

        /// <summary>
        /// Visszaad egy cached ikont az állapot alapján - NINCS allokáció!
        /// </summary>
        public Icon Get(KeyboardState state) => _icons[state.ToIndex()];

        /// <summary>
        /// LED csíkok elrendezése az ikonon (minden mérethez éles, egész pixeles)
        /// </summary>
        internal static Rectangle[] LayoutBars(int size, int count)
        {
            int marginX = Math.Max(1, size / 8);
            int marginY = Math.Max(1, size / 16);
            int gap = Math.Max(1, size / 12);
            int width = size - 2 * marginX;
            int barHeight = Math.Max(1, (size - 2 * marginY - gap * (count - 1)) / count);
            int total = barHeight * count + gap * (count - 1);
            int top = (size - total) / 2;

            var bars = new Rectangle[count];
            for (int i = 0; i < count; i++)
            {
                bars[i] = new Rectangle(marginX, top + i * (barHeight + gap), width, barHeight);
            }
            return bars;
        }

        private static Icon CreateIcon(KeyboardState state, int size, bool lightTheme, bool includeInsert)
        {
            using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.Clear(lightTheme ? LightBackground : DarkBackground);

                using var onBrush = new SolidBrush(lightTheme ? LightOn : DarkOn);
                using var offBrush = new SolidBrush(lightTheme ? LightOff : DarkOff);

                bool[] values = includeInsert
                    ? new[] { state.NumLock, state.CapsLock, state.ScrollLock, state.Insert }
                    : new[] { state.NumLock, state.CapsLock, state.ScrollLock };

                var bars = LayoutBars(size, values.Length);
                for (int i = 0; i < bars.Length; i++)
                {
                    g.FillRectangle(values[i] ? onBrush : offBrush, bars[i]);
                }
            }

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

        public static void DisposeIcons(Icon[]? icons)
        {
            if (icons == null) return;
            foreach (var icon in icons)
            {
                icon?.Dispose();
            }
        }

        /// <summary>
        /// Felszabadítja a cached ikonokat
        /// </summary>
        public void Dispose()
        {
            DisposeIcons(_icons);
            _icons = Array.Empty<Icon>();
        }
    }
}
