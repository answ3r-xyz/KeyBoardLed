using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using KeyboardLed.Models;
using KeyboardLed.Services;

namespace KeyboardLed.Views
{
    public partial class OverlayWindow : Window
    {
        private static readonly Duration FadeInDuration = new(TimeSpan.FromMilliseconds(120));
        private static readonly Duration FadeOutDuration = new(TimeSpan.FromMilliseconds(250));

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;  // click-through
        private const int WS_EX_TOOLWINDOW = 0x00000080;   // Hide from Alt+Tab
        private const int WS_EX_NOACTIVATE = 0x08000000;   // ne vegye el a fókuszt megjelenéskor
        private const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011; // Hide from screen capture
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;
        private static readonly IntPtr HWND_TOPMOST = new(-1);

        private readonly Settings _settings;
        private readonly Dictionary<LockKey, (Border Box, TextBlock Text)> _indicators = new();
        private readonly DispatcherTimer _hideTimer = new();

        // Cached brushes - only recreated when settings change
        private SolidColorBrush _onBrush = Brushes.LimeGreen;
        private SolidColorBrush _offBrush = Brushes.IndianRed;

        private IntPtr _hwnd;
        private bool _dragging;
        private bool _wantVisible;

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowDisplayAffinity(IntPtr hwnd, uint dwAffinity);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(IntPtr hwnd, IntPtr hwndInsertAfter, int x, int y, int cx, int cy, uint flags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hwnd, out DisplayInfo.RECT rect);

        public OverlayWindow(Settings settings)
        {
            InitializeComponent();
            _settings = settings;

            foreach (var key in LockKeys.All)
            {
                var text = new TextBlock { FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
                var box = new Border
                {
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(10, 4, 10, 4),
                    Margin = new Thickness(2),
                    Child = text
                };
                IndicatorPanel.Children.Add(box);
                _indicators[key] = (box, text);
            }

            _hideTimer.Tick += HideTimer_Tick;
            SourceInitialized += OnSourceInitialized;
            SizeChanged += (_, _) => RefreshPosition();
            DpiChanged += (_, _) => Dispatcher.BeginInvoke(new Action(RefreshPosition), DispatcherPriority.Loaded);

            ApplySettings();

            // HWND létrehozása megjelenítés nélkül: az ablakstílusok már az első megjelenés előtt érvényesek
            new WindowInteropHelper(this).EnsureHandle();
        }

        private double TargetOpacity => _settings.OverlayOpacity / 100.0;

        private void OnSourceInitialized(object? sender, EventArgs e)
        {
            _hwnd = new WindowInteropHelper(this).Handle;

            // Make window click-through, hide from Alt+Tab, never steal focus
            int extendedStyle = GetWindowLong(_hwnd, GWL_EXSTYLE);
            SetWindowLong(_hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);

            // Hide from screen capture (NVIDIA ShadowPlay, OBS, etc.) - Windows 10 2004 or later
            if (!SetWindowDisplayAffinity(_hwnd, WDA_EXCLUDEFROMCAPTURE))
            {
                Log.Info($"Overlay capture exclusion not available (error {Marshal.GetLastWin32Error()})");
            }
        }

        public void ApplySettings()
        {
            double fontSize = _settings.OverlaySize switch
            {
                OverlaySize.Small => 9,
                OverlaySize.Large => 14,
                _ => 11
            };

            var textBrush = CreateBrush(_settings.OverlayTextColor, Settings.DefaultTextColor);
            _onBrush = CreateBrush(_settings.OverlayBodyColor, Settings.DefaultBodyColor);
            _offBrush = CreateBrush(_settings.OverlayOffColor, Settings.DefaultOffColor);

            foreach (var indicator in _indicators.Values)
            {
                indicator.Text.FontSize = fontSize;
                indicator.Text.Foreground = textBrush;
            }

            if (_wantVisible && IsVisible)
            {
                AnimateOpacity(TargetOpacity, FadeInDuration, null);
            }
        }

        /// <param name="popup">Felugró (automatikus elrejtés) módban jelenjen-e meg</param>
        public void UpdateState(KeyboardState state, bool popup)
        {
            bool anyShown = false;
            bool allOff = true;

            foreach (var key in LockKeys.All)
            {
                var (box, text) = _indicators[key];
                bool shown = _settings.Keys[key].ShowInOverlay && (key != LockKey.Insert || _settings.TrackInsert);
                box.Visibility = shown ? Visibility.Visible : Visibility.Collapsed;
                if (!shown) continue;

                bool on = state[key];
                box.Background = on ? _onBrush : _offBrush;
                text.Text = $"{_settings.GetDisplayName(key)} {Loc.T(on ? "On" : "Off")}";
                anyShown = true;
                allOff &= !on;
            }

            if (_dragging) return;

            // Hide when all OFF - only check the ones that are enabled to show
            bool visible = _settings.ShowOverlay && anyShown && !(_settings.HideWhenAllOff && allOff);
            if (!visible)
            {
                _hideTimer.Stop();
                FadeOut();
            }
            else if (!_settings.HideAfterSeconds)
            {
                // Állandó mód
                _hideTimer.Stop();
                FadeIn();
            }
            else if (popup)
            {
                // Felugró mód: megjelenik, majd X mp múlva eltűnik
                FadeIn();
                RestartHideTimer();
            }
        }

        public void RefreshPosition()
        {
            if (_hwnd == IntPtr.Zero || _dragging) return;

            var (width, height) = GetPixelSize();
            if (width <= 0 || height <= 0) return;

            int x, y;
            if (_settings.OverlayAnchor == OverlayAnchor.Custom)
            {
                // Pontos pozíció, de lecsatolt monitor esetén visszahúzzuk a legközelebbi monitorra
                var area = DisplayInfo.GetMonitorAreaNearest(_settings.OverlayCustomX, _settings.OverlayCustomY);
                (x, y) = OverlayPlacement.Clamp(area, width, height, _settings.OverlayCustomX, _settings.OverlayCustomY);
            }
            else
            {
                var workArea = DisplayInfo.GetWorkArea(_settings.OverlayMonitor);
                (x, y) = OverlayPlacement.Compute(workArea, width, height,
                    _settings.OverlayAnchor, _settings.OverlayOffsetX, _settings.OverlayOffsetY);
            }

            SetWindowPos(_hwnd, HWND_TOPMOST, x, y, 0, 0, SWP_NOSIZE | SWP_NOACTIVATE);
        }

        public void BeginDrag()
        {
            _hideTimer.Stop();
            FadeIn();
            _dragging = true;
            SetClickThrough(false);
        }

        /// <summary>
        /// Visszaadja az új pozíciót a horgonynak megfelelő formában (eltolás vagy abszolút X/Y)
        /// </summary>
        public (int X, int Y)? EndDrag(OverlayAnchor anchor)
        {
            SetClickThrough(true);
            _dragging = false;

            if (_hwnd == IntPtr.Zero || !GetWindowRect(_hwnd, out var r))
            {
                return null;
            }

            var rect = new PixelRect(r.Left, r.Top, r.Right, r.Bottom);
            if (anchor == OverlayAnchor.Custom)
            {
                return (rect.Left, rect.Top);
            }

            var workArea = DisplayInfo.GetWorkAreaForRect(rect);
            return OverlayPlacement.ComputeOffsets(workArea, rect.Left, rect.Top, rect.Width, rect.Height, anchor);
        }

        private void SetClickThrough(bool clickThrough)
        {
            if (_hwnd == IntPtr.Zero) return;

            int extendedStyle = GetWindowLong(_hwnd, GWL_EXSTYLE);
            SetWindowLong(_hwnd, GWL_EXSTYLE, clickThrough
                ? extendedStyle | WS_EX_TRANSPARENT
                : extendedStyle & ~WS_EX_TRANSPARENT);
        }

        private (int Width, int Height) GetPixelSize()
        {
            if (IsVisible && GetWindowRect(_hwnd, out var r) && r.Right > r.Left)
            {
                return (r.Right - r.Left, r.Bottom - r.Top);
            }

            // Még nem látszik: becslés a tartalom méretéből (megjelenés után a SizeChanged pontosít)
            Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var dpi = VisualTreeHelper.GetDpi(this);
            return ((int)Math.Ceiling(DesiredSize.Width * dpi.DpiScaleX), (int)Math.Ceiling(DesiredSize.Height * dpi.DpiScaleY));
        }

        private void FadeIn()
        {
            _wantVisible = true;
            if (!IsVisible)
            {
                if (_settings.FadeAnimation)
                {
                    BeginAnimation(OpacityProperty, null);
                    Opacity = 0;
                }
                RefreshPosition();
                Show();
            }
            AnimateOpacity(TargetOpacity, FadeInDuration, null);
            RefreshPosition();
        }

        private void FadeOut()
        {
            _wantVisible = false;
            if (!IsVisible) return;

            AnimateOpacity(0, FadeOutDuration, () =>
            {
                // Közben újra megjelenítést kérhettek
                if (!_wantVisible) Hide();
            });
        }

        private void AnimateOpacity(double to, Duration duration, Action? completed)
        {
            if (!_settings.FadeAnimation)
            {
                BeginAnimation(OpacityProperty, null);
                Opacity = to;
                completed?.Invoke();
                return;
            }

            var animation = new DoubleAnimation(to, duration) { FillBehavior = FillBehavior.HoldEnd };
            if (completed != null)
            {
                animation.Completed += (_, _) => completed();
            }
            BeginAnimation(OpacityProperty, animation);
        }

        private void RestartHideTimer()
        {
            _hideTimer.Stop();
            _hideTimer.Interval = TimeSpan.FromSeconds(
                Math.Clamp(_settings.HideAfterSecondsValue, Settings.MinHideSeconds, Settings.MaxHideSeconds));
            _hideTimer.Start();
        }

        private void HideTimer_Tick(object? sender, EventArgs e)
        {
            _hideTimer.Stop();
            if (!_dragging)
            {
                FadeOut();
            }
        }

        private static SolidColorBrush CreateBrush(string hex, string fallback)
        {
            Color color;
            try
            {
                color = (Color)ColorConverter.ConvertFromString(hex);
            }
            catch (Exception ex) when (ex is FormatException || ex is NotSupportedException)
            {
                color = (Color)ColorConverter.ConvertFromString(fallback);
            }

            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // This is only triggered when click-through is disabled (during position adjustment)
            if (_dragging && e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _hideTimer.Stop();
            base.OnClosed(e);
        }
    }
}
