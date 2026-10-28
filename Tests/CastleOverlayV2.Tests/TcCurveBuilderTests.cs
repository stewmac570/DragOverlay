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

    [Fact]
    public void Castle_launch_uses_median_rest_throttle_and_firmware_defaults()
    {
        RunData run = CastleRun();

        TcLaunchResult launch = TcCurveBuilder.FindLaunch(run);

        Assert.Equal(TcLaunchSource.CastleRunDefaults, launch.Source);
        Assert.Equal(1500, launch.NeutralUs);
        Assert.Equal(1580, launch.LaunchUs);
        Assert.Equal(0, launch.TimeSeconds, 6);
    }

    [Fact]
    public void Castle_launch_uses_tc_status_values_from_this_session()
    {
        RunData run = CastleRun();
        var status = new TcLoggerStatus
        {
            IsTcLogger = true,
            NeutralUs = 1500,
            FullUs = 1900,
            Settings = new Dictionary<string, double> { ["launchPct"] = 50 }
        };

        TcLaunchResult launch = TcCurveBuilder.FindLaunch(run, status);

        Assert.Equal(TcLaunchSource.LoggerStatus, launch.Source);
        Assert.Equal(1700, launch.LaunchUs);
        Assert.Equal(0.10, launch.TimeSeconds, 6);
    }

    [Fact]
    public void Castle_curve_uses_logs_chart_rpm_for_two_pole_and_four_pole()
    {
        RunData run = CastleRun();

        TcCurveBuildResult twoPole = TcCurveBuilder.Build(
            run, 0, TcMarginUnit.Rpm, 0.15, isFourPoleMode: false);
        TcCurveBuildResult fourPole = TcCurveBuilder.Build(
            run, 0, TcMarginUnit.Rpm, 0.15, isFourPoleMode: true);

        Assert.Equal(0, twoPole.Points[0].TimeMs);
        Assert.Equal(150, twoPole.Points[^1].TimeMs);
        Assert.InRange(twoPole.Points.Count, 2, TcCurveBuilder.MaximumPoints);
        Assert.Equal(twoPole.RpmTrace.Count, fourPole.RpmTrace.Count);
        Assert.All(twoPole.RpmTrace.Zip(fourPole.RpmTrace), pair =>
            Assert.Equal(pair.First.Rpm * 0.5, pair.Second.Rpm, 6));
        Assert.All(twoPole.Points.Zip(fourPole.Points), pair =>
            Assert.Equal(pair.First.Rpm / 2, pair.Second.Rpm));
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

    private static RunData CastleRun()
    {
        var run = new RunData { FileName = "castle.csv" };
        run.DataPoints.AddRange(new[]
        {
            new DataPoint { Time = -0.10, Throttle = 1.50, Speed = 8_000 },
            new DataPoint { Time = -0.05, Throttle = 1.50, Speed = 9_000 },
            new DataPoint { Time = 0.00, Throttle = 1.60, Speed = 10_000 },
            new DataPoint { Time = 0.05, Throttle = 1.65, Speed = 12_000 },
            new DataPoint { Time = 0.10, Throttle = 1.75, Speed = 14_000 },
            new DataPoint { Time = 0.15, Throttle = 1.85, Speed = 16_000 },
            new DataPoint { Time = 0.20, Throttle = 1.90, Speed = 18_000 },
            new DataPoint { Time = 0.25, Throttle = 1.90, Speed = 20_000 },
            new DataPoint { Time = 0.30, Throttle = 1.90, Speed = 22_000 }
        });
        return run;
    }
}
