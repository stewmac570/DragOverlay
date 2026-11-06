using CastleOverlayV2.Models;
using CastleOverlayV2.Services;
using System.Reflection;
using System.Runtime.CompilerServices;

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

            // A clean machine (the release build) has no DragOverlay folder yet.
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

            try
            {
                var configService = (ConfigService)RuntimeHelpers.GetUninitializedObject(
                    typeof(ConfigService));
                typeof(ConfigService)
                    .GetField("_config", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(configService, new Config { EnableDebugLogging = true });
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
                    if (File.Exists(logPath))
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
