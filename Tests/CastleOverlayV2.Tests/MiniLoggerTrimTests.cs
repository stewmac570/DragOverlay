using CastleOverlayV2.Models;
using CastleOverlayV2.Services;
using Xunit;

namespace CastleOverlayV2.Tests;

// Trim a TC (Mini Logger) log on the Logs chart like the other logs (Stew, 22 Nov 2026).
[Collection("TcCurveStore")]
public class MiniLoggerTrimTests
{
    private static RunData LoggerRun()
    {
        var run = new RunData { IsMiniLogger = true };
        var rpm = new List<DataPoint>();
        var throttle = new List<DataPoint>();
        for (int ms = -500; ms <= 2500; ms += 10)
        {
            double t = ms / 1000.0;
            rpm.Add(new DataPoint { Time = t, Y = ms * 10 });
            throttle.Add(new DataPoint { Time = t, Y = 50 });
            int state = t < 0 ? 1 : t < 2.0 ? 2 : 3;
            run.MiniLoggerSamples.Add(new MiniLoggerSample(t + 10, t, 1.5, ms * 10, 0, 1.5, state));
        }
        run.Data[MiniLoggerLoader.RpmChannel] = rpm;
        run.Data[MiniLoggerLoader.ThrottleChannel] = throttle;
        run.DataPoints = throttle.Select(p => new DataPoint { Time = p.Time }).ToList();
        return run;
    }

    [Fact]
    public void TrimCutsEveryChannelAndUndoPutsItBack()
    {
        RunData run = LoggerRun();
        int full = run.Data[MiniLoggerLoader.RpmChannel].Count;
        run.CaptureTrimBaseline();

        Assert.True(run.CanManualTrim);
        Assert.True(run.SetTrimWindow(0.0, 1.0));
        Assert.All(run.Data.Values, list => Assert.All(list, p => Assert.InRange(p.Time, 0.0, 1.0)));
        Assert.Equal(run.Data[MiniLoggerLoader.ThrottleChannel].Count, run.DataPoints.Count);

        Assert.True(run.UndoTrim());
        Assert.Equal(full, run.Data[MiniLoggerLoader.RpmChannel].Count);
    }

    [Fact]
    public void TrimThatWouldRemoveEverythingIsIgnored()
    {
        RunData run = LoggerRun();
        run.CaptureTrimBaseline();

        Assert.False(run.SetTrimWindow(5.0, null));
        Assert.Null(run.TrimStartTime);
    }

    [Fact]
    public void TrimCutsTheTcCurveToo()
    {
        RunData run = LoggerRun();
        TcCurveStore.Set(new List<TcCurvePoint> { new(0, 1000), new(500, 20000), new(1900, 50000) },
            Array.Empty<TcCurvePoint>());
        TcCurveChannels.Apply(run);
        run.CaptureTrimBaseline();

        run.SetTrimWindow(null, 1.0);
        TcCurveChannels.Apply(run);

        Assert.All(run.Data[MiniLoggerLoader.TcCurveChannel], p => Assert.True(p.Time <= 1.0));
        Assert.False(run.BaselineData!.ContainsKey(MiniLoggerLoader.TcCurveChannel));
    }

    [Fact]
    public void RaceBoxRunsStillCannotBeTrimmed()
    {
        Assert.False(new RunData { IsRaceBox = true }.CanManualTrim);
    }
}
