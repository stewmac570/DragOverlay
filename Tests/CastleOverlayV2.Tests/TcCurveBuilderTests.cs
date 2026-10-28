using CastleOverlayV2.Models;
using CastleOverlayV2.Services;

namespace CastleOverlayV2.Tests;

public sealed class TcCurveBuilderTests
{
    [Fact]
    public void Tc_state_active_defines_time_zero()
    {
        RunData run = Run(tcActiveAt: 0.75);

        TcLaunchResult launch = TcCurveBuilder.FindLaunch(run);

        Assert.Equal(TcLaunchSource.TcState, launch.Source);
        Assert.Equal(0.75, launch.TimeSeconds, 6);
    }

    [Fact]
    public void Logger_status_launch_us_defines_time_zero_for_plain_run()
    {
        RunData run = Run(tcActiveAt: null);
        var status = new TcLoggerStatus
        {
            IsTcLogger = true,
            NeutralUs = 1500,
            FullUs = 1900,
            Settings = new Dictionary<string, double> { ["launchPct"] = 25 }
        };

        TcLaunchResult launch = TcCurveBuilder.FindLaunch(run, status);

        Assert.Equal(TcLaunchSource.LoggerStatus, launch.Source);
        Assert.Equal(1600, launch.LaunchUs);
        Assert.Equal(0.5, launch.TimeSeconds, 6);
    }

    [Theory]
    [InlineData(TcMarginUnit.Rpm, 1000)]
    [InlineData(TcMarginUnit.Percent, 10)]
    public void Curve_applies_margin_ends_at_requested_time_and_is_firmware_valid(
        TcMarginUnit unit,
        double margin)
    {
        RunData run = Run(tcActiveAt: 0.75, sampleCount: 2500);

        TcCurveBuildResult result = TcCurveBuilder.Build(run, margin, unit, 1.25);

        Assert.Equal(0, result.Points[0].TimeMs);
        Assert.Equal(1250, result.Points[^1].TimeMs);
        Assert.InRange(result.Points.Count, 2, 128);
        Assert.All(result.Points, point => Assert.True(point.Rpm > 0));
        Assert.All(result.Points.Zip(result.Points.Skip(1)), pair =>
            Assert.True(pair.Second.TimeMs > pair.First.TimeMs));

        int expectedFirst = unit == TcMarginUnit.Rpm
            ? (int)Math.Round(result.RpmTrace[0].Rpm + margin)
            : (int)Math.Round(result.RpmTrace[0].Rpm * (1 + margin / 100.0));
        Assert.Equal(Math.Max(1, expectedFirst), result.Points[0].Rpm);
    }

    private static RunData Run(double? tcActiveAt, int sampleCount = 2000)
    {
        var run = new RunData { IsMiniLogger = true, FileName = "run.csv" };
        for (int i = 0; i < sampleCount; i++)
        {
            double time = i / 1000.0;
            double throttleMs = time < 0.5 ? 1.5 : 1.9;
            int? state = tcActiveAt.HasValue ? (time >= tcActiveAt.Value ? 2 : 1) : null;
            double rpm = 5000 + time * 20000 + (i % 83 == 0 ? 2000 : 0);
            run.MiniLoggerSamples.Add(new MiniLoggerSample(
                time, time - 0.5, throttleMs, rpm, time * 20, throttleMs, state));
        }
        return run;
    }
}
