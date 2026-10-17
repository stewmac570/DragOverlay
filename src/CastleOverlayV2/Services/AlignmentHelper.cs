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
        /// Offset (ms) that lines the Mini Logger's throttle step up with the Castle run's
        /// throttle step: both traces cross half of their own peak throttle at the same plot
        /// time. With no Castle run, the Mini Logger throttle step goes to t = 0.
        /// Returns null when either throttle trace has no clear step.
        /// </summary>
        public static double? GetMiniLoggerOffsetMs(RunData miniRun, RunData? castleRun)
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
