using System;
using System.IO;

namespace CastleOverlayV2.Services
{
    public static class Logger
    {
        private static bool _enabled = false;
        private static string _logPath = "";

        // Gate hot-path interpolated calls with `if (Logger.IsEnabled) Logger.Log(...)`
        // so the message string isn't built when logging is disabled.
        public static bool IsEnabled => _enabled;

        public static void Init(bool enableLogging)
        {
            _enabled = enableLogging;

            if (!_enabled) return;

            string appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DragOverlay");

            Directory.CreateDirectory(appDataPath);
            _logPath = Path.Combine(appDataPath, "DragOverlay.log");

            //File.WriteAllText(_logPath, $"=== App started: {DateTime.Now} ===\n");
        }

        public static void Log(string message)
        {
            if (!_enabled) return;

            try
            {
                File.AppendAllText(_logPath, $"{DateTime.Now:HH:mm:ss} - {message}{Environment.NewLine}");

            }
            catch
            {
                // silently fail
            }
        }
    }
}
