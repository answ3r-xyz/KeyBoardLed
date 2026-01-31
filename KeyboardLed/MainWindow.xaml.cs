using System;
using System.Drawing;
using System.Media;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KeyboardLed.Models;
using KeyboardLed.Services;
using KeyboardLed.Views;

namespace KeyboardLed
{
    public partial class MainWindow : Window
    {
        private Settings _settings;
        private KeyboardHook _keyboardHook;
        private OverlayWindow _overlayWindow;
        private bool _isLoading = true;
        private bool _isDragMode = false;
        private KeyboardState _lastState;
        private System.Windows.Threading.DispatcherTimer _pollTimer;
        
        // Tray menu items
        private MenuItem _trayNumLock = null!;
        private MenuItem _trayCapsLock = null!;
        private MenuItem _trayScrollLock = null!;

        // For simulating key presses
        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private const byte VK_NUMLOCK = 0x90;
        private const byte VK_CAPITAL = 0x14;
        private const byte VK_SCROLL = 0x91;
        private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        public MainWindow()
        {
            InitializeComponent();
            
            // Create and set tray icon
            CreateTrayIcon();
            
            // Load settings
            _settings = Settings.Load();
            _lastState = KeyboardHook.GetCurrentState();
            
            // Initialize overlay
            _overlayWindow = new OverlayWindow(_settings);
            
            // Create tray context menu
            CreateTrayMenu();
            
            // Initialize keyboard hook (for Ctrl+Scroll Lock workaround)
            _keyboardHook = new KeyboardHook();
            _keyboardHook.Start();
            
            // Start polling timer for reliable state detection
            // Csak polling-ot használunk, ez mindig a valós állapotot adja vissza
            _pollTimer = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Send);
            _pollTimer.Interval = TimeSpan.FromMilliseconds(16); // ~60fps, gyors reakció
            _pollTimer.Tick += PollTimer_Tick;
            _pollTimer.Start();
            
            // Load UI from settings
            LoadSettingsToUI();
            
            // Check if started minimized
            var args = Environment.GetCommandLineArgs();
            if (args.Length > 1 && args[1] == "--minimized")
            {
                this.WindowState = WindowState.Minimized;
                this.ShowInTaskbar = false;
                this.Hide();
            }
            
            _isLoading = false;
            
            // Show overlay
            if (_settings.ShowOverlay)
            {
                _overlayWindow.Show();
            }
            
            // Initial state update - force refresh
            _lastState = KeyboardHook.GetCurrentState();
            UpdateStateDisplay(_lastState);
            _overlayWindow.UpdateState(_lastState);
        }

        private void PollTimer_Tick(object? sender, EventArgs e)
        {
            var currentState = KeyboardHook.GetCurrentState();
            
            // Check if state changed
            if (currentState.NumLock != _lastState.NumLock ||
                currentState.CapsLock != _lastState.CapsLock ||
                currentState.ScrollLock != _lastState.ScrollLock)
            {
                // Beep if enabled
                if (_settings.BeepOnChange)
                {
                    System.Media.SystemSounds.Beep.Play();
                }
                
                UpdateStateDisplay(currentState);
                _overlayWindow.UpdateState(currentState);
                _overlayWindow.ShowOverlay();
                
                _lastState = currentState;
            }
        }

        private void CreateTrayMenu()
        {
            var menu = new ContextMenu();
            
            var showItem = new MenuItem { Header = "Show Settings" };
            showItem.Click += TrayMenu_Show_Click;
            menu.Items.Add(showItem);
            
            menu.Items.Add(new Separator());
            
            _trayNumLock = new MenuItem { Header = "Num Lock", IsCheckable = true };
            _trayNumLock.Click += TrayMenu_NumLock_Click;
            menu.Items.Add(_trayNumLock);
            
            _trayCapsLock = new MenuItem { Header = "Caps Lock", IsCheckable = true };
            _trayCapsLock.Click += TrayMenu_CapsLock_Click;
            menu.Items.Add(_trayCapsLock);
            
            _trayScrollLock = new MenuItem { Header = "Scroll Lock", IsCheckable = true };
            _trayScrollLock.Click += TrayMenu_ScrollLock_Click;
            menu.Items.Add(_trayScrollLock);
            
            menu.Items.Add(new Separator());
            
            var exitItem = new MenuItem { Header = "Exit" };
            exitItem.Click += TrayMenu_Exit_Click;
            menu.Items.Add(exitItem);
            
            TrayIcon.ContextMenu = menu;
        }

        private void CreateTrayIcon()
        {
            // Create an initial icon with current state
            var state = KeyboardHook.GetCurrentState();
            TrayIcon.Icon = IconHelper.CreateTrayIcon(state.NumLock, state.CapsLock, state.ScrollLock);
        }
        
        private void UpdateTrayIcon(KeyboardState state)
        {
            // Update tray icon to reflect current LED states
            var oldIcon = TrayIcon.Icon;
            TrayIcon.Icon = IconHelper.CreateTrayIcon(state.NumLock, state.CapsLock, state.ScrollLock);
            oldIcon?.Dispose();
        }

        private void LoadSettingsToUI()
        {
            ChkAutoStart.IsChecked = AutoStartManager.IsAutoStartEnabled();
            ChkBeepOnChange.IsChecked = _settings.BeepOnChange;
            ChkShowOverlay.IsChecked = _settings.ShowOverlay;
            ChkOsdNumLock.IsChecked = _settings.OverlayShowNumLock;
            ChkOsdCapsLock.IsChecked = _settings.OverlayShowCapsLock;
            ChkOsdScrollLock.IsChecked = _settings.OverlayShowScrollLock;
            ChkHideWhenAllOff.IsChecked = _settings.HideWhenAllOff;
            ChkHideAfterSec.IsChecked = _settings.HideAfterSeconds;
            TxtHideAfterSec.Text = _settings.HideAfterSecondsValue.ToString();
            SliderOpacity.Value = _settings.OverlayOpacity;
            TxtPosX.Text = _settings.OverlayX.ToString();
            TxtPosY.Text = _settings.OverlayY.ToString();
            
            // Size combo
            foreach (ComboBoxItem item in CmbSize.Items)
            {
                if (item.Content?.ToString() == _settings.OverlaySize)
                {
                    CmbSize.SelectedItem = item;
                    break;
                }
            }
            
            // Body color combo
            foreach (ComboBoxItem item in CmbBodyColor.Items)
            {
                if (item.Tag?.ToString() == _settings.OverlayBodyColor)
                {
                    CmbBodyColor.SelectedItem = item;
                    break;
                }
            }
            
            // Text color combo
            foreach (ComboBoxItem item in CmbTextColor.Items)
            {
                if (item.Tag?.ToString() == _settings.OverlayTextColor)
                {
                    CmbTextColor.SelectedItem = item;
                    break;
                }
            }
            
            // Custom names
            TxtNumLockName.Text = _settings.NumLockName;
            TxtCapsLockName.Text = _settings.CapsLockName;
            TxtScrollLockName.Text = _settings.ScrollLockName;
        }

        private void UpdateStateDisplay(KeyboardState state)
        {
            var onBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#32CD32"));
            var offBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF6B6B"));
            
            NumLockState.Background = state.NumLock ? onBrush : offBrush;
            CapsLockState.Background = state.CapsLock ? onBrush : offBrush;
            ScrollLockState.Background = state.ScrollLock ? onBrush : offBrush;
            
            BtnNumLock.Content = state.NumLock ? "🟢 Num Lock" : "⚪ Num Lock";
            BtnCapsLock.Content = state.CapsLock ? "🟢 Caps Lock" : "⚪ Caps Lock";
            BtnScrollLock.Content = state.ScrollLock ? "🟢 Scroll Lock" : "⚪ Scroll Lock";
            
            // Update tray icon with current state
            UpdateTrayIcon(state);
            
            // Update tray menu
            _trayNumLock.IsChecked = state.NumLock;
            _trayCapsLock.IsChecked = state.CapsLock;
            _trayScrollLock.IsChecked = state.ScrollLock;
            
            // Update tray tooltip
            TrayIcon.ToolTipText = $"Keyboard LEDs\nNum: {(state.NumLock ? "ON" : "OFF")}\nCaps: {(state.CapsLock ? "ON" : "OFF")}\nScroll: {(state.ScrollLock ? "ON" : "OFF")}";
        }

        private void SaveSettings()
        {
            if (_isLoading) return;
            
            _settings.BeepOnChange = ChkBeepOnChange.IsChecked ?? false;
            _settings.ShowOverlay = ChkShowOverlay.IsChecked ?? true;
            _settings.OverlayShowNumLock = ChkOsdNumLock.IsChecked ?? true;
            _settings.OverlayShowCapsLock = ChkOsdCapsLock.IsChecked ?? true;
            _settings.OverlayShowScrollLock = ChkOsdScrollLock.IsChecked ?? true;
            _settings.HideWhenAllOff = ChkHideWhenAllOff.IsChecked ?? true;
            _settings.HideAfterSeconds = ChkHideAfterSec.IsChecked ?? false;
            
            if (int.TryParse(TxtHideAfterSec.Text, out int hideSeconds))
            {
                _settings.HideAfterSecondsValue = hideSeconds;
            }
            
            _settings.OverlayOpacity = (int)SliderOpacity.Value;
            
            if (CmbSize.SelectedItem is ComboBoxItem sizeItem)
            {
                _settings.OverlaySize = sizeItem.Content?.ToString() ?? "Normal";
            }
            
            if (CmbBodyColor.SelectedItem is ComboBoxItem bodyColorItem)
            {
                _settings.OverlayBodyColor = bodyColorItem.Tag?.ToString() ?? "#32CD32";
            }
            
            if (CmbTextColor.SelectedItem is ComboBoxItem textColorItem)
            {
                _settings.OverlayTextColor = textColorItem.Tag?.ToString() ?? "#FFFFFF";
            }
            
            if (int.TryParse(TxtPosX.Text, out int posX))
            {
                _settings.OverlayX = posX;
            }
            
            if (int.TryParse(TxtPosY.Text, out int posY))
            {
                _settings.OverlayY = posY;
            }
            
            // Custom names
            _settings.NumLockName = TxtNumLockName.Text;
            _settings.CapsLockName = TxtCapsLockName.Text;
            _settings.ScrollLockName = TxtScrollLockName.Text;
            
            _settings.Save();
            
            // Update overlay
            _overlayWindow.UpdateSettings(_settings);
            _overlayWindow.UpdateState(KeyboardHook.GetCurrentState());
            
            if (_settings.ShowOverlay)
            {
                _overlayWindow.Show();
            }
            else
            {
                _overlayWindow.Hide();
            }
        }

        private void Settings_Changed(object sender, RoutedEventArgs e)
        {
            SaveSettings();
        }

        private void Settings_Changed(object sender, TextChangedEventArgs e)
        {
            SaveSettings();
        }

        private void Settings_Changed(object sender, SelectionChangedEventArgs e)
        {
            SaveSettings();
        }

        private void SliderOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtOpacity != null)
            {
                TxtOpacity.Text = $"{(int)SliderOpacity.Value}%";
            }
            SaveSettings();
        }

        private void Position_Changed(object sender, TextChangedEventArgs e)
        {
            SaveSettings();
        }

        private void ChkAutoStart_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            AutoStartManager.SetAutoStart(ChkAutoStart.IsChecked ?? false);
            _settings.AutoStart = ChkAutoStart.IsChecked ?? false;
            _settings.Save();
        }

        private void BtnDragOverlay_Click(object sender, RoutedEventArgs e)
        {
            if (!_isDragMode)
            {
                _isDragMode = true;
                _overlayWindow.SetClickThrough(false);
                _overlayWindow.Show();
                MessageBox.Show("Drag the overlay to your desired position.\nClick OK when done.", 
                    "Position Overlay", MessageBoxButton.OK, MessageBoxImage.Information);
                
                // Get new position
                var (x, y) = _overlayWindow.GetPosition();
                TxtPosX.Text = x.ToString();
                TxtPosY.Text = y.ToString();
                
                _overlayWindow.SetClickThrough(true);
                _isDragMode = false;
            }
        }

        private void ToggleKey(byte vkCode)
        {
            keybd_event(vkCode, 0x45, KEYEVENTF_EXTENDEDKEY, UIntPtr.Zero);
            keybd_event(vkCode, 0x45, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        private void BtnNumLock_Click(object sender, RoutedEventArgs e)
        {
            ToggleKey(VK_NUMLOCK);
        }

        private void BtnCapsLock_Click(object sender, RoutedEventArgs e)
        {
            ToggleKey(VK_CAPITAL);
        }

        private void BtnScrollLock_Click(object sender, RoutedEventArgs e)
        {
            ToggleKey(VK_SCROLL);
        }

        private void TrayMenu_NumLock_Click(object sender, RoutedEventArgs e)
        {
            ToggleKey(VK_NUMLOCK);
        }

        private void TrayMenu_CapsLock_Click(object sender, RoutedEventArgs e)
        {
            ToggleKey(VK_CAPITAL);
        }

        private void TrayMenu_ScrollLock_Click(object sender, RoutedEventArgs e)
        {
            ToggleKey(VK_SCROLL);
        }

        private void TrayMenu_Show_Click(object sender, RoutedEventArgs e)
        {
            this.Show();
            this.WindowState = WindowState.Normal;
            this.ShowInTaskbar = true;
            this.Activate();
        }

        private void TrayMenu_Exit_Click(object sender, RoutedEventArgs e)
        {
            _pollTimer.Stop();
            _keyboardHook.Dispose();
            _overlayWindow.Close();
            TrayIcon.Dispose();
            Application.Current.Shutdown();
        }

        private void TrayIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e)
        {
            TrayMenu_Show_Click(sender, e);
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                this.Hide();
                this.ShowInTaskbar = false;
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Minimize to tray instead of closing
            e.Cancel = true;
            this.WindowState = WindowState.Minimized;
            this.Hide();
            this.ShowInTaskbar = false;
        }
    }
}
