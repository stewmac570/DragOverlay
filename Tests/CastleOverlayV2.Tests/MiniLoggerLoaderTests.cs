using CastleOverlayV2.Services;
using System.Globalization;
using System.Text;

namespace CastleOverlayV2.Tests
{
    public class MiniLoggerLoaderTests
    {
        // 3 s at 1 kHz: 1 s at rest (1.50 ms), then full throttle (1.90 ms) with RPM climbing.
        private static string WriteRun()
        {
            var sb = new StringBuilder(MiniLoggerLoader.HeaderLine).AppendLine();
            for (int i = 0; i < 3000; i++)
            {
                double t = i / 1000.0;
                double throttle = t < 1.0 ? 1.50 : 1.90;
                double rpm = t < 1.0 ? 0 : (t - 1.0) * 20000;
                double wheel = t < 1.0 ? 0 : (t - 1.0) * 30;
                sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"{t:0.0000},{throttle:0.000},{rpm:0},{wheel:0.00}"));
            }
            string path = Path.Combine(Path.GetTempPath(), $"minilogger-{Guid.NewGuid():N}.csv");
            File.WriteAllText(path, sb.ToString());
            return path;
        }

        [Fact]
        public void Trimmed_by_default_and_full_when_asked()
        {
            string path = WriteRun();
            try
            {
                var trimmed = MiniLoggerLoader.Load(path).Value!.Data[MiniLoggerLoader.ThrottleChannel];
                var full = MiniLoggerLoader.Load(path, keepFullLog: true).Value!.Data[MiniLoggerLoader.ThrottleChannel];

                // Launch (the throttle step at 1 s) is t = 0 either way.
                Assert.InRange(trimmed.First().Time, -0.5, -0.49);
                Assert.Equal(3000, full.Count);
                Assert.InRange(full.First().Time, -1.01, -0.99);
                Assert.InRange(full.Last().Time, 1.99, 2.0);
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
