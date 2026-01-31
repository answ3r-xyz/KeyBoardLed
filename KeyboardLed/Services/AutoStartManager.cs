using Microsoft.Win32;
using System;

namespace KeyboardLed.Services
{
    public static class AutoStartManager
    {
        private const string AppName = "KeyboardLed";
        private const string RegistryPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

        public static bool IsAutoStartEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, false);
                return key?.GetValue(AppName) != null;
            }
            catch
            {
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
                    var exePath = Environment.ProcessPath ?? AppContext.BaseDirectory + "KeyboardLed.exe";
                    key.SetValue(AppName, $"\"{exePath}\" --minimized");
                }
                else
                {
                    key.DeleteValue(AppName, false);
                }
            }
            catch
            {
                // Ignore registry errors
            }
        }
    }
}
