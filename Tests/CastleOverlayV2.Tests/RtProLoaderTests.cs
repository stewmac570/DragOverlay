using CastleOverlayV2.Services;

namespace CastleOverlayV2.Tests
{
    public class RtProLoaderTests
    {
        // A real RT Pro session cut to 15-60 s, with time-of-day and position columns blanked.
        private static string SamplePath => Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "rtpro logs", "RTProTree_sample_telemetry.csv"));

        [Fact]
        public void Recognises_rtpro_files_only()
        {
            Assert.True(RtProLoader.IsRtProCsv(SamplePath));

            // A file that exists in the repo but isn't RT Pro (a missing file would also read false).
            string other = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "tune settnigs", "yeti tune.dat"));
            Assert.True(File.Exists(other), other);
            Assert.False(RtProLoader.IsRtProCsv(other));
        }

        [Fact]
        public void Loads_the_pass_zeroed_at_launch_like_a_racebox_run()
        {
            var result = RtProLoader.Load(SamplePath);
            Assert.True(result.Ok, result.Message);
            var (header, points) = result.Value;

            // Same -0.5 s .. +2.5 s window as RaceBox runs.
            Assert.InRange(points.First().Time.TotalSeconds, -0.5, -0.4);
            Assert.InRange(points.Last().Time.TotalSeconds, 2.4, 2.5);

            // Standing at t = 0, then the pass: peaks around 91 mph inside the window.
            var atLaunch = points.First(p => p.Time.TotalSeconds >= 0);
            Assert.True(atLaunch.SpeedMph <= 0.5);
            Assert.InRange(points.Max(p => p.SpeedMph), 85, 95);

            // Launch pulls positive forward g (the device's x axis points backwards).
            Assert.True(points.Where(p => p.Time.TotalSeconds is > 0 and < 0.5).Max(p => p.GForceX) > 2.0);

            // 66 ft then 132 ft, both inside the first second of a 90 mph pass.
            Assert.Equal(new[] { "66 feet", "132 feet" }, header.SplitLabels);
            Assert.Equal(2, header.SplitTimes!.Count);
            Assert.InRange(header.SplitTimes[0], 0.2, 1.0);
            Assert.InRange(header.SplitTimes[1], header.SplitTimes[0], 1.5);
        }

        [Fact]
        public void Full_log_keeps_every_sample_with_launch_still_at_zero()
        {
            var trimmed = RtProLoader.Load(SamplePath).Value.Points;
            var full = RtProLoader.Load(SamplePath, keepFullLog: true).Value.Points;

            // The sample covers 15-60 s of the session and the launch is near 39 s.
            Assert.True(full.Count > trimmed.Count * 10);
            Assert.InRange(full.First().Time.TotalSeconds, -25, -20);
            Assert.InRange(full.Last().Time.TotalSeconds, 15, 25);
            Assert.True(full.First(p => p.Time.TotalSeconds >= 0).SpeedMph <= 0.5);
        }

        [Fact]
        public void Launch_is_the_standstill_before_the_fastest_pass()
        {
            // A slow roll (12 mph) then a stop, then the real pass.
            var speeds = new List<double> { 0, 5, 12, 6, 0, 0, 0.2, 30, 60, 80, 40, 0 };
            Assert.Equal(6, RtProLoader.FindLaunchIndex(speeds));
        }

        [Fact]
        public void No_launch_when_nothing_is_fast_enough()
        {
            Assert.Equal(-1, RtProLoader.FindLaunchIndex(new List<double> { 0, 2, 4, 3, 0 }));
        }
    }
}
