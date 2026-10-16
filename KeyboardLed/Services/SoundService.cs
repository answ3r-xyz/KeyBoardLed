using System;
using System.IO;
using System.Media;
using KeyboardLed.Models;

namespace KeyboardLed.Services
{
    /// <summary>
    /// Billentyűnkénti hangjelzés (rendszerhang vagy egyéni .wav)
    /// </summary>
    public static class SoundService
    {
        private static SoundPlayer? _player;
        private static string? _playerPath;

        public static void Play(KeySettings key)
        {
            try
            {
                switch (key.Sound)
                {
                    case KeySound.Beep: SystemSounds.Beep.Play(); break;
                    case KeySound.Asterisk: SystemSounds.Asterisk.Play(); break;
                    case KeySound.Exclamation: SystemSounds.Exclamation.Play(); break;
                    case KeySound.Hand: SystemSounds.Hand.Play(); break;
                    case KeySound.Question: SystemSounds.Question.Play(); break;
                    case KeySound.Custom: PlayFile(key.CustomSoundPath); break;
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"Sound playback failed ({key.Sound}, {key.CustomSoundPath})", ex);
                SystemSounds.Beep.Play();
            }
        }

        private static void PlayFile(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                SystemSounds.Beep.Play();
                return;
            }

            if (_player == null || !string.Equals(_playerPath, path, StringComparison.OrdinalIgnoreCase))
            {
                _player?.Dispose();
                _player = new SoundPlayer(path);
                _playerPath = path;
            }
            _player.Play();
        }
    }
}
