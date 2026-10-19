using CastleOverlayV2.Models;
using CsvHelper;
using CsvHelper.Configuration;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace CastleOverlayV2.Services
{
    /// <summary>
    /// Loads an RT Pro GPS telemetry CSV (files named "RTProTree_..._telemetry.csv", 25 Hz).
    /// One file is one recording session that can hold several rolls and one or more passes.
    /// The fastest pass is taken: t = 0 is the last standstill sample before it, and the data
    /// is trimmed to the same -0.5 s → +2.5 s window as RaceBox logs. The result is shaped
    /// like a RaceBox run (Speed, G-Force X, and 66 / 132 ft splits worked out from speed)
    /// so it loads into the RaceBox / Logger slots and plots on the RaceBox channels.
    /// </summary>
    public static class RtProLoader
    {
        private const double MpsToMph = 2.23694;
        private const double MphToFeetPerSec = 5280.0 / 3600.0;
        private const double StandstillMph = 0.5;
        private const double MinPassPeakMph = 10.0;
        private const double PreLaunchSeconds = 0.5;
        private const double PostLaunchSeconds = 2.5;

        // Split distances (feet) marked on the chart, like a RaceBox RC drag discipline.
        private static readonly double[] SplitFeet = { 66.0, 132.0 };

        private static readonly string[] RequiredColumns =
        {
            "run_id", "recording_seconds", "speed_mps", "corrected_accel_x_g"
        };

        /// <summary>True if the file's header has the RT Pro telemetry columns.</summary>
        public static bool IsRtProCsv(string filePath)
        {
            try
            {
                using var reader = new StreamReader(filePath);
                string? first = reader.ReadLine();
                if (first == null) return false;
                var columns = first.Split(',').Select(c => c.Trim().Trim('"').TrimStart('﻿')).ToHashSet(StringComparer.OrdinalIgnoreCase);
                return RequiredColumns.All(columns.Contains);
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

        /// <summary>
        /// Read the fastest pass. Points are re-zeroed to the launch and trimmed; the header
        /// carries the 66 / 132 ft split times (seconds after launch) for the split lines.
        /// </summary>
        public static LoadResult<(RaceBoxData Header, List<RaceBoxPoint> Points)> Load(string filePath)
        {
            Logger.Log($"[RtProLoader] Load started: {Path.GetFileName(filePath)}");

            if (!IsRtProCsv(filePath))
                return LoadResult<(RaceBoxData, List<RaceBoxPoint>)>.Error("Invalid File",
                    "This file is not an RT Pro telemetry log.");

            var samples = new List<(double Time, double SpeedMph, double ForwardG)>();
            try
            {
                var config = new CsvConfiguration(CultureInfo.InvariantCulture) { MissingFieldFound = null, BadDataFound = null };
                using var reader = new StreamReader(filePath);
                using var csv = new CsvReader(reader, config);
                csv.Read();
                csv.ReadHeader();
                while (csv.Read())
                {
                    if (!TryGet(csv, "recording_seconds", out double t) ||
                        !TryGet(csv, "speed_mps", out double speedMps))
                        continue;

                    // The device's x axis points backwards: launch reads as negative g.
                    double forwardG = TryGet(csv, "corrected_accel_x_g", out double ax) ? -ax : 0.0;
                    samples.Add((t, Math.Max(0, speedMps) * MpsToMph, forwardG));
                }
            }
            catch (Exception ex) when (ex is IOException or CsvHelperException)
            {
                return LoadResult<(RaceBoxData, List<RaceBoxPoint>)>.Error("RT Pro Import Failed",
                    "The RT Pro file could not be read.\n\n" + ex.Message);
            }

            if (samples.Count < 10)
                return LoadResult<(RaceBoxData, List<RaceBoxPoint>)>.Error("RT Pro Import Failed",
                    "The RT Pro file has no telemetry rows.");

            int launch = FindLaunchIndex(samples.Select(s => s.SpeedMph).ToList());
            if (launch < 0)
                return LoadResult<(RaceBoxData, List<RaceBoxPoint>)>.Error("No Pass Found",
                    $"No pass faster than {MinPassPeakMph:F0} mph was found in this RT Pro file.");

            double t0 = samples[launch].Time;
            var splitTimes = FindSplitTimes(samples, launch);

            var points = samples
                .Where(s => s.Time >= t0 - PreLaunchSeconds && s.Time <= t0 + PostLaunchSeconds)
                .Select((s, i) => new RaceBoxPoint
                {
                    Time = TimeSpan.FromSeconds(s.Time - t0),
                    SpeedMph = s.SpeedMph,
                    GForceX = s.ForwardG,
                    RunIndex = 1,
                    Record = i
                })
                .ToList();

            var header = new RaceBoxData
            {
                RunCount = 1,
                Discipline = "RT Pro",
                FirstCompleteRunIndex = 1,
                SplitTimes = splitTimes.Select(s => s.Seconds).ToList(),
                SplitLabels = splitTimes.Select(s => $"{s.Feet:0} feet").ToList()
            };

            Logger.Log($"[RtProLoader] Launch at {t0:F2}s, peak {samples.Skip(launch).Max(s => s.SpeedMph):F1} mph, " +
                       $"{points.Count} points, splits: {string.Join(", ", splitTimes.Select(s => $"{s.Feet:0}ft {s.Seconds:F3}s"))}");
            return LoadResult<(RaceBoxData, List<RaceBoxPoint>)>.Success((header, points));
        }

        /// <summary>
        /// Launch of the fastest pass: the last standstill sample before the highest speed in
        /// the file. -1 when nothing in the file reaches <see cref="MinPassPeakMph"/>.
        /// </summary>
        public static int FindLaunchIndex(IReadOnlyList<double> speedsMph)
        {
            int peak = -1;
            for (int i = 0; i < speedsMph.Count; i++)
                if (peak < 0 || speedsMph[i] > speedsMph[peak]) peak = i;

            if (peak < 0 || speedsMph[peak] < MinPassPeakMph)
                return -1;

            for (int i = peak; i >= 0; i--)
                if (speedsMph[i] <= StandstillMph)
                    return i;

            return 0; // recording started already moving
        }

        // Time after launch at which the integrated distance first reaches each split.
        private static List<(double Feet, double Seconds)> FindSplitTimes(
            List<(double Time, double SpeedMph, double ForwardG)> samples, int launch)
        {
            var result = new List<(double, double)>();
            double distance = 0;
            int next = 0;
            for (int i = launch + 1; i < samples.Count && next < SplitFeet.Length; i++)
            {
                double dt = samples[i].Time - samples[i - 1].Time;
                if (dt <= 0) continue;
                double step = samples[i].SpeedMph * MphToFeetPerSec * dt;
                while (next < SplitFeet.Length && distance + step >= SplitFeet[next])
                {
                    // Interpolate inside the sample so the split isn't rounded to 40 ms.
                    double fraction = step > 0 ? (SplitFeet[next] - distance) / step : 0;
                    double time = samples[i - 1].Time + fraction * dt - samples[launch].Time;
                    result.Add((SplitFeet[next], time));
                    next++;
                }
                distance += step;
            }
            return result;
        }

        private static bool TryGet(CsvReader csv, string column, out double value)
        {
            value = 0;
            string? text = csv.GetField(column);
            return !string.IsNullOrWhiteSpace(text) &&
                   double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }
    }
}
