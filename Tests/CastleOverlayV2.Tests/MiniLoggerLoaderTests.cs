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

        [Fact]
        public void Loads_a_raw_capture_with_begin_and_end_lines()
        {
            string path = WriteRun();
            try
            {
                // A serial capture that kept the logger's BEGIN / END markers.
                File.WriteAllText(path, "BEGIN run_0013.csv\r\n" + File.ReadAllText(path) + "END\r\n");

                Assert.True(MiniLoggerLoader.IsMiniLoggerCsv(path));
                var result = MiniLoggerLoader.Load(path, keepFullLog: true);
                Assert.True(result.Ok, result.Message);
                Assert.Equal(3000, result.Value!.Data[MiniLoggerLoader.ThrottleChannel].Count);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void Six_column_tc_csv_loads_throttle_out_and_state()
        {
            string path = WriteTcRun(includeBegin: false);
            try
            {
                Assert.True(MiniLoggerLoader.IsMiniLoggerCsv(path));
                var result = MiniLoggerLoader.Load(path, keepFullLog: true);

                Assert.True(result.Ok, result.Message);
                var run = result.Value!;
                Assert.True(run.Data.ContainsKey(MiniLoggerLoader.ThrottleOutChannel));
                Assert.True(run.Data.ContainsKey(MiniLoggerLoader.TcStateChannel));
                Assert.Equal(3000, run.Data[MiniLoggerLoader.ThrottleOutChannel].Count);
                Assert.Contains(run.Data[MiniLoggerLoader.TcStateChannel], point => point.Y == 2);
                Assert.Contains(run.MiniLoggerSamples,
                    sample => sample.TcState == 2 && sample.ThrottleOutMs < sample.ThrottleMs);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void Six_column_tc_csv_with_begin_line_is_accepted()
        {
            string path = WriteTcRun(includeBegin: true);
            try
            {
                Assert.True(MiniLoggerLoader.IsMiniLoggerCsv(path));
                var result = MiniLoggerLoader.Load(path, keepFullLog: true);
                Assert.True(result.Ok, result.Message);
                Assert.Equal(3000, result.Value!.MiniLoggerSamples.Count);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void Four_column_csv_does_not_add_tc_channels()
        {
            string path = WriteRun();
            try
            {
                var result = MiniLoggerLoader.Load(path, keepFullLog: true);
                Assert.True(result.Ok, result.Message);
                Assert.DoesNotContain(MiniLoggerLoader.ThrottleOutChannel, result.Value!.Data.Keys);
                Assert.DoesNotContain(MiniLoggerLoader.TcStateChannel, result.Value.Data.Keys);
                Assert.All(result.Value.MiniLoggerSamples, sample =>
                {
                    Assert.Null(sample.ThrottleOutMs);
                    Assert.Null(sample.TcState);
                });
            }
            finally
            {
                File.Delete(path);
            }
        }

        private static string WriteTcRun(bool includeBegin)
        {
            var sb = new StringBuilder();
            if (includeBegin) sb.AppendLine("BEGIN run_0042.csv");
            sb.AppendLine(MiniLoggerLoader.HeaderLine + ",Throttle Out (ms),TC State");
            for (int i = 0; i < 3000; i++)
            {
                double t = i / 1000.0;
                double throttle = t < 1.0 ? 1.50 : 1.90;
                double output = t is >= 1.2 and < 1.5 ? 1.70 : throttle;
                int state = t is >= 1.0 and < 2.5 ? 2 : 0;
                double rpm = t < 1.0 ? 0 : (t - 1.0) * 20000;
                double wheel = t < 1.0 ? 0 : (t - 1.0) * 30;
                sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
                    $"{t:0.000},{throttle:0.000},{rpm:0},{wheel:0.00},{output:0.000},{state}"));
            }
            if (includeBegin) sb.AppendLine("END");
            string path = Path.Combine(Path.GetTempPath(), $"tc-logger-{Guid.NewGuid():N}.csv");
            File.WriteAllText(path, sb.ToString());
            return path;
        }
    }
}
