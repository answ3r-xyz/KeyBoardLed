using KeyboardLed.Models;
using Xunit;

namespace KeyboardLed.Tests
{
    public class SettingsTests
    {
        [Theory]
        [InlineData(-1, Settings.MinHideSeconds)]
        [InlineData(0, Settings.MinHideSeconds)]
        [InlineData(5, 5)]
        [InlineData(9999999, Settings.MaxHideSeconds)]
        public void HideAfterSeconds_is_clamped(int value, int expected)
        {
            // A régi verzió negatív / túl nagy értéknél összeomlott (DispatcherTimer.Interval)
            var settings = Settings.FromJson($"{{\"HideAfterSecondsValue\": {value}}}", 1.0);

            Assert.Equal(expected, settings.HideAfterSecondsValue);
        }

        [Fact]
        public void Invalid_colors_fall_back_to_defaults()
        {
            var settings = Settings.FromJson(
                "{\"OverlayBodyColor\": \"not-a-color\", \"OverlayTextColor\": \"#12\", \"OverlayOffColor\": \"#abcdef\"}", 1.0);

            Assert.Equal(Settings.DefaultBodyColor, settings.OverlayBodyColor);
            Assert.Equal(Settings.DefaultTextColor, settings.OverlayTextColor);
            Assert.Equal("#ABCDEF", settings.OverlayOffColor);
        }

        [Fact]
        public void Undefined_enum_values_fall_back_to_defaults()
        {
            var settings = Settings.FromJson("{\"OverlayAnchor\": 42, \"Theme\": 7}", 1.0);

            Assert.Equal(OverlayAnchor.BottomRight, settings.OverlayAnchor);
            Assert.Equal(AppTheme.System, settings.Theme);
        }

        [Fact]
        public void Opacity_is_clamped()
        {
            Assert.Equal(Settings.MinOpacity, Settings.FromJson("{\"OverlayOpacity\": 0}", 1.0).OverlayOpacity);
            Assert.Equal(Settings.MaxOpacity, Settings.FromJson("{\"OverlayOpacity\": 500}", 1.0).OverlayOpacity);
        }

        [Fact]
        public void Empty_and_long_names_are_normalized()
        {
            var longName = new string('x', 100);
            var settings = Settings.FromJson(
                "{\"Keys\": {\"NumLock\": {\"Name\": \"   \"}, \"CapsLock\": {\"Name\": \"" + longName + "\"}}}", 1.0);

            Assert.Equal("Num Lock", settings.Keys.NumLock.Name);
            Assert.Equal(Settings.MaxNameLength, settings.Keys.CapsLock.Name.Length);
        }

        [Fact]
        public void Insert_forced_state_is_removed()
        {
            var settings = Settings.FromJson("{\"Keys\": {\"Insert\": {\"Forced\": \"AlwaysOff\"}}}", 1.0);

            Assert.Equal(ForcedState.None, settings.Keys.Insert.Forced);
        }

        [Fact]
        public void Null_key_settings_are_recreated()
        {
            var settings = Settings.FromJson("{\"Keys\": {\"NumLock\": null}}", 1.0);

            Assert.NotNull(settings.Keys.NumLock);
            Assert.Equal("Num Lock", settings.Keys.NumLock.Name);
        }

        [Fact]
        public void Legacy_v1_settings_are_migrated()
        {
            const string legacy = @"{
                ""AutoStart"": true,
                ""BeepOnChange"": true,
                ""ShowOverlay"": true,
                ""OverlayShowNumLock"": true,
                ""OverlayShowCapsLock"": false,
                ""OverlayShowScrollLock"": true,
                ""HideWhenAllOff"": false,
                ""HideAfterSeconds"": true,
                ""HideAfterSecondsValue"": 3,
                ""OverlayOpacity"": 70,
                ""OverlayX"": 200,
                ""OverlayY"": 100,
                ""OverlayBodyColor"": ""#0078D4"",
                ""OverlayTextColor"": ""#000000"",
                ""OverlayOffColor"": ""#FF6B6B"",
                ""OverlaySize"": ""Large"",
                ""NumLockName"": ""NUM"",
                ""CapsLockName"": ""CAPS"",
                ""ScrollLockName"": ""SCROLL"",
                ""ShowTrayIcon"": true
            }";

            var settings = Settings.FromJson(legacy, 1.5);

            Assert.Equal("NUM", settings.Keys.NumLock.Name);
            Assert.Equal("CAPS", settings.Keys.CapsLock.Name);
            Assert.Equal("SCROLL", settings.Keys.ScrollLock.Name);
            Assert.True(settings.Keys.NumLock.ShowInOverlay);
            Assert.False(settings.Keys.CapsLock.ShowInOverlay);
            Assert.Equal(KeySound.Beep, settings.Keys.CapsLock.Sound);
            Assert.Equal(KeySound.None, settings.Keys.Insert.Sound);

            // A régi DIP pozíció fizikai pixelre váltva
            Assert.Equal(OverlayAnchor.Custom, settings.OverlayAnchor);
            Assert.Equal(300, settings.OverlayCustomX);
            Assert.Equal(150, settings.OverlayCustomY);

            Assert.Equal(OverlaySize.Large, settings.OverlaySize);
            Assert.Equal(3, settings.HideAfterSecondsValue);
            Assert.False(settings.HideWhenAllOff);
            Assert.Null(settings.Legacy);
        }

        [Fact]
        public void Json_round_trip_keeps_values()
        {
            var original = new Settings
            {
                Language = AppLanguage.Hungarian,
                Theme = AppTheme.Dark,
                TrackInsert = true,
                OverlayAnchor = OverlayAnchor.TopCenter,
                OverlayMonitor = OverlayMonitor.MouseCursor,
                OverlayOffsetX = -15,
                FadeAnimation = false
            }.Normalize();
            original.Keys.CapsLock.Forced = ForcedState.AlwaysOff;
            original.Keys.ScrollLock.Sound = KeySound.Custom;
            original.Keys.ScrollLock.CustomSoundPath = @"C:\sounds\ding.wav";

            var copy = Settings.FromJson(original.ToJson(), 1.0);

            Assert.Equal(AppLanguage.Hungarian, copy.Language);
            Assert.Equal(AppTheme.Dark, copy.Theme);
            Assert.True(copy.TrackInsert);
            Assert.Equal(OverlayAnchor.TopCenter, copy.OverlayAnchor);
            Assert.Equal(OverlayMonitor.MouseCursor, copy.OverlayMonitor);
            Assert.Equal(-15, copy.OverlayOffsetX);
            Assert.False(copy.FadeAnimation);
            Assert.Equal(ForcedState.AlwaysOff, copy.Keys.CapsLock.Forced);
            Assert.Equal(KeySound.Custom, copy.Keys.ScrollLock.Sound);
            Assert.Equal(@"C:\sounds\ding.wav", copy.Keys.ScrollLock.CustomSoundPath);
        }

        [Fact]
        public void Enums_are_saved_as_names()
        {
            var json = new Settings { Theme = AppTheme.Dark }.Normalize().ToJson();

            Assert.Contains("\"Theme\": \"Dark\"", json);
        }

        [Fact]
        public void Display_name_falls_back_to_default()
        {
            var settings = new Settings().Normalize();
            settings.Keys.Insert.Name = "  ";

            Assert.Equal("Insert", settings.GetDisplayName(LockKey.Insert));
        }
    }
}
