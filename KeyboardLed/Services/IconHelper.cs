using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace KeyboardLed.Services
{
    public static class IconHelper
    {
        /// <summary>
        /// Creates a clean, modern keyboard LED indicator tray icon (16x16)
        /// Shows 3 LED indicators: N, C, S with colors based on state
        /// </summary>
        public static Icon CreateTrayIcon(bool numLock = true, bool capsLock = false, bool scrollLock = false)
        {
            // Create multi-resolution icon for better display
            using var bitmap16 = CreateTrayBitmap(16, numLock, capsLock, scrollLock);
            using var bitmap32 = CreateTrayBitmap(32, numLock, capsLock, scrollLock);
            
            // Use the 16x16 for tray
            IntPtr hIcon = bitmap16.GetHicon();
            return Icon.FromHandle(hIcon);
        }
        
        private static Bitmap CreateTrayBitmap(int size, bool numLock, bool capsLock, bool scrollLock)
        {
            var bitmap = new Bitmap(size, size);
            using var g = Graphics.FromImage(bitmap);
            
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.Transparent);
            
            // Dark background for visibility
            using var bgBrush = new SolidBrush(Color.FromArgb(30, 30, 35));
            g.FillRectangle(bgBrush, 0, 0, size, size);
            
            // LED colors
            var onColor = Color.FromArgb(50, 255, 100);    // Bright green
            var offColor = Color.FromArgb(60, 60, 65);     // Dark gray
            
            if (size == 16)
            {
                // 16x16: Three horizontal bars
                int barHeight = 3;
                int barWidth = 12;
                int x = 2;
                int gap = 2;
                
                // Num Lock bar
                using var numBrush = new SolidBrush(numLock ? onColor : offColor);
                g.FillRectangle(numBrush, x, 2, barWidth, barHeight);
                
                // Caps Lock bar
                using var capsBrush = new SolidBrush(capsLock ? onColor : offColor);
                g.FillRectangle(capsBrush, x, 2 + barHeight + gap, barWidth, barHeight);
                
                // Scroll Lock bar
                using var scrollBrush = new SolidBrush(scrollLock ? onColor : offColor);
                g.FillRectangle(scrollBrush, x, 2 + (barHeight + gap) * 2, barWidth, barHeight);
            }
            else
            {
                // 32x32: Three circles with letters
                int ledSize = 8;
                int y = 12;
                int spacing = 10;
                int startX = 3;
                
                DrawLedCircle(g, startX, y, ledSize, numLock, onColor, offColor);
                DrawLedCircle(g, startX + spacing, y, ledSize, capsLock, onColor, offColor);
                DrawLedCircle(g, startX + spacing * 2, y, ledSize, scrollLock, onColor, offColor);
            }
            
            return bitmap;
        }
        
        private static void DrawLedCircle(Graphics g, int x, int y, int size, bool isOn, Color onColor, Color offColor)
        {
            var color = isOn ? onColor : offColor;
            
            // Glow effect
            if (isOn)
            {
                using var glowBrush = new SolidBrush(Color.FromArgb(80, onColor));
                g.FillEllipse(glowBrush, x - 1, y - 1, size + 2, size + 2);
            }
            
            // Main circle
            using var brush = new SolidBrush(color);
            g.FillEllipse(brush, x, y, size, size);
            
            // Highlight
            if (isOn)
            {
                using var highlightBrush = new SolidBrush(Color.FromArgb(120, 255, 255, 255));
                g.FillEllipse(highlightBrush, x + 1, y + 1, size / 3, size / 3);
            }
        }
    }
}
