using CastleOverlayV2.Models;

namespace CastleOverlayV2.Services;

/// <summary>
/// Puts the TC curves on a TC logger run as two channels, "Logger TC Curve" and "Logger TC Soft
/// Curve", so they sit in the channel list with their own on/off tick (Stew, 19 Nov 2026). Time zero
/// is the logger's launch (first TC State = ACTIVE row). The curve stops where the logger left ACTIVE.
/// </summary>
public static class TcCurveChannels
{
    public static void Apply(RunData run)
    {
        if (!run.IsMiniLogger) return;
        Set(run, MiniLoggerLoader.TcCurveChannel, TcCurveStore.Hard);
        Set(run, MiniLoggerLoader.TcSoftCurveChannel, TcCurveStore.Soft);
    }

    private static void Set(RunData run, string channel, IReadOnlyList<TcCurvePoint> curve)
    {
        run.Data.Remove(channel);
        List<DataPoint>? points = Points(run.MiniLoggerSamples, curve);
        if (points is not null) run.Data[channel] = points;
    }

    public static List<DataPoint>? Points(IReadOnlyList<MiniLoggerSample> samples, IReadOnlyList<TcCurvePoint> curve)
    {
        if (curve.Count < 2) return null;
        int launch = -1;
        for (int i = 0; i < samples.Count; i++)
            if (samples[i].TcState == 2) { launch = i; break; }
        if (launch < 0) return null;
        int after = -1;
        for (int i = launch; i < samples.Count; i++)
            if (samples[i].TcState != 2) { after = i; break; }
        double launchSec = samples[launch].LogsPageTimeSeconds;
        double endSec = (after < 0 ? samples[^1] : samples[after]).LogsPageTimeSeconds;

        var points = new List<DataPoint>();
        for (int i = 0; i < curve.Count; i++)
        {
            double x = launchSec + curve[i].TimeMs / 1000.0;
            if (x <= endSec)
            {
                points.Add(new DataPoint { Time = x, Y = curve[i].Rpm });
                continue;
            }
            if (i > 0)  // stop the line where the logger left ACTIVE
            {
                double x0 = launchSec + curve[i - 1].TimeMs / 1000.0;
                double f = (endSec - x0) / (x - x0);
                points.Add(new DataPoint { Time = endSec, Y = curve[i - 1].Rpm + f * (curve[i].Rpm - curve[i - 1].Rpm) });
            }
            break;
        }
        return points.Count >= 2 ? points : null;
    }
}
