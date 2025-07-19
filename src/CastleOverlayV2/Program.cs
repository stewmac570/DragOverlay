using CastleOverlayV2.Services;

namespace CastleOverlayV2
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            // Initialize application settings (DPI, fonts, etc.)
            ApplicationConfiguration.Initialize();

            // Load config
            var configService = new ConfigService();

            // Initialize logger (only logs if enabled in config.json)
            Logger.Init(configService.IsDebugLoggingEnabled());
            Logger.Log($"App started — Build {configService.GetBuildNumber()}");

            // Start the app with config injected into MainForm
            Application.Run(new MainForm(configService));
        }
    }
}
