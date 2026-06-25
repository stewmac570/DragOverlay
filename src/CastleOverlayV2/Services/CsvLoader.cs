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

        public LoadResult<RunData> Load(string filePath, bool trimForDrag = true)
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

            // ---- Debug log writer (optional) ---------------------------------
            StreamWriter log = null;
            if (_configService.IsDebugLoggingEnabled())
            {
                try
                {
                    string logPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "DragOverlay",
                        "debug_log.txt"
                    );
                    log = new StreamWriter(logPath, false);
                    log.WriteLine($"[CsvLoader] Load started at {DateTime.Now}");
                    log.WriteLine($"File: {filePath}");
                }
                catch (IOException)
                {
                    // ignore logging failures
                }
            }
            // ------------------------------------------------------------------

            // Castle radio endpoints (ms). Defaults if not found in header.
            var cal = new ThrottleCal
            {
                MinMs = 1.048,   // Full Reverse
                NeutralMs = 1.500,
                MaxMs = 1.910    // Full Forward
            };
            double sampleIntervalSeconds = 0.05;

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

                        if (meta.Contains("Full Reverse:", StringComparison.OrdinalIgnoreCase))
                        {
                            if (double.TryParse(
                                meta.Split(':').Last().Replace("ms", "", StringComparison.OrdinalIgnoreCase).Trim(),
                                NumberStyles.Any, CultureInfo.InvariantCulture, out double v)) cal.MinMs = v;
                        }
                        else if (meta.Contains("Neutral:", StringComparison.OrdinalIgnoreCase))
                        {
                            if (double.TryParse(
                                meta.Split(':').Last().Replace("ms", "", StringComparison.OrdinalIgnoreCase).Trim(),
                                NumberStyles.Any, CultureInfo.InvariantCulture, out double v)) cal.NeutralMs = v;
                        }
                        else if (meta.Contains("Full Forward:", StringComparison.OrdinalIgnoreCase))
                        {
                            if (double.TryParse(
                                meta.Split(':').Last().Replace("ms", "", StringComparison.OrdinalIgnoreCase).Trim(),
                                NumberStyles.Any, CultureInfo.InvariantCulture, out double v)) cal.MaxMs = v;
                        }
                        else if (meta.Contains("Sample Time:", StringComparison.OrdinalIgnoreCase))
                        {
                            string sampleTimeText = meta.Split("Sample Time:", StringSplitOptions.RemoveEmptyEntries).Last().Trim();
                            if (double.TryParse(sampleTimeText, NumberStyles.Any, CultureInfo.InvariantCulture, out double v) && v > 0)
                                sampleIntervalSeconds = v;
                        }
                    }
                    else
                    {
                        break;
                    }
                }

                log?.WriteLine($"Skipped {skipCount} metadata lines.");
                log?.WriteLine($"ThrottleCal(ms): min={cal.MinMs:F3} neu={cal.NeutralMs:F3} max={cal.MaxMs:F3}");

                using (var csv = new CsvReader(reader, config))
                {
                    // Header + flags row
                    csv.Read();
                    csv.ReadHeader();
                    Logger.Log("[DEBUG] Available CSV Headers: " + string.Join(", ", csv.HeaderRecord));
                    log?.WriteLine($"Header: {string.Join(", ", csv.HeaderRecord)}");

                    // CL2: Required column guard — Power-Out must be present for launch detection
                    if (!csv.HeaderRecord.Contains("Power-Out", StringComparer.OrdinalIgnoreCase))
                    {
                        Logger.Log("[CsvLoader] Required column 'Power-Out' not found — aborting load.");
                        log?.WriteLine("[CsvLoader] Required column 'Power-Out' not found.");
                        log?.Close();
                        return LoadResult<RunData>.Error(
                            "Import Failed",
                            "This file is missing a required column: 'Power-Out'.\n\nIt may not be a valid Castle ESC log.");
                    }

                    csv.Read(); // skip flags row
                    log?.WriteLine("Skipped flags row.");

                    int rowIndex = 0;
                    const int rowMax = 10000;

                    while (csv.Read())
                    {
                        string rawRecord = csv.Parser.RawRecord ?? string.Empty;
                        if (rawRecord.TrimStart().StartsWith("#", StringComparison.Ordinal))
                        {
                            log?.WriteLine("Next Castle session encountered; stopping at first session.");
                            break;
                        }

                        if (rowIndex > rowMax)
                        {
                            log?.WriteLine("Row limit hit!");
                            break;
                        }

                        string rpmField = csv.HeaderRecord.Contains("RPM") ? "RPM" : "Speed";

                        CastleCell GetCell(string col)
                        {
                            if (!csv.HeaderRecord.Contains(col, StringComparer.OrdinalIgnoreCase))
                                return new CastleCell(0.0, false);

                            string raw = csv.GetField<string>(col);
                            if (string.IsNullOrWhiteSpace(raw))
                                return new CastleCell(0.0, false);

                            bool hasBadMarker = raw.Contains('b', StringComparison.OrdinalIgnoreCase);
                            raw = raw.Replace("b", "", StringComparison.OrdinalIgnoreCase).Replace("%", "").Trim();
                            bool parsed = double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out double result);
                            return new CastleCell(parsed ? result : 0.0, parsed && !hasBadMarker);
                        }

                        CastleCell throttleCell = GetCell("Throttle");
                        CastleCell powerOutCell = GetCell("Power-Out");
                        CastleCell currentCell = GetCell("Current");
                        CastleCell speedCell = GetCell(rpmField);

                        double throttleMs = throttleCell.Value;
                        double throttlePct = ThrottlePercent.FromMilliseconds(
                            throttleMs, cal.MinMs, cal.NeutralMs, cal.MaxMs);

                        var point = new DataPoint
                        {
                            Time = rowIndex * sampleIntervalSeconds,
                            Throttle = throttleMs,
                            ThrottlePercent = throttlePct,
                            PowerOut = powerOutCell.Value,
                            PowerOutValid = powerOutCell.Valid,
                            Voltage = GetCell("Voltage").Value,
                            Ripple = GetCell("Ripple").Value,
                            Current = currentCell.Value,
                            CurrentValid = currentCell.Valid,
                            Speed = speedCell.Value,
                            SpeedValid = speedCell.Valid,
                            Temperature = GetCell("Temperature").Value,
                            MotorTemp = GetCell("Motor Temp.").Value,
                            MotorTiming = GetCell("Motor Timing.").Value,
                            Acceleration = GetCell("Acceleration.").Value
                        };

                        log?.WriteLine($"Row {rowIndex}: Throttle(ms)={throttleMs:F4}  Throttle(%)={throttlePct:F1}");
                        runData.DataPoints.Add(point);
                        if (Logger.IsEnabled)
                            Logger.Log($"Row {rowIndex}: ADDED — Time={point.Time:F2} Acceleration={point.Acceleration}");

                        rowIndex++;
                    }

                    log?.WriteLine($"=== TOTAL POINTS LOADED: {runData.DataPoints.Count} ===");
                }
            }

            bool noDragPass = false;

            if (trimForDrag)
            {
                if (runData.DataPoints.Count > 100 &&
                    runData.DataPoints[^1].Time - runData.DataPoints[0].Time > 3.0)
                {
                    DragPassDetectionResult detection = DragPassDetector.Detect(runData.DataPoints, sampleIntervalSeconds);
                    Logger.Log($"CsvLoader: LaunchIndex = {detection.LaunchIndex}; Score = {detection.Score:F2}");

                    if (!detection.Found)
                    {
                        noDragPass = true;
                    }
                    else
                    {
                        runData.DataPoints = AutoTrim(runData.DataPoints, detection);
                        Logger.Log($"CsvLoader: Trimmed count = {runData.DataPoints.Count}");
                        Logger.Log($"CsvLoader: First Point Time = {runData.DataPoints[0].Time:F2}");
                        Logger.Log($"CsvLoader: Last Point Time = {runData.DataPoints[^1].Time:F2}");
                    }
                }
                else
                {
                    Logger.Log("CsvLoader: Skipping AutoTrim — log too short or too brief.");
                }
            }
            else
            {
                // Always re-zero to first sample
                if (runData.DataPoints.Count > 0)
                {
                    double t0 = runData.DataPoints[0].Time;
                    foreach (var p in runData.DataPoints)
                        p.Time -= t0;
                    Logger.Log("CsvLoader: Re-zeroed full log to first sample.");
                }
            }

            if (runData.DataPoints.Count > 0)
            {
                var accelVals = runData.DataPoints.Select(dp => dp.Acceleration).Take(10).ToArray();
                Logger.Log($"🧪 Sample Acceleration values: {string.Join(", ", accelVals)}");
            }

            // Snapshot the loaded samples as the reversible manual-trim baseline. Whatever
            // auto-trim produced (windowed) or the full re-zeroed log (auto-trim skipped)
            // becomes the set that right-click trim and "Reset trim" operate over.
            runData.CaptureTrimBaseline();

            log?.WriteLine($"=== CsvLoader.Load() EXIT — Rows: {runData.DataPoints.Count} ===");
            log?.Close();

            Logger.Log($"CsvLoader.Load() exit — Rows: {runData.DataPoints.Count}");

            return noDragPass
                ? LoadResult<RunData>.SuccessWithWarning(runData, "DragOverlay",
                    "No drag pass detected in this log.\nAuto-trim was skipped.\n\n" +
                    "Arm the run and right-click the plot to trim it manually.")
                : LoadResult<RunData>.Success(runData);
        }

        private sealed class ThrottleCal
        {
            public double MinMs;
            public double NeutralMs;
            public double MaxMs;
        }

        private sealed record CastleCell(double Value, bool Valid);

        private static List<DataPoint> AutoTrim(List<DataPoint> data, DragPassDetectionResult detection)
        {
            if (!detection.Found || detection.LaunchIndex < 0 || detection.LaunchIndex >= data.Count)
                return data;

            double t0 = data[detection.LaunchIndex].Time;
            var trimmed = data
                .Skip(detection.TrimStartIndex)
                .Take(detection.TrimEndIndex - detection.TrimStartIndex + 1)
                .ToList();
            foreach (var p in trimmed)
                p.Time -= t0;

            return trimmed;
        }
    }
}
