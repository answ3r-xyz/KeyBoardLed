using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using KeyboardLed.Services;

namespace KeyboardLed.ViewModels
{
    public abstract class ObservableObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(name);
            return true;
        }
    }

    public sealed class RelayCommand : ICommand
    {
        private readonly Action _execute;

        public RelayCommand(Action execute)
        {
            _execute = execute;
        }

        public event EventHandler? CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => _execute();
    }

    /// <summary>
    /// ComboBox elem lokalizált felirattal (nyelvváltáskor frissül)
    /// </summary>
    public sealed class Option<T> : ObservableObject
    {
        private readonly string _labelKey;
        private readonly bool _literal;

        public Option(T value, string labelKey, bool literal = false)
        {
            Value = value;
            _labelKey = labelKey;
            _literal = literal;
            if (!literal)
            {
                Loc.Instance.LanguageChanged += () => OnPropertyChanged(nameof(Label));
            }
        }

        public T Value { get; }

        public string Label => _literal ? _labelKey : Loc.T(_labelKey);
    }
}
