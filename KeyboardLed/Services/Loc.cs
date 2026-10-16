using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;
using KeyboardLed.Models;

namespace KeyboardLed.Services
{
    /// <summary>
    /// Futás közben váltható lokalizáció (magyar / angol).
    /// XAML-ben: {l:T &lt;kulcs&gt;}, kódban: Loc.T("&lt;kulcs&gt;")
    /// </summary>
    public sealed class Loc : INotifyPropertyChanged
    {
        public static Loc Instance { get; } = new();

        private IReadOnlyDictionary<string, string> _current = Strings.English;

        public bool IsHungarian { get; private set; }

        public string this[string key] =>
            _current.TryGetValue(key, out var value) ? value
            : Strings.English.TryGetValue(key, out var fallback) ? fallback
            : key;

        public event PropertyChangedEventHandler? PropertyChanged;

        public event Action? LanguageChanged;

        public static string T(string key) => Instance[key];

        public static string Format(string key, params object[] args) =>
            string.Format(CultureInfo.CurrentCulture, Instance[key], args);

        public void SetLanguage(AppLanguage language)
        {
            bool hungarian = language == AppLanguage.Hungarian ||
                (language == AppLanguage.Auto && CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "hu");

            var strings = hungarian ? Strings.Hungarian : Strings.English;
            if (ReferenceEquals(strings, _current)) return;

            _current = strings;
            IsHungarian = hungarian;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
            LanguageChanged?.Invoke();
        }
    }

    /// <summary>
    /// Markup extension: {l:T &lt;kulcs&gt;} - nyelvváltáskor automatikusan frissül
    /// </summary>
    [MarkupExtensionReturnType(typeof(object))]
    public sealed class TExtension : MarkupExtension
    {
        public TExtension(string key)
        {
            Key = key;
        }

        [ConstructorArgument("key")]
        public string Key { get; set; }

        public override object ProvideValue(IServiceProvider serviceProvider) =>
            new Binding($"[{Key}]") { Source = Loc.Instance, Mode = BindingMode.OneWay }.ProvideValue(serviceProvider);
    }
}
