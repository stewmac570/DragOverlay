using CastleOverlayV2.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CastleOverlayV2.Services
{
    public static class AlignmentHelper
    {
        /// <summary>
        /// Calculate the non-destructive offset required to place the detected launch at t = 0.
        /// Returns null when no clear launch can be found.
        /// </summary>
        public static double? GetAutoOffsetMs(RunData run)
        {
            if (run.IsMiniLogger)
                return GetMiniLoggerOffsetMs(run, null);

            if (!run.IsRaceBox)
            {
                foreach (var point in run.DataPoints)
                {
                    if (point.Throttle > 1.65 && point.PowerOutValid && point.PowerOut > 10)
                        return -point.Time * 1000.0;
                }

                return null;
            }

            if (!run.Data.TryGetValue("RaceBox Speed", out var speed))
                return null;

            for (int i = 0; i <= speed.Count - 3; i++)
            {
                if (speed[i].Y > 1.0 &&
                    speed[i + 1].Y > 1.0 &&
                    speed[i + 2].Y > 1.0)
                    return -speed[i].Time * 1000.0;
            }

            return null;
        }

        /// <summary>
        /// Offset (ms) that lines a Mini Logger or Big logger run up with the Castle run.
        /// It matches the whole RPM trace of both logs (<see cref="RpmMatchOffsetMs"/>). When
        /// either log has no usable RPM, it falls back to the throttle step: both traces cross
        /// half of their own peak throttle at the same plot time. With no Castle run, the
        /// logger's throttle step goes to t = 0. Returns null when nothing lines up.
        /// </summary>
        public static double? GetMiniLoggerOffsetMs(RunData miniRun, RunData? castleRun)
        {
            if (castleRun is not null && RpmMatchOffsetMs(miniRun, castleRun) is double rpmOffset)
                return rpmOffset;
            return ThrottleStepOffsetMs(miniRun, castleRun);
        }

        private const double RpmMinimumCorrelation = 0.5;

        /// <summary>
        /// Offset (ms) at which the logger's RPM trace best matches the Castle run's RPM trace,
        /// scored by correlation so the two logs' different RPM scales don't matter. It searches
        /// every offset where the logs overlap. The throttle step is not used as a starting point:
        /// on the 3 Nov 2026 bench test each log picked a different blip as its launch, which put
        /// the throttle step 3.4 s out. Null when either trace never changes or the best match is weak.
        /// </summary>
        public static double? RpmMatchOffsetMs(RunData miniRun, RunData castleRun)
        {
            List<MiniLoggerSample> samples = miniRun.MiniLoggerSamples;
            if (samples.Count < 100 || castleRun.DataPoints.Count < 20)
                return null;

            // Logger RPM on a 1 ms grid (the logger writes 1000 rows a second), so each lookup is direct.
            double gridStart = samples[0].LogsPageTimeSeconds;
            int gridLength = (int)Math.Round((samples[^1].LogsPageTimeSeconds - gridStart) * 1000.0) + 1;
            if (gridLength < 100 || gridLength > 50_000_000)
                return null;
            var grid = new double[gridLength];
            var filled = new bool[gridLength];
            foreach (MiniLoggerSample sample in samples)
            {
                int index = (int)Math.Round((sample.LogsPageTimeSeconds - gridStart) * 1000.0);
                if (index >= 0 && index < gridLength)
                {
                    grid[index] = sample.MotorRpm;
                    filled[index] = true;
                }
            }
            for (int i = 1; i < gridLength; i++)
                if (!filled[i])
                    grid[i] = grid[i - 1];

            double castleShift = castleRun.TimeShiftMs / 1000.0;
            double[] castleTimes = castleRun.DataPoints.Select(p => p.Time + castleShift).ToArray();
            double[] castleRpm = castleRun.DataPoints.Select(p => p.Speed).ToArray();
            if (castleRpm.Max() - castleRpm.Min() <= 0 || grid.Max() - grid.Min() <= 0)
                return null;

            double gridEnd = gridStart + (gridLength - 1) / 1000.0;
            double low = castleTimes[0] - gridEnd;
            double high = castleTimes[^1] - gridStart;
            if (high <= low)
                return null;

            double? Score(double lag)
            {
                // The logger sample shown at castle time t was recorded at logs-page time t - lag.
                int count = 0;
                double sumX = 0, sumY = 0, sumXX = 0, sumYY = 0, sumXY = 0;
                for (int i = 0; i < castleTimes.Length; i++)
                {
                    int index = (int)Math.Round((castleTimes[i] - lag - gridStart) * 1000.0);
                    if (index < 0 || index >= gridLength)
                        continue;
                    double x = castleRpm[i], y = grid[index];
                    count++;
                    sumX += x; sumY += y; sumXX += x * x; sumYY += y * y; sumXY += x * y;
                }
                if (count < Math.Max(20, castleTimes.Length / 2))
                    return null;
                double varX = sumXX - sumX * sumX / count;
                double varY = sumYY - sumY * sumY / count;
                if (varX <= 0 || varY <= 0)
                    return null;
                return (sumXY - sumX * sumY / count) / Math.Sqrt(varX * varY);
            }

            double bestLag = double.NaN, bestScore = double.NegativeInfinity;
            void Search(double from, double to, double step)
            {
                for (double lag = from; lag <= to + step / 2; lag += step)
                {
                    if (Score(lag) is double score && score > bestScore)
                    {
                        bestScore = score;
                        bestLag = lag;
                    }
                }
            }

            Search(low, high, 0.010);
            if (double.IsNaN(bestLag))
                return null;
            double coarse = bestLag;
            Search(Math.Max(low, coarse - 0.010), Math.Min(high, coarse + 0.010), 0.001);

            Logger.Log($"[AlignmentHelper] RPM match: offset {bestLag * 1000.0:F0} ms, correlation {bestScore:F3}");
            return bestScore >= RpmMinimumCorrelation ? bestLag * 1000.0 : null;
        }

        private static double? ThrottleStepOffsetMs(RunData miniRun, RunData? castleRun)
        {
            if (!miniRun.Data.TryGetValue(MiniLoggerLoader.ThrottleChannel, out var miniThr))
                return null;

            double? miniCross = ThrottleStepTime(miniThr.Select(p => (p.Time, p.Y)).ToList());
            if (miniCross == null)
                return null;

            double target = 0;
            if (castleRun != null)
            {
                double? castleCross = ThrottleStepTime(
                    castleRun.DataPoints.Select(p => (p.Time, p.ThrottlePercent)).ToList());
                if (castleCross == null)
                    return null;
                target = castleCross.Value + castleRun.TimeShiftMs / 1000.0;
            }

            return (target - miniCross.Value) * 1000.0;
        }

        /// <summary>
        /// Time the throttle rises through half its peak, on the rising edge nearest the run's
        /// own t = 0 (its detected launch). Linear interpolation between samples, so 20 Hz
        /// Castle data still gives a crossing between its samples.
        /// </summary>
        private static double? ThrottleStepTime(List<(double Time, double Pct)> pts)
        {
            if (pts.Count < 2)
                return null;

            double peak = pts.Max(p => p.Pct);
            if (peak < 10)
                return null;

            double threshold = peak * MiniLoggerLoader.LaunchThrottlePercent / 100.0;
            double? best = null;
            for (int i = 1; i < pts.Count; i++)
            {
                if (pts[i - 1].Pct < threshold && pts[i].Pct >= threshold)
                {
                    double t = Interpolate(pts[i - 1].Time, pts[i - 1].Pct, pts[i].Time, pts[i].Pct, threshold);
                    if (best == null || Math.Abs(t) < Math.Abs(best.Value))
                        best = t;
                }
            }
            return best;
        }

        /// <summary>Time at which a straight line from (t0, y0) to (t1, y1) reaches <paramref name="y"/>.</summary>
        public static double Interpolate(double t0, double y0, double t1, double y1, double y)
        {
            if (y1 == y0) return t1;
            return t0 + (y - y0) / (y1 - y0) * (t1 - t0);
        }
    }
}
