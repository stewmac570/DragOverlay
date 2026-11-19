using CastleOverlayV2.Models;
using CastleOverlayV2.Services;
using Xunit;

namespace CastleOverlayV2.Tests;

// The TC curve as a logger channel (Stew, 19 Nov 2026): starts at the logger's launch, stops where it left ACTIVE.
public class TcCurveChannelsTests
{
    private static List<MiniLoggerSample> Samples(double launchSec, double endSec)
    {
        var list = new List<MiniLoggerSample>();
        for (int ms = 0; ms <= 10000; ms += 10)
        {
            double t = ms / 1000.0 - 5.0;
            int state = t < launchSec ? 1 : t < endSec ? 2 : 3;
            list.Add(new MiniLoggerSample(ms / 1000.0, t, 1.5, 0, 0, 1.5, state));
        }
        return list;
    }

    [Fact]
    public void CurveStartsAtLaunchAndStopsWhenActiveEnds()
    {
        var curve = new List<TcCurvePoint> { new(0, 10000), new(1000, 30000), new(4000, 50000) };
        var points = TcCurveChannels.Points(Samples(-1.0, 1.0), curve)!;

        Assert.Equal(-1.0, points[0].Time, 6);
        Assert.Equal(10000, points[0].Y);
        Assert.Equal(0.0, points[1].Time, 6);
        Assert.Equal(1.0, points[^1].Time, 6);          // cut where ACTIVE ended, 2 s in
        Assert.Equal(30000 + 20000.0 / 3, points[^1].Y, 3); // a third of the way from 30000 to 50000
    }

    [Fact]
    public void NoLaunchMeansNoCurve()
    {
        var samples = Samples(99, 99);
        Assert.Null(TcCurveChannels.Points(samples, new List<TcCurvePoint> { new(0, 1), new(10, 2) }));
    }
}
