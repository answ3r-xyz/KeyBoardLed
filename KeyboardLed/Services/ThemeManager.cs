using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using KeyboardLed.Models;
using Microsoft.Win32;

namespace KeyboardLed.Services
{
    /// <summary>
    /// Világos/sötét téma: a rendszer beállítás követése és a színpaletta cseréje
    /// </summary>
    public static class ThemeManager
    {
        private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19; // Windows 10 1809-1909

        private static readonly Uri LightUri = new("pack://application:,,,/KeyboardLed;component/Themes/Light.xaml");
        private static readonly Uri DarkUri = new("pack://application:,,,/KeyboardLed;component/Themes/Dark.xaml");

        private static bool? _appliedDark;

        public static bool IsDark => _appliedDark ?? false;

        public static event Action? ThemeChanged;

        /// <summary>
        /// A tálca világos témájú-e (a tálcaikon mindig a tálcához igazodik)
        /// </summary>
        public static bool IsTaskbarLight() => ReadPersonalizeFlag("SystemUsesLightTheme", defaultValue: false);

        public static bool ResolveDark(AppTheme theme) => theme switch
        {
            AppTheme.Light => false,
            AppTheme.Dark => true,
            _ => !ReadPersonalizeFlag("AppsUseLightTheme", defaultValue: true)
        };

        public static void Apply(AppTheme theme)
        {
            bool dark = ResolveDark(theme);
            if (_appliedDark == dark) return;
            _appliedDark = dark;

            var resources = Application.Current.Resources.MergedDictionaries;
            var palette = new ResourceDictionary { Source = dark ? DarkUri : LightUri };
            // A paletta mindig az első merged dictionary (App.xaml)
            if (resources.Count > 0)
            {
                resources[0] = palette;
            }
            else
            {
                resources.Add(palette);
            }

            ThemeChanged?.Invoke();
        }

        /// <summary>
        /// Sötét címsor (Windows 10 1809+ / Windows 11)
        /// </summary>
        public static void ApplyTitleBar(Window window)
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;

            int value = IsDark ? 1 : 0;
            try
            {
                if (DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int)) != 0)
                {
                    DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, ref value, sizeof(int));
                }
            }
            catch (DllNotFoundException)
            {
                // DWM nem elérhető
            }
        }

        private static bool ReadPersonalizeFlag(string name, bool defaultValue)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey, false);
                if (key?.GetValue(name) is int value)
                {
                    return value != 0;
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"Could not read theme setting {name}", ex);
            }
            return defaultValue;
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    }
}
