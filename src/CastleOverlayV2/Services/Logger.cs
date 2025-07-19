using System;
using System.IO;

namespace CastleOverlayV2.Services
{
    public static class Logger
    {
        private static bool _enabled = false;
        private static string _logPath = "";

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
