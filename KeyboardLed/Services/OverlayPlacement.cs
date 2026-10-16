using System;
using KeyboardLed.Models;

namespace KeyboardLed.Services
{
    /// <summary>
    /// Téglalap fizikai pixelben (Right/Bottom exkluzív, mint a Win32 RECT)
    /// </summary>
    public readonly record struct PixelRect(int Left, int Top, int Right, int Bottom)
    {
        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    /// <summary>
    /// Overlay pozíció számítás (tiszta függvények, tesztelhető).
    /// Az eltolás a választott saroktól/éltől befelé értendő; középre igazításnál pozitív = jobbra/lefelé.
    /// </summary>
    public static class OverlayPlacement
    {
        public static (int X, int Y) Compute(PixelRect workArea, int width, int height,
            OverlayAnchor anchor, int offsetX, int offsetY)
        {
            int x = IsLeft(anchor) ? workArea.Left + offsetX
                : IsRight(anchor) ? workArea.Right - width - offsetX
                : workArea.Left + (workArea.Width - width) / 2 + offsetX;

            int y = IsTop(anchor) ? workArea.Top + offsetY
                : workArea.Bottom - height - offsetY;

            return Clamp(workArea, width, height, x, y);
        }

        /// <summary>
        /// A Compute inverze: egy adott pozícióhoz tartozó eltolás
        /// </summary>
        public static (int OffsetX, int OffsetY) ComputeOffsets(PixelRect workArea, int x, int y,
            int width, int height, OverlayAnchor anchor)
        {
            int offsetX = IsLeft(anchor) ? x - workArea.Left
                : IsRight(anchor) ? workArea.Right - width - x
                : x - (workArea.Left + (workArea.Width - width) / 2);

            int offsetY = IsTop(anchor) ? y - workArea.Top
                : workArea.Bottom - height - y;

            return (offsetX, offsetY);
        }

        /// <summary>
        /// A képernyőn kívülre került (pl. lecsatolt monitor) overlay visszahúzása a munkaterületre
        /// </summary>
        public static (int X, int Y) Clamp(PixelRect workArea, int width, int height, int x, int y)
        {
            x = Math.Max(Math.Min(x, workArea.Right - width), workArea.Left);
            y = Math.Max(Math.Min(y, workArea.Bottom - height), workArea.Top);
            return (x, y);
        }

        private static bool IsLeft(OverlayAnchor a) => a == OverlayAnchor.TopLeft || a == OverlayAnchor.BottomLeft;

        private static bool IsRight(OverlayAnchor a) => a == OverlayAnchor.TopRight || a == OverlayAnchor.BottomRight;

        private static bool IsTop(OverlayAnchor a) =>
            a == OverlayAnchor.TopLeft || a == OverlayAnchor.TopCenter || a == OverlayAnchor.TopRight || a == OverlayAnchor.Custom;
    }
}
