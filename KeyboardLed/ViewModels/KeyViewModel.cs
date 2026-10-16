using System;
using System.Collections.Generic;
using System.Windows.Input;
using KeyboardLed.Models;
using KeyboardLed.Services;
using Microsoft.Win32;

namespace KeyboardLed.ViewModels
{
    /// <summary>
    /// Egy billentyű állapota és beállításai a felületen
    /// </summary>
    public sealed class KeyViewModel : ObservableObject
    {
        private readonly KeySettings _s;
        private readonly Action _changed;
        private bool _isOn;
        private bool _isTracked = true;

        public static IReadOnlyList<Option<KeySound>> SoundOptions { get; } = new[]
        {
            new Option<KeySound>(KeySound.None, "SoundNone"),
            new Option<KeySound>(KeySound.Beep, "SoundBeep"),
            new Option<KeySound>(KeySound.Asterisk, "SoundAsterisk"),
            new Option<KeySound>(KeySound.Exclamation, "SoundExclamation"),
            new Option<KeySound>(KeySound.Hand, "SoundHand"),
            new Option<KeySound>(KeySound.Question, "SoundQuestion"),
            new Option<KeySound>(KeySound.Custom, "SoundCustom")
        };

        public static IReadOnlyList<Option<ForcedState>> ForcedOptions { get; } = new[]
        {
            new Option<ForcedState>(ForcedState.None, "ForceNone"),
            new Option<ForcedState>(ForcedState.AlwaysOn, "ForceOn"),
            new Option<ForcedState>(ForcedState.AlwaysOff, "ForceOff")
        };

        public KeyViewModel(LockKey key, KeySettings settings, Action changed)
        {
            Key = key;
            _s = settings;
            _changed = changed;
            ToggleCommand = new RelayCommand(() => KeyboardInput.Toggle(Key));
            TestSoundCommand = new RelayCommand(() => SoundService.Play(_s));
            BrowseSoundCommand = new RelayCommand(BrowseSound);
        }

        public LockKey Key { get; }

        public string KeyLabel => LockKeys.DefaultName(Key);

        public bool CanForce => LockKeys.CanForce(Key);

        public ICommand ToggleCommand { get; }
        public ICommand TestSoundCommand { get; }
        public ICommand BrowseSoundCommand { get; }

        public bool IsOn
        {
            get => _isOn;
            set => SetField(ref _isOn, value);
        }

        /// <summary>
        /// Insert csak akkor látszik, ha a figyelése be van kapcsolva
        /// </summary>
        public bool IsTracked
        {
            get => _isTracked;
            set => SetField(ref _isTracked, value);
        }

        public string DisplayName => Settings.NormalizeName(_s.Name, Key);

        public string Name
        {
            get => _s.Name;
            set
            {
                if (_s.Name == value) return;
                _s.Name = value ?? "";
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
                _changed();
            }
        }

        public bool ShowInOverlay
        {
            get => _s.ShowInOverlay;
            set => Update(_s.ShowInOverlay, value, v => _s.ShowInOverlay = v);
        }

        public bool PopupOnChange
        {
            get => _s.PopupOnChange;
            set => Update(_s.PopupOnChange, value, v => _s.PopupOnChange = v);
        }

        public KeySound Sound
        {
            get => _s.Sound;
            set
            {
                if (Update(_s.Sound, value, v => _s.Sound = v))
                {
                    OnPropertyChanged(nameof(HasSound));
                }
            }
        }

        public bool HasSound => Sound != KeySound.None;

        public ForcedState Forced
        {
            get => _s.Forced;
            set => Update(_s.Forced, CanForce ? value : ForcedState.None, v => _s.Forced = v);
        }

        private bool Update<T>(T current, T value, Action<T> apply, [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(current, value)) return false;
            apply(value);
            OnPropertyChanged(name);
            _changed();
            return true;
        }

        private void BrowseSound()
        {
            var dialog = new OpenFileDialog
            {
                Filter = Loc.T("SoundFileFilter"),
                CheckFileExists = true
            };
            if (!string.IsNullOrEmpty(_s.CustomSoundPath))
            {
                dialog.FileName = _s.CustomSoundPath;
            }

            if (dialog.ShowDialog() == true)
            {
                _s.CustomSoundPath = dialog.FileName;
                Sound = KeySound.Custom;
                _changed();
                SoundService.Play(_s);
            }
        }
    }
}
