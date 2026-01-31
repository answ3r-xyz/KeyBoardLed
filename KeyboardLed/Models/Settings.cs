using System;
using System.IO;
using System.Text.Json;

namespace KeyboardLed.Models
{
    public class Settings
    {
        public bool AutoStart { get; set; } = false;
        public bool BeepOnChange { get; set; } = false;
        
        // Overlay settings
        public bool ShowOverlay { get; set; } = true;
        public bool OverlayShowNumLock { get; set; } = true;
        public bool OverlayShowCapsLock { get; set; } = true;
        public bool OverlayShowScrollLock { get; set; } = true;
        public bool HideWhenAllOff { get; set; } = true;
        public bool HideAfterSeconds { get; set; } = false;
        public int HideAfterSecondsValue { get; set; } = 1;
        public int OverlayOpacity { get; set; } = 80;
        
        // Overlay position (pixel-perfect)
        public int OverlayX { get; set; } = 100;
        public int OverlayY { get; set; } = 100;
        
        // Overlay colors
        public string OverlayBodyColor { get; set; } = "#32CD32"; // Lime
        public string OverlayTextColor { get; set; } = "#FFFFFF"; // White
        public string OverlayOffColor { get; set; } = "#FF6B6B"; // Red for OFF state
        
        // Overlay size
        public string OverlaySize { get; set; } = "Normal"; // Small, Normal, Large
        
        // Tray icon settings
        public bool ShowTrayIcon { get; set; } = true;
        
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "KeyboardLed",
            "settings.json");

        public static Settings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    return JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
                }
            }
            catch
            {
                // Return default settings on error
            }
            return new Settings();
        }

        public void Save()
        {
            try
            {
                var directory = Path.GetDirectoryName(SettingsPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(this, options);
                File.WriteAllText(SettingsPath, json);
            }
            catch
            {
                // Ignore save errors
            }
        }
    }
}
