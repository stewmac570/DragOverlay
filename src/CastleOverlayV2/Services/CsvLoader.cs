using CastleOverlayV2.Models;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;

namespace CastleOverlayV2.Services
{
    public class CsvLoader
    {
        private readonly ConfigService _configService;

        public CsvLoader(ConfigService configService)
        {
            _configService = configService;
        }

        public RunData Load(string filePath)
        {
            Logger.Log("CsvLoader.Load() entered.");

            var runData = new RunData
            {
                FileName = Path.GetFileName(filePath)
            };

            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                MissingFieldFound = null,
                TrimOptions = TrimOptions.Trim,
                Delimiter = ","
            };

            // ---- Debug log (optional) -----------------------------------------
            StreamWriter? log = null;
            if (_configService.IsDebugLoggingEnabled())
            {
                try
                {
                    string logPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "DragOverlay",
                        "debug_log.txt");
                    log = new StreamWriter(logPath, false);
                    log.WriteLine($"[CsvLoader] Load started at {DateTime.Now}");
                    log.WriteLine($"File: {filePath}");
                }
                catch { /* ignore logging failures */ }
            }
            // -------------------------------------------------------------------

            // Castle radio endpoints (ms). Defaults if not found in header.
            var cal = new ThrottleCal
            {
                MinMs = 1.048, // Full Reverse
                NeutralMs = 1.500, // Neutral
                MaxMs = 1.910  // Full Forward
            };

            using (var reader = new StreamReader(filePath))
            {
                // --- Skip '#' metadata lines and capture optional ms calibration
                int skipCount = 0;
                for (int i = 0; i < 1000; i++)
                {
                    int peek = reader.Peek();
                    if (peek == -1) break;

                    if ((char)peek == '#')
                    {
                        string meta = reader.ReadLine() ?? string.Empty;
                        skipCount++;
                        log?.WriteLine($"[DEBUG] Meta: {meta}");

                        // Optional header lines:
                        //   # Full Reverse: 1.048 ms
                        //   # Neutral: 1.500 ms
                        //   # Full Forward: 1.910 ms
                        if (meta.Contains("Full Reverse:", StringComparison.OrdinalIgnoreCase))
                        {
                            if (double.TryParse(meta.Split(':').Last().Replace("ms", "", StringComparison.OrdinalIgnoreCase).Trim(), out double v))
                                cal.MinMs = v;
                        }
                        else if (meta.Contains("Neutral:", StringComparison.OrdinalIgnoreCase))
                        {
                            if (double.TryParse(meta.Split(':').Last().Replace("ms", "", StringComparison.OrdinalIgnoreCase).Trim(), out double v))
                                cal.NeutralMs = v;
                        }
                        else if (meta.Contains("Full Forward:", StringComparison.OrdinalIgnoreCase))
                        {
                            if (double.TryParse(meta.Split(':').Last().Replace("ms", "", StringComparison.OrdinalIgnoreCase).Trim(), out double v))
                                cal.MaxMs = v;
                        }
                    }
                    else
                    {
                        break;
                    }
                }

                log?.WriteLine($"Skipped {skipCount} metadata lines.");
                log?.WriteLine($"ThrottleCal (initial ms): min={cal.MinMs:F3} neu={cal.NeutralMs:F3} max={cal.MaxMs:F3}");

                using (var csv = new CsvReader(reader, config))
                {
                    // Header + flags row
                    csv.Read();
                    csv.ReadHeader();
                    Logger.Log("[DEBUG] Available CSV Headers: " + string.Join(", ", csv.HeaderRecord));
                    log?.WriteLine($"Header: {string.Join(", ", csv.HeaderRecord)}");

                    csv.Read(); // skip flags row
                    log?.WriteLine("Skipped flags row.");

                    int rowIndex = 0;
                    const int rowMax = 10000;
                    bool launchPointFound = false;

                    // collect pre-launch throttle to auto-center neutral
                    var preLaunchThrottleMs = new List<double>(256);

                    // Helper: safe column reader
                    double GetDouble(string col)
                    {
                        string raw = csv.GetField<string>(col);
                        if (string.IsNullOrWhiteSpace(raw)) return 0.0;
                        raw = raw.Replace("b", "").Replace("%", "").Trim();
                        return double.TryParse(raw, out double result) ? result : 0.0;
                    }

                    while (csv.Read())
                    {
                        if (rowIndex > rowMax)
                        {
                            log?.WriteLine("Row limit hit!");
                            break;
                        }

                        // Power-Out (cleaned)
                        string rawPowerOut = csv.GetField<string>("Power-Out");
                        double powerOut = 0.0;
                        if (!string.IsNullOrWhiteSpace(rawPowerOut))
                        {
                            rawPowerOut = rawPowerOut.Replace("b", "").Replace("%", "").Trim();
                            double.TryParse(rawPowerOut, out powerOut);
                        }

                        // Throttle in ms for this row (needed for both % and launch heuristics)
                        double throttleMs = GetDouble("Throttle");

                        // collect pre-launch idle baseline
                        if (!launchPointFound && throttleMs > 0)
                            preLaunchThrottleMs.Add(throttleMs);

                        // Detect launch (same rule as before)
                        if (!launchPointFound && powerOut >= 5.0)
                        {
                            launchPointFound = true;

                            // Auto-center neutral to the actual idle baseline (median)
                            if (preLaunchThrottleMs.Count >= 5)
                            {
                                double baseline = preLaunchThrottleMs.OrderBy(v => v)
                                                                    .ElementAt(preLaunchThrottleMs.Count / 2);
                                // clamp inside [Min, Max] to be safe
                                cal.NeutralMs = Math.Max(Math.Min(baseline, Math.Max(cal.MinMs, cal.MaxMs)),
                                                         Math.Min(cal.MinMs, cal.MaxMs));
                                log?.WriteLine($"[ThrottleCal] Neutral auto-centered to idle baseline: {cal.NeutralMs:F4} ms");
                            }

                            log?.WriteLine($"Launch found at row {rowIndex}");
                        }

                        // skip rows until launch is found (preserve previous behavior)
                        if (!launchPointFound)
                        {
                            rowIndex++;
                            continue;
                        }

                        // RPM fallback
                        string rpmField = csv.HeaderRecord.Contains("RPM") ? "RPM" : "Speed";

                        // % from ms (with deadband)
                        double throttlePct = MsToPercent(throttleMs, cal);

                        var point = new DataPoint
                        {
                            Time = rowIndex * 0.05,
                            Throttle = throttleMs,            // keep ms for existing logic
                            ThrottlePercent = throttlePct,    // new derived value [-100..+100]
                            PowerOut = powerOut,
                            Voltage = GetDouble("Voltage"),
                            Ripple = GetDouble("Ripple"),
                            Current = GetDouble("Current"),
                            Speed = GetDouble(rpmField),
                            Temperature = GetDouble("Temperature"),
                            MotorTemp = GetDouble("Motor Temp."),
                            MotorTiming = GetDouble("Motor Timing."),
                            Acceleration = GetDouble("Acceleration.")
                        };

                        log?.WriteLine($"Row {rowIndex}: Throttle(ms)={throttleMs:F4}  Throttle(%)={throttlePct:F1}");

                        runData.DataPoints.Add(point);
                        rowIndex++;
                    }

                    log?.WriteLine($"=== TOTAL POINTS LOADED: {runData.DataPoints.Count} ===");
                }
            }

            // --- AutoTrim around launch (unchanged) ---------------------------
            if (runData.DataPoints.Count > 100 &&
                runData.DataPoints[^1].Time - runData.DataPoints[0].Time > 3.0)
            {
                int launchIndex = DetectDragStartIndex(runData.DataPoints);
                Logger.Log($"CsvLoader: LaunchIndex = {launchIndex}");

                if (launchIndex == -1)
                {
                    MessageBox.Show(
                        "No drag pass detected in this log.\nAuto-trim was skipped.",
                        "DragOverlay",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
                else
                {
                    runData.DataPoints = AutoTrim(runData.DataPoints, launchIndex);
                    Logger.Log($"CsvLoader: Trimmed count = {runData.DataPoints.Count}");
                    Logger.Log($"CsvLoader: First Point Time = {runData.DataPoints[0].Time:F2}");
                    Logger.Log($"CsvLoader: Last Point Time = {runData.DataPoints[^1].Time:F2}");
                }
            }
            else
            {
                Logger.Log("CsvLoader: Skipping AutoTrim — log too short or too brief.");
            }
            // -------------------------------------------------------------------

            // Debug previews
            if (runData.DataPoints.Count > 0)
            {
                var accelBeforeTrim = runData.DataPoints.Select(dp => dp.Acceleration).Take(20).ToArray();
                Logger.Log($"🧪 Raw Acceleration (pre-trim): {string.Join(", ", accelBeforeTrim)}");

                var accelVals = runData.DataPoints.Select(dp => dp.Acceleration).Take(10).ToArray();
                Logger.Log($"🧪 Trimmed Acceleration values: {string.Join(", ", accelVals)}");
            }

            log?.WriteLine($"=== CsvLoader.Load() EXIT — Rows: {runData.DataPoints.Count} ===");
            log?.Close();

            Logger.Log($"CsvLoader.Load() exit — Rows: {runData.DataPoints.Count}");
            return runData;
        }

        // ---------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------

        private sealed class ThrottleCal
        {
            public double MinMs;     // full reverse
            public double NeutralMs; // neutral
            public double MaxMs;     // full forward
        }

        /// <summary>
        /// ms -> % using piecewise linear mapping around Neutral with a small deadband.
        /// </summary>
        private static double MsToPercent(double ms, ThrottleCal cal)
        {
            double min = Math.Min(cal.MinMs, cal.MaxMs);
            double max = Math.Max(cal.MinMs, cal.MaxMs);
            double neu = cal.NeutralMs;

            if (!(min < neu && neu < max))
                return 0.0;

            double pct;
            if (ms >= neu)
            {
                double span = Math.Max(1e-9, max - neu);
                pct = (ms - neu) / span * 100.0;
            }
            else
            {
                double span = Math.Max(1e-9, neu - min);
                pct = -(neu - ms) / span * 100.0;
            }

            // snap tiny jitter to zero so idle shows 0%
            const double deadbandPct = 1.5;
            if (Math.Abs(pct) < deadbandPct)
                pct = 0.0;

            // clamp hard limits
            return Math.Max(-100.0, Math.Min(100.0, pct));
        }

        private static int DetectDragStartIndex(List<DataPoint> data)
        {
            int sustainWindow = 5; // ≈250 ms at 50 ms/sample

            for (int i = 1; i < data.Count - sustainWindow; i++)
            {
                double prevThrottle = data[i - 1].Throttle; // ms
                double currThrottle = data[i].Throttle;      // ms

                double currPower = data[i].PowerOut;
                double currAccel = data[i].Acceleration;
                double currCurrent = data[i].Current;

                // Rule 1: throttle spike must sustain (thresholds in ms)
                if (prevThrottle <= 1.65 && currThrottle > 1.65)
                {
                    bool sustained = true;
                    for (int k = 0; k < sustainWindow; k++)
                    {
                        if (data[i + k].PowerOut < 20.0 || data[i + k].Acceleration < 0.5)
                        {
                            sustained = false;
                            break;
                        }
                    }
                    if (sustained)
                    {
                        Logger.Log($"DetectDragStartIndex: Sustained launch detected at row {i}");
                        return i;
                    }
                }

                // Rule 2: fallback multi-signal score, also sustained
                int triggerScore = 0;
                if (currPower > 65.0) triggerScore++;
                if (data[i].Speed > 5000) triggerScore++;
                if (currAccel > 1.0) triggerScore++;
                if (currThrottle > 1.7) triggerScore++; // ms, not %
                if (currCurrent > 5.0) triggerScore++;

                if (triggerScore >= 3)
                {
                    bool sustained = true;
                    for (int k = 0; k < sustainWindow; k++)
                    {
                        if (data[i + k].PowerOut < 20.0)
                        {
                            sustained = false;
                            break;
                        }
                    }
                    if (sustained)
                    {
                        Logger.Log($"DetectDragStartIndex: Sustained fallback launch detected at row {i}");
                        return i;
                    }
                }
            }

            Logger.Log("DetectDragStartIndex: No drag pass detected.");
            return -1;
        }

        private static List<DataPoint> AutoTrim(List<DataPoint> data, int index)
        {
            if (index == -1 || index >= data.Count)
                return data;

            double t0 = data[index].Time;
            double tMin = t0 - 0.5;
            double tMax = t0 + 2.5;

            var trimmed = data.Where(p => p.Time >= tMin && p.Time <= tMax).ToList();

            // Shift so launch becomes t=0
            foreach (var p in trimmed)
                p.Time -= t0;

            return trimmed;
        }
    }
}
