using Microsoft.Win32;
using System;

namespace KeyboardLed.Services
{
    public static class AutoStartManager
    {
        private const string AppName = "KeyboardLed";
        private const string RegistryPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

        private static string Command
        {
            get
            {
                var exePath = Environment.ProcessPath ?? AppContext.BaseDirectory + "KeyboardLed.exe";
                return $"\"{exePath}\" --minimized";
            }
        }

        public static bool IsAutoStartEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, false);
                return key?.GetValue(AppName) != null;
            }
            catch (Exception ex)
            {
                Log.Warn("Could not read autostart registry value", ex);
                return false;
            }
        }

        public static void SetAutoStart(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, true);
                if (key == null) return;

                if (enable)
                {
                    key.SetValue(AppName, Command);
                }
                else
                {
                    key.DeleteValue(AppName, false);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Could not change autostart registry value");
            }
        }

        /// <summary>
        /// Ha az exe-t áthelyezték, az automatikus indítás a régi útvonalra mutatna - frissítjük
        /// </summary>
        public static void RefreshPathIfEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, true);
                if (key?.GetValue(AppName) is string current &&
                    !string.Equals(current, Command, StringComparison.OrdinalIgnoreCase))
                {
                    key.SetValue(AppName, Command);
                    Log.Info($"Autostart path updated: {Command}");
                }
            }
            catch (Exception ex)
            {
                Log.Warn("Could not refresh autostart path", ex);
            }
        }
    }
}
