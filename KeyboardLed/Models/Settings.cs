using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using KeyboardLed.Services;

namespace KeyboardLed.Models
{
    /// <summary>
    /// Billentyűnkénti beállítások
    /// </summary>
    public class KeySettings
    {
        public string Name { get; set; } = "";
        public bool ShowInOverlay { get; set; } = true;
        public bool PopupOnChange { get; set; } = true;
        public KeySound Sound { get; set; } = KeySound.None;
        public string? CustomSoundPath { get; set; }
        public ForcedState Forced { get; set; } = ForcedState.None;
    }

    public class KeySettingsSet
    {
        public KeySettings NumLock { get; set; } = new();
        public KeySettings CapsLock { get; set; } = new();
        public KeySettings ScrollLock { get; set; } = new();
        public KeySettings Insert { get; set; } = new() { ShowInOverlay = true, PopupOnChange = true };

        public KeySettings this[LockKey key] => key switch
        {
            LockKey.NumLock => NumLock,
            LockKey.CapsLock => CapsLock,
            LockKey.ScrollLock => ScrollLock,
            LockKey.Insert => Insert,
            _ => throw new ArgumentOutOfRangeException(nameof(key))
        };
    }

    public class Settings
    {
        public const int CurrentVersion = 2;
        public const int MinHideSeconds = 1;
        public const int MaxHideSeconds = 60;
        public const int MinOpacity = 10;
        public const int MaxOpacity = 100;
        public const int MaxOffset = 5000;
        public const int MaxCoordinate = 32000;
        public const int MaxNameLength = 32;

        public const string DefaultBodyColor = "#32CD32"; // Lime
        public const string DefaultTextColor = "#FFFFFF"; // White
        public const string DefaultOffColor = "#FF6B6B";  // Red for OFF state

        private static readonly Regex HexColor = new("^#([0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$", RegexOptions.Compiled);

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public int Version { get; set; } = CurrentVersion;

        // General
        public AppLanguage Language { get; set; } = AppLanguage.Auto;
        public AppTheme Theme { get; set; } = AppTheme.System;
        public bool ShowTrayIcon { get; set; } = true;
        public bool TrackInsert { get; set; } = false;

        // Per-key settings
        public KeySettingsSet Keys { get; set; } = new();

        // Overlay settings
        public bool ShowOverlay { get; set; } = true;
        public bool HideWhenAllOff { get; set; } = true;
        public bool HideAfterSeconds { get; set; } = false;
        public int HideAfterSecondsValue { get; set; } = 2;
        public int OverlayOpacity { get; set; } = 80;
        public OverlaySize OverlaySize { get; set; } = OverlaySize.Normal;
        public bool FadeAnimation { get; set; } = true;

        // Overlay colors
        public string OverlayBodyColor { get; set; } = DefaultBodyColor;
        public string OverlayTextColor { get; set; } = DefaultTextColor;
        public string OverlayOffColor { get; set; } = DefaultOffColor;

        // Overlay position (fizikai pixel)
        public OverlayAnchor OverlayAnchor { get; set; } = OverlayAnchor.BottomRight;
        public OverlayMonitor OverlayMonitor { get; set; } = OverlayMonitor.Primary;
        public int OverlayOffsetX { get; set; } = 24;
        public int OverlayOffsetY { get; set; } = 24;
        public int OverlayCustomX { get; set; } = 100;
        public int OverlayCustomY { get; set; } = 100;

        /// <summary>
        /// Régi (v1) beállítások, amiket a migráció alakít át
        /// </summary>
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Legacy { get; set; }

        private static readonly string SettingsPath = Path.Combine(AppInfo.DataDirectory, "settings.json");

        public string GetDisplayName(LockKey key)
        {
            var name = Keys[key].Name?.Trim();
            return string.IsNullOrEmpty(name) ? LockKeys.DefaultName(key) : name;
        }

        /// <param name="legacyDipScale">A v1 pozíció DIP-ben volt; ezzel a szorzóval lesz fizikai pixel.</param>
        public static Settings Load(double legacyDipScale)
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    return FromJson(File.ReadAllText(SettingsPath), legacyDipScale);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Settings file could not be read, using defaults");
                BackupCorruptFile();
            }
            return new Settings().Normalize();
        }

        public static Settings FromJson(string json, double legacyDipScale)
        {
            var settings = JsonSerializer.Deserialize<Settings>(json, JsonOptions) ?? new Settings();
            settings.MigrateLegacy(legacyDipScale);
            return settings.Normalize();
        }

        public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(AppInfo.DataDirectory);

                // Atomikus mentés: félbeszakadt írás nem rontja el a meglévő fájlt
                var tempPath = SettingsPath + ".tmp";
                File.WriteAllText(tempPath, ToJson());
                File.Move(tempPath, SettingsPath, overwrite: true);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Settings could not be saved");
            }
        }

        /// <summary>
        /// Érvénytelen / kézzel elrontott értékek javítása, hogy ne okozzanak összeomlást.
        /// </summary>
        public Settings Normalize()
        {
            Version = CurrentVersion;

            Language = Defined(Language, AppLanguage.Auto);
            Theme = Defined(Theme, AppTheme.System);
            OverlaySize = Defined(OverlaySize, OverlaySize.Normal);
            OverlayAnchor = Defined(OverlayAnchor, OverlayAnchor.BottomRight);
            OverlayMonitor = Defined(OverlayMonitor, OverlayMonitor.Primary);

            HideAfterSecondsValue = Math.Clamp(HideAfterSecondsValue, MinHideSeconds, MaxHideSeconds);
            OverlayOpacity = Math.Clamp(OverlayOpacity, MinOpacity, MaxOpacity);
            OverlayOffsetX = Math.Clamp(OverlayOffsetX, -MaxOffset, MaxOffset);
            OverlayOffsetY = Math.Clamp(OverlayOffsetY, -MaxOffset, MaxOffset);
            OverlayCustomX = Math.Clamp(OverlayCustomX, -MaxCoordinate, MaxCoordinate);
            OverlayCustomY = Math.Clamp(OverlayCustomY, -MaxCoordinate, MaxCoordinate);

            OverlayBodyColor = ValidColorOr(OverlayBodyColor, DefaultBodyColor);
            OverlayTextColor = ValidColorOr(OverlayTextColor, DefaultTextColor);
            OverlayOffColor = ValidColorOr(OverlayOffColor, DefaultOffColor);

            Keys ??= new KeySettingsSet();
            Keys.NumLock ??= new KeySettings();
            Keys.CapsLock ??= new KeySettings();
            Keys.ScrollLock ??= new KeySettings();
            Keys.Insert ??= new KeySettings();

            foreach (var key in LockKeys.All)
            {
                var k = Keys[key];
                k.Name = NormalizeName(k.Name, key);
                k.Sound = Defined(k.Sound, KeySound.None);
                k.Forced = LockKeys.CanForce(key) ? Defined(k.Forced, ForcedState.None) : ForcedState.None;
                if (string.IsNullOrWhiteSpace(k.CustomSoundPath))
                {
                    k.CustomSoundPath = null;
                }
            }

            return this;
        }

        public static bool IsValidColor(string? value) => value != null && HexColor.IsMatch(value);

        public static string NormalizeName(string? name, LockKey key)
        {
            var trimmed = name?.Trim() ?? "";
            if (trimmed.Length == 0)
            {
                return LockKeys.DefaultName(key);
            }
            return trimmed.Length > MaxNameLength ? trimmed[..MaxNameLength] : trimmed;
        }

        private static string ValidColorOr(string? value, string fallback) =>
            IsValidColor(value) ? value!.ToUpperInvariant() : fallback;

        private static T Defined<T>(T value, T fallback) where T : struct, Enum =>
            Enum.IsDefined(value) ? value : fallback;

        /// <summary>
        /// v1 (lapos) beállítások átalakítása az új szerkezetre
        /// </summary>
        private void MigrateLegacy(double legacyDipScale)
        {
            if (Legacy == null || Legacy.Count == 0)
            {
                Legacy = null;
                return;
            }

            MigrateKey("NumLock", Keys.NumLock);
            MigrateKey("CapsLock", Keys.CapsLock);
            MigrateKey("ScrollLock", Keys.ScrollLock);

            if (TryGetBool("BeepOnChange", out bool beep) && beep)
            {
                Keys.NumLock.Sound = KeySound.Beep;
                Keys.CapsLock.Sound = KeySound.Beep;
                Keys.ScrollLock.Sound = KeySound.Beep;
            }

            if (TryGetInt("OverlayX", out int x) && TryGetInt("OverlayY", out int y))
            {
                OverlayAnchor = OverlayAnchor.Custom;
                OverlayCustomX = (int)Math.Round(x * legacyDipScale);
                OverlayCustomY = (int)Math.Round(y * legacyDipScale);
            }

            Legacy = null;
        }

        private void MigrateKey(string prefix, KeySettings target)
        {
            if (TryGetString(prefix + "Name", out var name))
            {
                target.Name = name;
            }
            if (TryGetBool("OverlayShow" + prefix, out bool show))
            {
                target.ShowInOverlay = show;
            }
        }

        private bool TryGetBool(string name, out bool value)
        {
            value = false;
            if (Legacy!.TryGetValue(name, out var e) && (e.ValueKind == JsonValueKind.True || e.ValueKind == JsonValueKind.False))
            {
                value = e.GetBoolean();
                return true;
            }
            return false;
        }

        private bool TryGetInt(string name, out int value)
        {
            value = 0;
            return Legacy!.TryGetValue(name, out var e) && e.ValueKind == JsonValueKind.Number && e.TryGetInt32(out value);
        }

        private bool TryGetString(string name, out string value)
        {
            value = "";
            if (Legacy!.TryGetValue(name, out var e) && e.ValueKind == JsonValueKind.String)
            {
                value = e.GetString() ?? "";
                return true;
            }
            return false;
        }

        private static void BackupCorruptFile()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var backup = SettingsPath + $".corrupt-{DateTime.Now:yyyyMMdd-HHmmss}";
                    File.Move(SettingsPath, backup);
                    Log.Warn($"Corrupt settings file moved to {backup}");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Corrupt settings file could not be backed up");
            }
        }
    }
}
