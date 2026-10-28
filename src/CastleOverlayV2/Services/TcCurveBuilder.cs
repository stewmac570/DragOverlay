using CastleOverlayV2.Models;
using System.Globalization;

namespace CastleOverlayV2.Services;

public enum TcMarginUnit
{
    Rpm,
    Percent
}

public enum TcLaunchSource
{
    TcState,
    LoggerStatus,
    FirmwareDefaults
}

public sealed record TcLaunchResult(
    double TimeSeconds,
    TcLaunchSource Source,
    double NeutralUs,
    double FullUs,
    double LaunchPercent)
{
    public double LaunchUs => NeutralUs + (FullUs - NeutralUs) * LaunchPercent / 100.0;

    public string Description => Source switch
    {
        TcLaunchSource.TcState => "Time zero: first TC ACTIVE row (TC State = 2).",
        TcLaunchSource.LoggerStatus => string.Create(CultureInfo.InvariantCulture,
            $"Time zero: {LaunchUs:0} µs from this session's logger status " +
            $"(neutral {NeutralUs:0}, full {FullUs:0}, launch {LaunchPercent:0.##}%)."),
        _ => string.Create(CultureInfo.InvariantCulture,
            $"Time zero: {LaunchUs:0} µs using firmware defaults " +
            $"(run rest {NeutralUs:0}, full rest + 400, launch 20%).")
    };
}

public sealed record TcRpmSample(double TimeSeconds, double Rpm);

public sealed record TcCurveBuildResult(
    TcLaunchResult Launch,
    IReadOnlyList<TcRpmSample> RpmTrace,
    IReadOnlyList<TcCurvePoint> Points);

/// <summary>Builds firmware-ready TC curves from raw Mini Logger samples.</summary>
public static class TcCurveBuilder
{
    public const int MaximumPoints = 128;
    public const double DefaultLaunchPercent = 20.0;
    public const double DefaultTravelUs = 400.0;

    public static TcLaunchResult FindLaunch(RunData run, TcLoggerStatus? loggerStatus = null)
    {
        ArgumentNullException.ThrowIfNull(run);
        IReadOnlyList<MiniLoggerSample> samples = run.MiniLoggerSamples;
        if (!run.IsMiniLogger || samples.Count == 0)
            throw new ArgumentException("The selected run is not a loaded Mini Logger run.", nameof(run));

        for (int i = 0; i < samples.Count; i++)
        {
            if (samples[i].TcState == 2 && (i == 0 || samples[i - 1].TcState != 2))
                return new TcLaunchResult(samples[i].TimeSeconds, TcLaunchSource.TcState, 0, 0, 0);
        }

        double neutralUs;
        double fullUs;
        double launchPercent;
        TcLaunchSource source;
        if (loggerStatus is { IsTcLogger: true, NeutralUs: > 0, FullUs: > 0 } &&
            loggerStatus.Settings.TryGetValue("launchPct", out double statusLaunchPercent))
        {
            neutralUs = loggerStatus.NeutralUs;
            fullUs = loggerStatus.FullUs;
            launchPercent = statusLaunchPercent;
            source = TcLaunchSource.LoggerStatus;
        }
        else
        {
            var rest = samples.Where(sample => sample.ThrottleMs > 0)
                .Take(200)
                .Select(sample => sample.ThrottleMs * 1000.0)
                .OrderBy(value => value)
                .ToList();
            if (rest.Count == 0)
                throw new InvalidOperationException("The selected run has no throttle signal.");
            neutralUs = rest[rest.Count / 2];
            fullUs = neutralUs + DefaultTravelUs;
            launchPercent = DefaultLaunchPercent;
            source = TcLaunchSource.FirmwareDefaults;
        }

        double launchUs = neutralUs + (fullUs - neutralUs) * launchPercent / 100.0;
        bool forwardIncreases = fullUs >= neutralUs;
        bool IsPastLaunch(MiniLoggerSample sample) => sample.ThrottleMs > 0 &&
            (forwardIncreases ? sample.ThrottleMs * 1000.0 >= launchUs
                              : sample.ThrottleMs * 1000.0 <= launchUs);
        int bestStart = -1;
        double bestHeld = -1;
        for (int i = 0; i < samples.Count; i++)
        {
            if (!IsPastLaunch(samples[i]) || (i > 0 && IsPastLaunch(samples[i - 1])))
                continue;
            int end = i;
            while (end + 1 < samples.Count && IsPastLaunch(samples[end + 1])) end++;
            double held = samples[end].TimeSeconds - samples[i].TimeSeconds;
            if (held > bestHeld)
            {
                bestHeld = held;
                bestStart = i;
            }
            i = end;
        }
        if (bestStart < 0)
            throw new InvalidOperationException(string.Create(CultureInfo.InvariantCulture,
                $"The selected run never crossed the logger launch threshold ({launchUs:0} µs)."));

        return new TcLaunchResult(samples[bestStart].TimeSeconds, source, neutralUs, fullUs, launchPercent);
    }

    public static TcCurveBuildResult Build(
        RunData run,
        double margin,
        TcMarginUnit marginUnit,
        double curveEndSeconds,
        TcLoggerStatus? loggerStatus = null)
    {
        if (!double.IsFinite(margin) || margin < 0)
            throw new ArgumentOutOfRangeException(nameof(margin), "Margin must be zero or greater.");
        if (!double.IsFinite(curveEndSeconds) || curveEndSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(curveEndSeconds), "Curve end must be greater than zero.");

        TcLaunchResult launch = FindLaunch(run, loggerStatus);
        int endMs = checked((int)Math.Round(curveEndSeconds * 1000.0, MidpointRounding.AwayFromZero));
        if (endMs < 1)
            throw new ArgumentOutOfRangeException(nameof(curveEndSeconds), "Curve end must be at least 1 ms.");

        IReadOnlyList<MiniLoggerSample> samples = run.MiniLoggerSamples;
        double endTime = launch.TimeSeconds + endMs / 1000.0;
        if (endTime > samples[^1].TimeSeconds)
            throw new InvalidOperationException("The curve end is beyond the selected run.");

        var trace = new List<TcRpmSample>
        {
            new(0, InterpolateRpm(samples, launch.TimeSeconds))
        };
        trace.AddRange(samples
            .Where(sample => sample.TimeSeconds > launch.TimeSeconds && sample.TimeSeconds < endTime)
            .Select(sample => new TcRpmSample(sample.TimeSeconds - launch.TimeSeconds, sample.MotorRpm)));
        trace.Add(new TcRpmSample(endMs / 1000.0, InterpolateRpm(samples, endTime)));

        int ApplyMargin(double rpm)
        {
            double withMargin = marginUnit == TcMarginUnit.Percent
                ? rpm * (1.0 + margin / 100.0)
                : rpm + margin;
            if (!double.IsFinite(withMargin) || withMargin > int.MaxValue)
                throw new InvalidOperationException("The requested margin produces an RPM outside the supported range.");
            return Math.Max(1, (int)Math.Round(withMargin, MidpointRounding.AwayFromZero));
        }

        // Round to the firmware's whole-ms clock and collapse duplicate milliseconds. A local
        // RPM maximum wins so a narrow peak is not lost before shape-preserving thinning.
        var byMillisecond = new SortedDictionary<int, int>();
        foreach (TcRpmSample sample in trace)
        {
            int timeMs = Math.Clamp(
                (int)Math.Round(sample.TimeSeconds * 1000.0, MidpointRounding.AwayFromZero), 0, endMs);
            int rpm = ApplyMargin(sample.Rpm);
            if (!byMillisecond.TryGetValue(timeMs, out int existing) || rpm > existing)
                byMillisecond[timeMs] = rpm;
        }
        byMillisecond[0] = ApplyMargin(InterpolateRpm(samples, launch.TimeSeconds));
        byMillisecond[endMs] = ApplyMargin(InterpolateRpm(samples, endTime));

        var points = byMillisecond.Select(pair => new TcCurvePoint(pair.Key, pair.Value)).ToList();
        if (points.Count > MaximumPoints)
            points = LargestTriangleThreeBuckets(points, MaximumPoints);

        Validate(points);
        return new TcCurveBuildResult(launch, trace, points);
    }

    public static void Validate(IReadOnlyList<TcCurvePoint> points)
    {
        if (points.Count is < 1 or > MaximumPoints)
            throw new InvalidOperationException($"The curve must contain 1 to {MaximumPoints} points.");
        for (int i = 0; i < points.Count; i++)
        {
            if (points[i].TimeMs < 0)
                throw new InvalidOperationException("Curve times must be whole milliseconds at or after zero.");
            if (points[i].Rpm <= 0)
                throw new InvalidOperationException("Curve RPM values must be whole numbers greater than zero.");
            if (i > 0 && points[i].TimeMs <= points[i - 1].TimeMs)
                throw new InvalidOperationException("Curve times must be strictly rising whole milliseconds.");
        }
    }

    private static double InterpolateRpm(IReadOnlyList<MiniLoggerSample> samples, double time)
    {
        if (time <= samples[0].TimeSeconds) return samples[0].MotorRpm;
        if (time >= samples[^1].TimeSeconds) return samples[^1].MotorRpm;

        int high = 1;
        while (high < samples.Count && samples[high].TimeSeconds < time) high++;
        MiniLoggerSample before = samples[high - 1];
        MiniLoggerSample after = samples[high];
        double span = after.TimeSeconds - before.TimeSeconds;
        if (span <= 0) return after.MotorRpm;
        double fraction = (time - before.TimeSeconds) / span;
        return before.MotorRpm + (after.MotorRpm - before.MotorRpm) * fraction;
    }

    // LTTB retains endpoints and the largest visual feature in each time bucket.
    private static List<TcCurvePoint> LargestTriangleThreeBuckets(
        IReadOnlyList<TcCurvePoint> data,
        int threshold)
    {
        if (threshold >= data.Count || threshold < 3)
            return data.Take(threshold).ToList();

        var sampled = new List<TcCurvePoint>(threshold) { data[0] };
        double bucketSize = (data.Count - 2.0) / (threshold - 2.0);
        int selectedIndex = 0;

        for (int bucket = 0; bucket < threshold - 2; bucket++)
        {
            int averageStart = (int)Math.Floor((bucket + 1) * bucketSize) + 1;
            int averageEnd = Math.Min((int)Math.Floor((bucket + 2) * bucketSize) + 1, data.Count);
            if (averageStart >= data.Count) averageStart = data.Count - 1;
            if (averageEnd <= averageStart) averageEnd = averageStart + 1;

            double averageX = 0;
            double averageY = 0;
            int averageCount = averageEnd - averageStart;
            for (int i = averageStart; i < averageEnd; i++)
            {
                averageX += data[i].TimeMs;
                averageY += data[i].Rpm;
            }
            averageX /= averageCount;
            averageY /= averageCount;

            int rangeStart = (int)Math.Floor(bucket * bucketSize) + 1;
            int rangeEnd = Math.Min((int)Math.Floor((bucket + 1) * bucketSize) + 1, data.Count - 1);
            TcCurvePoint anchor = data[selectedIndex];
            double largestArea = -1;
            int nextIndex = rangeStart;
            for (int i = rangeStart; i < rangeEnd; i++)
            {
                double area = Math.Abs(
                    (anchor.TimeMs - averageX) * (data[i].Rpm - anchor.Rpm) -
                    (anchor.TimeMs - data[i].TimeMs) * (averageY - anchor.Rpm));
                if (area > largestArea)
                {
                    largestArea = area;
                    nextIndex = i;
                }
            }
            sampled.Add(data[nextIndex]);
            selectedIndex = nextIndex;
        }

        sampled.Add(data[^1]);
        return sampled;
    }
}
