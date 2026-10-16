using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using KeyboardLed.Models;
using KeyboardLed.Services;

namespace KeyboardLed.ViewModels
{
    /// <summary>
    /// A beállítások ablak view modelje. Minden változás azonnal érvénybe lép,
    /// a mentés pedig késleltetve (debounce) történik, hogy gépelés közben ne írjon folyamatosan.
    /// </summary>
    public sealed class MainViewModel : ObservableObject
    {
        private static readonly TimeSpan SaveDelay = TimeSpan.FromMilliseconds(400);

        private readonly Settings _s;
        private readonly DispatcherTimer _saveTimer;
        private bool _dirty;
        private bool _autoStart;

        /// <summary>
        /// Bármely beállítás változott (a hívó alkalmazza az overlay-re, tálcára, stb.)
        /// </summary>
        public event Action? SettingsChanged;

        public MainViewModel(Settings settings)
        {
            _s = settings;
            Keys = LockKeys.All.Select(k => new KeyViewModel(k, settings.Keys[k], OnChanged)).ToArray();

            _saveTimer = new DispatcherTimer { Interval = SaveDelay };
            _saveTimer.Tick += (_, _) => FlushSave();

            _autoStart = AutoStartManager.IsAutoStartEnabled();

            BodyColors = ColorOptions(_s.OverlayBodyColor,
                ("#32CD32", "ColorLime"), ("#0078D4", "ColorBlue"), ("#FF6B6B", "ColorRed"), ("#FFD700", "ColorYellow"),
                ("#FFA500", "ColorOrange"), ("#9B59B6", "ColorPurple"), ("#1ABC9C", "ColorTeal"));
            OffColors = ColorOptions(_s.OverlayOffColor,
                ("#FF6B6B", "ColorRed"), ("#5A5A5A", "ColorGray"), ("#2D2D2D", "ColorDarkGray"), ("#0078D4", "ColorBlue"),
                ("#FFA500", "ColorOrange"), ("#9B59B6", "ColorPurple"));
            TextColors = ColorOptions(_s.OverlayTextColor,
                ("#FFFFFF", "ColorWhite"), ("#000000", "ColorBlack"));

            Loc.Instance.LanguageChanged += () =>
            {
                OnPropertyChanged(nameof(PositionLabel));
                OnPropertyChanged(nameof(VersionText));
            };
            UpdateKeyTracking();
        }

        public IReadOnlyList<KeyViewModel> Keys { get; }

        public string VersionText => Loc.Format("Version", AppInfo.Version);

        public static IReadOnlyList<Option<AppLanguage>> LanguageOptions { get; } = new[]
        {
            new Option<AppLanguage>(AppLanguage.Auto, "LangAuto"),
            new Option<AppLanguage>(AppLanguage.Hungarian, "Magyar", literal: true),
            new Option<AppLanguage>(AppLanguage.English, "English", literal: true)
        };

        public static IReadOnlyList<Option<AppTheme>> ThemeOptions { get; } = new[]
        {
            new Option<AppTheme>(AppTheme.System, "ThemeSystem"),
            new Option<AppTheme>(AppTheme.Light, "ThemeLight"),
            new Option<AppTheme>(AppTheme.Dark, "ThemeDark")
        };

        public static IReadOnlyList<Option<OverlaySize>> SizeOptions { get; } = new[]
        {
            new Option<OverlaySize>(OverlaySize.Small, "SizeSmall"),
            new Option<OverlaySize>(OverlaySize.Normal, "SizeNormal"),
            new Option<OverlaySize>(OverlaySize.Large, "SizeLarge")
        };

        public static IReadOnlyList<Option<OverlayAnchor>> AnchorOptions { get; } = new[]
        {
            new Option<OverlayAnchor>(OverlayAnchor.TopLeft, "AnchorTopLeft"),
            new Option<OverlayAnchor>(OverlayAnchor.TopCenter, "AnchorTopCenter"),
            new Option<OverlayAnchor>(OverlayAnchor.TopRight, "AnchorTopRight"),
            new Option<OverlayAnchor>(OverlayAnchor.BottomLeft, "AnchorBottomLeft"),
            new Option<OverlayAnchor>(OverlayAnchor.BottomCenter, "AnchorBottomCenter"),
            new Option<OverlayAnchor>(OverlayAnchor.BottomRight, "AnchorBottomRight"),
            new Option<OverlayAnchor>(OverlayAnchor.Custom, "AnchorCustom")
        };

        public static IReadOnlyList<Option<OverlayMonitor>> MonitorOptions { get; } = new[]
        {
            new Option<OverlayMonitor>(OverlayMonitor.Primary, "MonitorPrimary"),
            new Option<OverlayMonitor>(OverlayMonitor.MouseCursor, "MonitorMouse"),
            new Option<OverlayMonitor>(OverlayMonitor.ActiveWindow, "MonitorActive")
        };

        public IReadOnlyList<Option<string>> BodyColors { get; }
        public IReadOnlyList<Option<string>> OffColors { get; }
        public IReadOnlyList<Option<string>> TextColors { get; }

        // --- General ---

        public bool AutoStart
        {
            get => _autoStart;
            set
            {
                AutoStartManager.SetAutoStart(value);
                SetField(ref _autoStart, AutoStartManager.IsAutoStartEnabled());
            }
        }

        public AppLanguage Language
        {
            get => _s.Language;
            set => Update(_s.Language, value, v => _s.Language = v);
        }

        public AppTheme Theme
        {
            get => _s.Theme;
            set => Update(_s.Theme, value, v => _s.Theme = v);
        }

        public bool ShowTrayIcon
        {
            get => _s.ShowTrayIcon;
            set => Update(_s.ShowTrayIcon, value, v => _s.ShowTrayIcon = v);
        }

        public bool TrackInsert
        {
            get => _s.TrackInsert;
            set
            {
                if (Update(_s.TrackInsert, value, v => _s.TrackInsert = v))
                {
                    UpdateKeyTracking();
                }
            }
        }

        // --- Overlay ---

        public bool ShowOverlay
        {
            get => _s.ShowOverlay;
            set => Update(_s.ShowOverlay, value, v => _s.ShowOverlay = v);
        }

        public bool HideWhenAllOff
        {
            get => _s.HideWhenAllOff;
            set => Update(_s.HideWhenAllOff, value, v => _s.HideWhenAllOff = v);
        }

        public bool HideAfterSeconds
        {
            get => _s.HideAfterSeconds;
            set => Update(_s.HideAfterSeconds, value, v => _s.HideAfterSeconds = v);
        }

        public int HideAfterSecondsValue
        {
            get => _s.HideAfterSecondsValue;
            set => Update(_s.HideAfterSecondsValue,
                Math.Clamp(value, Settings.MinHideSeconds, Settings.MaxHideSeconds),
                v => _s.HideAfterSecondsValue = v);
        }

        public bool FadeAnimation
        {
            get => _s.FadeAnimation;
            set => Update(_s.FadeAnimation, value, v => _s.FadeAnimation = v);
        }

        public OverlaySize OverlaySize
        {
            get => _s.OverlaySize;
            set => Update(_s.OverlaySize, value, v => _s.OverlaySize = v);
        }

        public int OverlayOpacity
        {
            get => _s.OverlayOpacity;
            set
            {
                if (Update(_s.OverlayOpacity, Math.Clamp(value, Settings.MinOpacity, Settings.MaxOpacity), v => _s.OverlayOpacity = v))
                {
                    OnPropertyChanged(nameof(OpacityText));
                }
            }
        }

        public string OpacityText => $"{OverlayOpacity}%";

        public string BodyColor
        {
            get => _s.OverlayBodyColor;
            set => UpdateColor(_s.OverlayBodyColor, value, v => _s.OverlayBodyColor = v);
        }

        public string OffColor
        {
            get => _s.OverlayOffColor;
            set => UpdateColor(_s.OverlayOffColor, value, v => _s.OverlayOffColor = v);
        }

        public string TextColor
        {
            get => _s.OverlayTextColor;
            set => UpdateColor(_s.OverlayTextColor, value, v => _s.OverlayTextColor = v);
        }

        // --- Position ---

        public OverlayAnchor Anchor
        {
            get => _s.OverlayAnchor;
            set
            {
                if (Update(_s.OverlayAnchor, value, v => _s.OverlayAnchor = v))
                {
                    OnPropertyChanged(nameof(IsCustomAnchor));
                    OnPropertyChanged(nameof(IsMonitorSelectable));
                    OnPropertyChanged(nameof(PositionLabel));
                    OnPropertyChanged(nameof(PositionX));
                    OnPropertyChanged(nameof(PositionY));
                }
            }
        }

        public OverlayMonitor Monitor
        {
            get => _s.OverlayMonitor;
            set => Update(_s.OverlayMonitor, value, v => _s.OverlayMonitor = v);
        }

        public bool IsCustomAnchor => Anchor == OverlayAnchor.Custom;

        public bool IsMonitorSelectable => !IsCustomAnchor;

        public string PositionLabel => Loc.T(IsCustomAnchor ? "PositionLabel" : "OffsetLabel");

        /// <summary>
        /// Egyéni módban abszolút X, egyébként a saroktól mért eltolás
        /// </summary>
        public int PositionX
        {
            get => IsCustomAnchor ? _s.OverlayCustomX : _s.OverlayOffsetX;
            set
            {
                if (IsCustomAnchor)
                    Update(_s.OverlayCustomX, Math.Clamp(value, -Settings.MaxCoordinate, Settings.MaxCoordinate), v => _s.OverlayCustomX = v);
                else
                    Update(_s.OverlayOffsetX, Math.Clamp(value, -Settings.MaxOffset, Settings.MaxOffset), v => _s.OverlayOffsetX = v);
            }
        }

        public int PositionY
        {
            get => IsCustomAnchor ? _s.OverlayCustomY : _s.OverlayOffsetY;
            set
            {
                if (IsCustomAnchor)
                    Update(_s.OverlayCustomY, Math.Clamp(value, -Settings.MaxCoordinate, Settings.MaxCoordinate), v => _s.OverlayCustomY = v);
                else
                    Update(_s.OverlayOffsetY, Math.Clamp(value, -Settings.MaxOffset, Settings.MaxOffset), v => _s.OverlayOffsetY = v);
            }
        }

        public void UpdateState(KeyboardState state)
        {
            foreach (var key in Keys)
            {
                key.IsOn = state[key.Key];
            }
        }

        public void FlushSave()
        {
            _saveTimer.Stop();
            if (_dirty)
            {
                _dirty = false;
                _s.Save();
            }
        }

        private void UpdateKeyTracking()
        {
            foreach (var key in Keys)
            {
                key.IsTracked = key.Key != LockKey.Insert || _s.TrackInsert;
            }
        }

        private void OnChanged()
        {
            _dirty = true;
            _saveTimer.Stop();
            _saveTimer.Start();
            SettingsChanged?.Invoke();
        }

        private bool Update<T>(T current, T value, Action<T> apply, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(current, value)) return false;
            apply(value);
            OnPropertyChanged(name);
            OnChanged();
            return true;
        }

        private bool UpdateColor(string current, string? value, Action<string> apply, [CallerMemberName] string? name = null) =>
            Settings.IsValidColor(value) && Update(current, value!, apply, name);

        /// <summary>
        /// Ha a beállításban kézzel megadott szín van, az is választható marad a listában
        /// </summary>
        private static IReadOnlyList<Option<string>> ColorOptions(string current, params (string Hex, string LabelKey)[] colors)
        {
            var options = colors.Select(c => new Option<string>(c.Hex, c.LabelKey)).ToList();
            if (!colors.Any(c => string.Equals(c.Hex, current, StringComparison.OrdinalIgnoreCase)))
            {
                options.Add(new Option<string>(current, current, literal: true));
            }
            return options;
        }
    }
}
