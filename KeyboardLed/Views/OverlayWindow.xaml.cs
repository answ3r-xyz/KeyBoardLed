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
        
        // For click-through when not dragging
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_TOOLWINDOW = 0x00000080;  // Hide from Alt+Tab
        private const int GWL_EXSTYLE = -20;
        
        // For hiding from screen capture (NVIDIA, OBS, etc.)
        private const int WDA_EXCLUDEFROMCAPTURE = 0x00000011;

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);
        
        [DllImport("user32.dll")]
        private static extern bool SetWindowDisplayAffinity(IntPtr hwnd, int dwAffinity);

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
            var hwnd = new WindowInteropHelper(this).Handle;
            
            // Make window click-through and hide from Alt+Tab
            var extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW);
            
            // Hide from screen capture (NVIDIA ShadowPlay, OBS, etc.)
            // This requires Windows 10 version 2004 or later
            try
            {
                SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE);
            }
            catch
            {
                // Older Windows version, ignore
            }
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
                NumLockText.Text = state.NumLock ? $"{_settings.NumLockName} ON" : $"{_settings.NumLockName} OFF";
                NumLockIndicator.Visibility = Visibility.Visible;
            }
            
            // Update Caps Lock
            if (_settings.OverlayShowCapsLock)
            {
                CapsLockIndicator.Background = state.CapsLock ? onBrush : offBrush;
                CapsLockText.Text = state.CapsLock ? $"{_settings.CapsLockName} ON" : $"{_settings.CapsLockName} OFF";
                CapsLockIndicator.Visibility = Visibility.Visible;
            }
            
            // Update Scroll Lock
            if (_settings.OverlayShowScrollLock)
            {
                ScrollLockIndicator.Background = state.ScrollLock ? onBrush : offBrush;
                ScrollLockText.Text = state.ScrollLock ? $"{_settings.ScrollLockName} ON" : $"{_settings.ScrollLockName} OFF";
                ScrollLockIndicator.Visibility = Visibility.Visible;
            }
            
            // Hide when all OFF - only check the ones that are enabled to show
            bool numOff = !_settings.OverlayShowNumLock || !state.NumLock;
            bool capsOff = !_settings.OverlayShowCapsLock || !state.CapsLock;
            bool scrollOff = !_settings.OverlayShowScrollLock || !state.ScrollLock;
            bool allOff = numOff && capsOff && scrollOff;
            
            if (_settings.HideWhenAllOff && allOff)
            {
                this.Hide();
            }
            else if (_settings.ShowOverlay)
            {
                this.Show();
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
                DragMove();
                
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
