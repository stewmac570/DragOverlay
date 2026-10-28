using CastleOverlayV2.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace CastleOverlayV2.Services
{
    /// <summary>
    /// Loads a Mini Logger run CSV (rc-logger firmware/mini_logger, 1000 Hz).
    /// Columns: Time (s), Throttle (ms), Motor RPM, Wheel (rev/s). Motor RPM is real motor
    /// RPM (already divided by pole pairs). Throttle 0 = no receiver signal.
    ///
    /// The run is re-zeroed so t = 0 is the throttle step of the drag pass (throttle crossing
    /// 50% of rest → peak), then trimmed to the same -0.5 s → +2.5 s window as Castle logs.
    /// </summary>
    public static class MiniLoggerLoader
    {
        public const string HeaderLine = "Time (s),Throttle (ms),Motor RPM,Wheel (rev/s)";

        // Channel keys (also used by PlotManager, ChannelDrawer, ColorMap and the presenter).
        public const string ThrottleChannel = "Logger Throttle %";
        public const string ThrottleOutChannel = "Logger Throttle Out %";
        public const string TcStateChannel = "Logger TC State";
        public const string RpmChannel = "Logger RPM";
        public const string WheelSpeedChannel = "Logger Wheel Speed";
        public const string RearSpeedChannel = "Logger Rear Speed";
        public const string SlipChannel = "Logger Slip %";
        public const string WheelRevsChannel = "Logger Wheel rev/s";

        public static readonly string[] Channels =
        {
            ThrottleChannel, ThrottleOutChannel, TcStateChannel, RpmChannel, WheelSpeedChannel,
            RearSpeedChannel, SlipChannel, WheelRevsChannel
        };

        public const double DefaultTyreDiameterMm = 70.0;
        private const double MetersPerSecToMph = 2.23694;
        public static double MphPerRevPerSec(double tyreDiameterMm)
        {
            if (!double.IsFinite(tyreDiameterMm) || tyreDiameterMm <= 0)
                tyreDiameterMm = DefaultTyreDiameterMm;
            return Math.PI * tyreDiameterMm / 1000.0 * MetersPerSecToMph;
        }

        public static double RearSpeedMph(
            double motorRpm,
            double rearWheelDiameterMm,
            int pinionTeeth,
            int spurTeeth,
            double transmissionRatio = 1.0)
        {
            double rearWheelRevsPerSecond = (motorRpm / 60.0) /
                ((double)spurTeeth / pinionTeeth) / transmissionRatio;
            return rearWheelRevsPerSecond * MphPerRevPerSec(rearWheelDiameterMm);
        }

        public static double SlipPercent(double rearSpeedMph, double frontSpeedMph)
        {
            if (rearSpeedMph < 2.0)
                return 0.0;

            return Math.Clamp((rearSpeedMph - frontSpeedMph) / rearSpeedMph * 100.0,
                -100.0, 100.0);
        }

        // Throttle crossing this share of rest → peak marks the launch (and the alignment point).
        public const double LaunchThrottlePercent = 50.0;

        private const double PreLaunchSeconds = 0.5;
        private const double PostLaunchSeconds = 2.5;
        private const int RestSampleCount = 200;          // firmware keeps 1 s before the trigger
        private const double MinThrottleTravelMs = 0.1;    // firmware trigger is 100 µs above rest

        /// <summary>
        /// True if the file starts with the Mini Logger CSV header. A raw serial capture that kept
        /// the logger's "BEGIN run_NNNN.csv" line above the header counts too; its "END" line is
        /// skipped by the row parser like any other non-numeric line.
        /// </summary>
        public static bool IsMiniLoggerCsv(string filePath)
        {
            try
            {
                using var reader = new StreamReader(filePath);
                string? first = reader.ReadLine()?.Trim().TrimStart('﻿');
                if (first != null && first.StartsWith("BEGIN ", StringComparison.Ordinal))
                    first = reader.ReadLine()?.Trim();
                return first != null && IsHeaderLine(first);
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        public static bool IsHeaderLine(string line) =>
            line.Equals(HeaderLine, StringComparison.OrdinalIgnoreCase) ||
            line.StartsWith(HeaderLine + ",", StringComparison.OrdinalIgnoreCase);

        public static LoadResult<RunData> Load(
            string filePath,
            bool keepFullLog = false,
            double tyreDiameterMm = DefaultTyreDiameterMm,
            double? rearWheelDiameterMm = null,
            int? pinionTeeth = null,
            int? spurTeeth = null,
            double? transmissionRatio = 1.0)
        {
            Logger.Log($"[MiniLoggerLoader] Load started: {Path.GetFileName(filePath)}");

            if (!IsMiniLoggerCsv(filePath))
                return LoadResult<RunData>.Error("Invalid File",
                    "This file is not a Mini Logger run.\n\nExpected the header:\n" + HeaderLine);

            var times = new List<double>();
            var throttleMs = new List<double>();
            var rpm = new List<double>();
            var wheelRps = new List<double>();
            var throttleOutMs = new List<double>();
            var tcStates = new List<int>();

            using var reader = new StreamReader(filePath);
            string? headerLine = reader.ReadLine()?.Trim().TrimStart('﻿');
            if (headerLine != null && headerLine.StartsWith("BEGIN ", StringComparison.Ordinal))
                headerLine = reader.ReadLine()?.Trim().TrimStart('﻿');
            if (headerLine is null || !IsHeaderLine(headerLine))
                return LoadResult<RunData>.Error("Invalid File",
                    "This file is not a Mini Logger run.\n\nExpected the header:\n" + HeaderLine);

            string[] header = headerLine.Split(',');
            int throttleOutIndex = Array.FindIndex(header,
                name => name.Trim().Equals("Throttle Out (ms)", StringComparison.OrdinalIgnoreCase));
            int tcStateIndex = Array.FindIndex(header,
                name => name.Trim().Equals("TC State", StringComparison.OrdinalIgnoreCase));
            bool hasTcColumns = throttleOutIndex >= 0 && tcStateIndex >= 0;

            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                string[] f = line.Split(',');
                if (f.Length < 4) continue;
                if (!double.TryParse(f[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double t) ||
                    !double.TryParse(f[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double thr) ||
                    !double.TryParse(f[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double r) ||
                    !double.TryParse(f[3], NumberStyles.Float, CultureInfo.InvariantCulture, out double w))
                    continue;

                double outMs = 0;
                int state = 0;
                if (hasTcColumns &&
                    (f.Length <= Math.Max(throttleOutIndex, tcStateIndex) ||
                     !double.TryParse(f[throttleOutIndex], NumberStyles.Float,
                         CultureInfo.InvariantCulture, out outMs) ||
                     !int.TryParse(f[tcStateIndex], NumberStyles.Integer,
                         CultureInfo.InvariantCulture, out state)))
                    continue;

                times.Add(t);
                throttleMs.Add(thr);
                rpm.Add(r);
                wheelRps.Add(w);
                if (hasTcColumns)
                {
                    throttleOutMs.Add(outMs);
                    tcStates.Add(state);
                }
            }

            Logger.Log($"[MiniLoggerLoader] Parsed {times.Count} rows");

            if (times.Count < 100)
                return LoadResult<RunData>.Error("Mini Logger Import Failed",
                    "This Mini Logger run has too few samples to plot.");

            // Rest = median of the first samples with a signal (the pre-trigger second).
            var restSamples = throttleMs.Where(v => v > 0).Take(RestSampleCount).OrderBy(v => v).ToList();
            if (restSamples.Count == 0)
                return LoadResult<RunData>.Error("Mini Logger Import Failed",
                    "No throttle signal was recorded in this Mini Logger run.");

            double restMs = restSamples[restSamples.Count / 2];
            double peakMs = throttleMs.Max();
            if (peakMs - restMs < MinThrottleTravelMs)
                return LoadResult<RunData>.Error("Mini Logger Import Failed",
                    "No throttle movement was found in this Mini Logger run.");

            var throttlePct = throttleMs
                .Select(v => v <= 0 ? 0.0 : Math.Clamp(100.0 * (v - restMs) / (peakMs - restMs), -100.0, 100.0))
                .ToList();
            var throttleOutPct = hasTcColumns
                ? throttleOutMs.Select(v => v <= 0
                    ? 0.0
                    : Math.Clamp(100.0 * (v - restMs) / (peakMs - restMs), -100.0, 100.0)).ToList()
                : new List<double>();

            double? launch = FindLaunchTime(times, throttlePct);
            if (launch == null)
                return LoadResult<RunData>.Error("Mini Logger Import Failed",
                    "No throttle step was found in this Mini Logger run.");

            double t0 = launch.Value;
            Logger.Log($"[MiniLoggerLoader] Throttle rest={restMs:F3}ms peak={peakMs:F3}ms launch t={t0:F4}s");

            var run = new RunData
            {
                IsMiniLogger = true,
                FileName = Path.GetFileName(filePath),
                SourcePath = filePath
            };

            var thrPts = new List<DataPoint>();
            var rpmPts = new List<DataPoint>();
            var mphPts = new List<DataPoint>();
            var rearMphPts = new List<DataPoint>();
            var slipPts = new List<DataPoint>();
            var rpsPts = new List<DataPoint>();
            var throttleOutPts = new List<DataPoint>();
            var tcStatePts = new List<DataPoint>();
            double effectiveTransmissionRatio = transmissionRatio ?? 1.0;
            bool produceRearChannels = rearWheelDiameterMm is > 0 &&
                pinionTeeth is > 0 && spurTeeth is > 0 &&
                double.IsFinite(effectiveTransmissionRatio) && effectiveTransmissionRatio > 0;

            for (int i = 0; i < times.Count; i++)
            {
                double t = times[i] - t0;
                run.MiniLoggerSamples.Add(new MiniLoggerSample(
                    times[i], t, throttleMs[i], rpm[i], wheelRps[i],
                    hasTcColumns ? throttleOutMs[i] : null,
                    hasTcColumns ? tcStates[i] : null));
                if (!keepFullLog && (t < -PreLaunchSeconds || t > PostLaunchSeconds)) continue;

                thrPts.Add(new DataPoint { Time = t, Y = throttlePct[i] });
                if (hasTcColumns)
                {
                    throttleOutPts.Add(new DataPoint { Time = t, Y = throttleOutPct[i] });
                    tcStatePts.Add(new DataPoint { Time = t, Y = tcStates[i] });
                }
                rpmPts.Add(new DataPoint { Time = t, Y = rpm[i] });
                double frontSpeedMph = wheelRps[i] * MphPerRevPerSec(tyreDiameterMm);
                mphPts.Add(new DataPoint { Time = t, Y = frontSpeedMph });
                if (produceRearChannels)
                {
                    double rearSpeedMph = RearSpeedMph(rpm[i], rearWheelDiameterMm!.Value,
                        pinionTeeth!.Value, spurTeeth!.Value, effectiveTransmissionRatio);
                    rearMphPts.Add(new DataPoint { Time = t, Y = rearSpeedMph });
                    slipPts.Add(new DataPoint { Time = t, Y = SlipPercent(rearSpeedMph, frontSpeedMph) });
                }
                rpsPts.Add(new DataPoint { Time = t, Y = wheelRps[i] });
            }

            run.Data[ThrottleChannel] = thrPts;
            if (hasTcColumns)
            {
                run.Data[ThrottleOutChannel] = throttleOutPts;
                run.Data[TcStateChannel] = tcStatePts;
            }
            run.Data[RpmChannel] = rpmPts;
            run.Data[WheelSpeedChannel] = mphPts;
            if (produceRearChannels)
            {
                run.Data[RearSpeedChannel] = rearMphPts;
                run.Data[SlipChannel] = slipPts;
            }
            run.Data[WheelRevsChannel] = rpsPts;
            // PlotManager skips runs with empty DataPoints — populate with time-only dummies.
            run.DataPoints = thrPts.Select(p => new DataPoint { Time = p.Time }).ToList();

            Logger.Log($"[MiniLoggerLoader] Trimmed to {thrPts.Count} samples around launch");
            return LoadResult<RunData>.Success(run);
        }

        /// <summary>
        /// The drag pass is the throttle-up that stays above <see cref="LaunchThrottlePercent"/>
        /// the longest (staging blips are short). Returns the interpolated crossing time.
        /// </summary>
        private static double? FindLaunchTime(List<double> times, List<double> pct)
        {
            double? best = null;
            double bestHeld = -1;

            for (int i = 1; i < pct.Count; i++)
            {
                if (!(pct[i - 1] < LaunchThrottlePercent && pct[i] >= LaunchThrottlePercent))
                    continue;

                int end = i;
                while (end + 1 < pct.Count && pct[end + 1] >= LaunchThrottlePercent)
                    end++;

                double held = times[end] - times[i];
                if (held > bestHeld)
                {
                    bestHeld = held;
                    best = AlignmentHelper.Interpolate(times[i - 1], pct[i - 1], times[i], pct[i], LaunchThrottlePercent);
                }
                i = end;
            }

            return best;
        }
    }
}
