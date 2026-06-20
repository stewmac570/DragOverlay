using CastleOverlayV2.Services;

namespace CastleOverlayV2.Tests
{
    public class CastleTuneLoaderTests
    {
        [Fact]
        public void Load_ParsesCastleTuneExport()
        {
            string path = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..",
                "tune settnigs",
                "yeti tune.dat"));

            Assert.True(File.Exists(path), $"Missing sample tune file: {path}");

            var result = new CastleTuneLoader().Load(path);

            Assert.True(result.Ok, result.Message);
            var tune = result.Value!;

            Assert.Equal("yeti tune.dat", tune.SourceFileName);
            Assert.Equal("GhostRC Cobra 8 Ace", tune.DeviceName);
            Assert.Equal("1.36", tune.Firmware);
            Assert.Equal(257, tune.ThrottleCurve.Count);
            Assert.Equal(257, tune.BoostByThrottleCurve.Count);
            Assert.False(tune.BoostEnabled);
            Assert.True(tune.TurboEnabled);
            Assert.Equal(32, tune.TurboTimingAmountDeg);
            Assert.Equal(85, tune.TurboActivationThrottlePct);
            Assert.Equal(45, tune.TurboAccelDegPerSecond);
            Assert.Equal(60, tune.TurboDecelDegPerSecond);
            Assert.Equal("100%", tune.MaxPower);
            Assert.Equal("17.5", tune.DragBrake);

            Assert.InRange(tune.ThrottleCurve[^1].OutputPercent, 99.9, 100.0);
            Assert.InRange(tune.BoostByThrottleCurve.Max(p => p.OutputPercent), 59.9, 60.1);
        }

        [Fact]
        public void Load_ReturnsErrorForNonCastleText()
        {
            string path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, "not a tune file");

                var result = new CastleTuneLoader().Load(path);

                Assert.False(result.Ok);
                Assert.Equal("Tune Import Failed", result.Title);
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
