using CastleOverlayV2.Services;

namespace CastleOverlayV2.Tests
{
    public class CsvLoaderTests
    {
        [Fact]
        public void Load_DisposesDebugLogWhenCsvOpenThrows()
        {
            string logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DragOverlay",
                "debug_log.txt");
            byte[]? originalLog = File.Exists(logPath) ? File.ReadAllBytes(logPath) : null;

            try
            {
                var configService = new ConfigService();
                configService.Config.EnableDebugLogging = true;
                var loader = new CsvLoader(configService);
                string missingCsv = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.csv");

                Assert.Throws<FileNotFoundException>(() => loader.Load(missingCsv));

                using var stream = new FileStream(
                    logPath,
                    FileMode.Open,
                    FileAccess.ReadWrite,
                    FileShare.None);
                Assert.True(stream.CanWrite);
            }
            finally
            {
                if (originalLog is null)
                {
                    File.Delete(logPath);
                }
                else
                {
                    File.WriteAllBytes(logPath, originalLog);
                }
            }
        }
    }
}
