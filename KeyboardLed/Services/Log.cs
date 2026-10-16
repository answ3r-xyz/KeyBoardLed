using System;
using System.IO;

namespace KeyboardLed.Services
{
    /// <summary>
    /// Egyszerű fájl logger (%AppData%\KeyboardLed\keyboardled.log), 1 MB felett forgat.
    /// </summary>
    public static class Log
    {
        private const long MaxBytes = 1_000_000;
        private static readonly object Sync = new();

        public static string LogPath { get; } = Path.Combine(AppInfo.DataDirectory, "keyboardled.log");

        public static void Info(string message) => Write("INFO", message, null);

        public static void Warn(string message, Exception? ex = null) => Write("WARN", message, ex);

        public static void Error(Exception? ex, string message) => Write("ERROR", message, ex);

        private static void Write(string level, string message, Exception? ex)
        {
            try
            {
                lock (Sync)
                {
                    Directory.CreateDirectory(AppInfo.DataDirectory);

                    var info = new FileInfo(LogPath);
                    if (info.Exists && info.Length > MaxBytes)
                    {
                        File.Move(LogPath, LogPath + ".old", overwrite: true);
                    }

                    var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";
                    if (ex != null)
                    {
                        line += Environment.NewLine + ex;
                    }
                    File.AppendAllText(LogPath, line + Environment.NewLine);
                }
            }
            catch
            {
                // A logolás soha nem döntheti el a programot
            }
        }
    }
}
