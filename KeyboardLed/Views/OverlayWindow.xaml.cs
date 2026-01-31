using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using KeyboardLed.Models;
using KeyboardLed.Services;

namespace KeyboardLed.Views
{
    public partial class OverlayWindow : Window
    {
        private Settings _settings;
        private DispatcherTimer? _hideTimer;
        private bool _isDragging = false;
        
        // For click-through when not dragging
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int GWL_EXSTYLE = -20;

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        public OverlayWindow(Settings settings)
        {
            InitializeComponent();
            _settings = settings;
            
            this.Loaded += OverlayWindow_Loaded;
            this.SourceInitialized += OverlayWindow_SourceInitialized;
            
            ApplySettings();
        }

        private void OverlayWindow_SourceInitialized(object? sender, EventArgs e)
        {
            // Make window click-through by default
            SetClickThrough(true);
        }

        private void OverlayWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Set position
            this.Left = _settings.OverlayX;
            this.Top = _settings.OverlayY;
            
            // Initial state update
            UpdateState(KeyboardHook.GetCurrentState());
        }

        public void SetClickThrough(bool clickThrough)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;
            
            var extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            if (clickThrough)
            {
                SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT);
            }
            else
            {
                SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle & ~WS_EX_TRANSPARENT);
            }
        }

        public void UpdateSettings(Settings settings)
        {
            _settings = settings;
            ApplySettings();
            
            // Update position
            this.Left = _settings.OverlayX;
            this.Top = _settings.OverlayY;
        }

        private void ApplySettings()
        {
            // Apply opacity
            this.Opacity = _settings.OverlayOpacity / 100.0;
            
            // Apply size
            double fontSize = _settings.OverlaySize switch
            {
                "Small" => 9,
                "Large" => 14,
                _ => 11
            };
            
            NumLockText.FontSize = fontSize;
            CapsLockText.FontSize = fontSize;
            ScrollLockText.FontSize = fontSize;
            
            // Apply text color
            var textBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_settings.OverlayTextColor));
            NumLockText.Foreground = textBrush;
            CapsLockText.Foreground = textBrush;
            ScrollLockText.Foreground = textBrush;
            
            // Visibility settings
            NumLockIndicator.Visibility = _settings.OverlayShowNumLock ? Visibility.Visible : Visibility.Collapsed;
            CapsLockIndicator.Visibility = _settings.OverlayShowCapsLock ? Visibility.Visible : Visibility.Collapsed;
            ScrollLockIndicator.Visibility = _settings.OverlayShowScrollLock ? Visibility.Visible : Visibility.Collapsed;
        }

        public void UpdateState(KeyboardState state)
        {
            var onBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_settings.OverlayBodyColor));
            var offBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_settings.OverlayOffColor));
            
            // Update Num Lock
            if (_settings.OverlayShowNumLock)
            {
                NumLockIndicator.Background = state.NumLock ? onBrush : offBrush;
                NumLockText.Text = state.NumLock ? "Num Lock ON" : "Num Lock OFF";
                NumLockIndicator.Visibility = Visibility.Visible;
            }
            
            // Update Caps Lock
            if (_settings.OverlayShowCapsLock)
            {
                CapsLockIndicator.Background = state.CapsLock ? onBrush : offBrush;
                CapsLockText.Text = state.CapsLock ? "Caps Lock ON" : "Caps Lock OFF";
                CapsLockIndicator.Visibility = Visibility.Visible;
            }
            
            // Update Scroll Lock
            if (_settings.OverlayShowScrollLock)
            {
                ScrollLockIndicator.Background = state.ScrollLock ? onBrush : offBrush;
                ScrollLockText.Text = state.ScrollLock ? "Scroll Lock ON" : "Scroll Lock OFF";
                ScrollLockIndicator.Visibility = Visibility.Visible;
            }
            
            // Hide when all OFF
            bool allOff = !state.NumLock && !state.CapsLock && !state.ScrollLock;
            
            if (_settings.HideWhenAllOff && allOff)
            {
                this.Visibility = Visibility.Hidden;
            }
            else
            {
                this.Visibility = _settings.ShowOverlay ? Visibility.Visible : Visibility.Hidden;
            }
            
            // Auto-hide timer
            if (_settings.HideAfterSeconds && _settings.ShowOverlay)
            {
                StartHideTimer();
            }
        }

        private void StartHideTimer()
        {
            _hideTimer?.Stop();
            _hideTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(_settings.HideAfterSecondsValue)
            };
            _hideTimer.Tick += (s, e) =>
            {
                _hideTimer?.Stop();
                this.Visibility = Visibility.Hidden;
            };
            _hideTimer.Start();
        }

        public void ShowOverlay()
        {
            if (_settings.ShowOverlay)
            {
                this.Visibility = Visibility.Visible;
                if (_settings.HideAfterSeconds)
                {
                    StartHideTimer();
                }
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // This is only triggered when click-through is disabled (during position adjustment)
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                _isDragging = true;
                DragMove();
                _isDragging = false;
                
                // Save new position
                _settings.OverlayX = (int)this.Left;
                _settings.OverlayY = (int)this.Top;
            }
        }

        public (int X, int Y) GetPosition()
        {
            return ((int)this.Left, (int)this.Top);
        }

        public void SetPosition(int x, int y)
        {
            this.Left = x;
            this.Top = y;
            _settings.OverlayX = x;
            _settings.OverlayY = y;
        }
    }
}
