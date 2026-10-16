using System;
using System.IO;
using System.Reflection;

namespace KeyboardLed.Services
{
    public static class AppInfo
    {
        public static string DataDirectory { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "KeyboardLed");

        public static string Version { get; } =
            Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
    }
}
