using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using KeyboardLed.Models;
using KeyboardLed.Services;
using KeyboardLed.ViewModels;
using KeyboardLed.Views;
using Microsoft.Win32;

namespace KeyboardLed
{
    public partial class MainWindow : Window
    {
        private const int MaxTooltipLength = 127; // NOTIFYICONDATA.szTip

        private readonly Settings _settings;
        private readonly MainViewModel _vm;
        private readonly StateMonitor _monitor;
        private readonly OverlayWindow _overlay;
        private readonly TrayIconRenderer _trayIcons = new();
        private readonly Dictionary<LockKey, MenuItem> _trayKeyItems = new();
        private MenuItem _trayShowItem = null!;
        private MenuItem _trayExitItem = null!;
        private bool _exiting;
        private bool _cleanedUp;

        public MainWindow(Settings settings)
        {
            InitializeComponent();

            _settings = settings;
            _vm = new MainViewModel(settings);
            DataContext = _vm;

            _monitor = new StateMonitor(settings);
            _overlay = new OverlayWindow(settings);

            CreateTrayMenu();
            ApplyAppearance();
            ApplyTrayVisibility();

            _vm.SettingsChanged += OnSettingsChanged;
            _monitor.StateChanged += OnStateChanged;
            Loc.Instance.LanguageChanged += OnLanguageChanged;
            ThemeManager.ThemeChanged += OnThemeChanged;
            SystemEvents.UserPreferenceChanged += OnSystemAppearanceChanged;
            SystemEvents.DisplaySettingsChanged += OnSystemAppearanceChanged;

            _monitor.Start();
            ShowState(_monitor.Current);
            _overlay.UpdateState(_monitor.Current, popup: true);
        }

        public void ShowSettingsWindow()
        {
            Show();
            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }
            ShowInTaskbar = true;
            Activate();
        }

        /// <summary>
        /// Erőforrások felszabadítása kilépéskor (többször is hívható)
        /// </summary>
        public void Cleanup()
        {
            if (_cleanedUp) return;
            _cleanedUp = true;
            _exiting = true;

            _vm.FlushSave();

            SystemEvents.UserPreferenceChanged -= OnSystemAppearanceChanged;
            SystemEvents.DisplaySettingsChanged -= OnSystemAppearanceChanged;
            ThemeManager.ThemeChanged -= OnThemeChanged;
            Loc.Instance.LanguageChanged -= OnLanguageChanged;

            _monitor.Dispose();
            _overlay.Close();
            TrayIcon.Dispose();
            _trayIcons.Dispose();
        }

        private void OnStateChanged(KeyboardState previous, KeyboardState current)
        {
            ShowState(current);

            var changed = LockKeys.All.Where(k => previous[k] != current[k]).ToList();

            // Több egyidejű változásnál csak egy hang szól
            foreach (var key in changed)
            {
                if (_settings.Keys[key].Sound != KeySound.None)
                {
                    SoundService.Play(_settings.Keys[key]);
                    break;
                }
            }

            bool popup = changed.Any(k => _settings.Keys[k].PopupOnChange);
            _overlay.UpdateState(current, popup);
        }

        private void OnSettingsChanged()
        {
            Loc.Instance.SetLanguage(_settings.Language);
            ThemeManager.Apply(_settings.Theme);
            ApplyTrayVisibility();
            UpdateTrayIconImages();

            // Insert figyelés vagy kényszerítés változhatott
            _monitor.Refresh();
            ShowState(_monitor.Current);

            // Előnézet: beállítás változáskor az overlay is felugrik
            _overlay.ApplySettings();
            _overlay.UpdateState(_monitor.Current, popup: true);
        }

        private void OnLanguageChanged()
        {
            ShowState(_monitor.Current);
            _overlay.UpdateState(_monitor.Current, popup: false);
        }

        private void OnThemeChanged() => ThemeManager.ApplyTitleBar(this);

        private void OnSystemAppearanceChanged(object? sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_cleanedUp) return;
                ApplyAppearance();
                _overlay.RefreshPosition();
            }));
        }

        private void ApplyAppearance()
        {
            ThemeManager.Apply(_settings.Theme);
            UpdateTrayIconImages();
        }

        private void ShowState(KeyboardState state)
        {
            _vm.UpdateState(state);
            TrayIcon.Icon = _trayIcons.Get(state);
            UpdateTrayMenu(state);
            UpdateTrayTooltip(state);
        }

        private void UpdateTrayIconImages()
        {
            var previous = _trayIcons.Update(DisplayInfo.GetTrayIconSize(), ThemeManager.IsTaskbarLight(), _settings.TrackInsert);
            if (previous == null) return;

            // Előbb az új ikon kerüljön a tálcára, csak utána dobjuk el a régieket
            TrayIcon.Icon = _trayIcons.Get(_monitor.Current);
            TrayIconRenderer.DisposeIcons(previous);
        }

        private void ApplyTrayVisibility()
        {
            TrayIcon.Visibility = _settings.ShowTrayIcon ? Visibility.Visible : Visibility.Collapsed;
        }

        private bool IsTracked(LockKey key) => key != LockKey.Insert || _settings.TrackInsert;

        private void CreateTrayMenu()
        {
            var menu = new ContextMenu();

            _trayShowItem = new MenuItem();
            _trayShowItem.Click += (_, _) => ShowSettingsWindow();
            menu.Items.Add(_trayShowItem);

            menu.Items.Add(new Separator());

            foreach (var key in LockKeys.All)
            {
                var item = new MenuItem();
                var toggledKey = key;
                item.Click += (_, _) => KeyboardInput.Toggle(toggledKey);
                _trayKeyItems[key] = item;
                menu.Items.Add(item);
            }

            menu.Items.Add(new Separator());

            _trayExitItem = new MenuItem();
            _trayExitItem.Click += (_, _) => ExitApplication();
            menu.Items.Add(_trayExitItem);

            TrayIcon.ContextMenu = menu;
        }

        private void UpdateTrayMenu(KeyboardState state)
        {
            _trayShowItem.Header = Loc.T("TrayShow");
            _trayExitItem.Header = Loc.T("TrayExit");

            foreach (var (key, item) in _trayKeyItems)
            {
                item.Header = _settings.GetDisplayName(key);
                item.IsChecked = state[key];
                item.Visibility = IsTracked(key) ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void UpdateTrayTooltip(KeyboardState state)
        {
            var lines = new List<string> { "Keyboard LEDs" };
            foreach (var key in LockKeys.All.Where(IsTracked))
            {
                lines.Add($"{_settings.GetDisplayName(key)}: {Loc.T(state[key] ? "On" : "Off")}");
            }

            var text = string.Join("\n", lines);
            TrayIcon.ToolTipText = text.Length > MaxTooltipLength ? text[..MaxTooltipLength] : text;
        }

        private void BtnDragOverlay_Click(object sender, RoutedEventArgs e)
        {
            _overlay.BeginDrag();
            MessageBox.Show(this, Loc.T("DragMessage"), Loc.T("DragTitle"),
                MessageBoxButton.OK, MessageBoxImage.Information);

            if (_overlay.EndDrag(_settings.OverlayAnchor) is var (x, y))
            {
                _vm.PositionX = x;
                _vm.PositionY = y;
            }

            // Ha a pozíció nem változott, nem jön SettingsChanged - a láthatóságot így is újra kell értékelni
            _overlay.UpdateState(_monitor.Current, popup: true);
        }

        private void BtnExit_Click(object sender, RoutedEventArgs e) => ExitApplication();

        private void ExitApplication()
        {
            Cleanup();
            Application.Current.Shutdown();
        }

        private void TrayIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e) => ShowSettingsWindow();

        private void Window_SourceInitialized(object? sender, EventArgs e) => ThemeManager.ApplyTitleBar(this);

        private void Window_StateChanged(object? sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                Hide();
                ShowInTaskbar = false;
            }
        }

        private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_exiting) return;

            // Bezárás helyett a tálcára kerül
            e.Cancel = true;
            _vm.FlushSave();
            Hide();
            ShowInTaskbar = false;
        }
    }
}
