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
                catch (IOException ex)
                {
                    // Fallback: don't crash if log can't be written
                }
            }

            using (var reader = new StreamReader(filePath))
            {
                // ✅ Skip metadata lines starting with '#'
                int skipMax = 1000;
                int skipCount = 0;

                for (int i = 0; i < skipMax; i++)
                {
                    int peek = reader.Peek();
                    if (peek == -1) break;

                    char c = (char)peek;
                    if (c == '#')
                    {
                        reader.ReadLine();
                        skipCount++;

                        log?.WriteLine($"[DEBUG] Skipped metadata line {skipCount}");
                    }
                    else
                    {
                        break;
                    }
                }

                log?.WriteLine($"Skipped {skipCount} metadata lines.");

                using (var csv = new CsvReader(reader, config))
                {
                    csv.Read();
                    csv.ReadHeader();
                    Logger.Log("[DEBUG] Available CSV Headers: " + string.Join(", ", csv.HeaderRecord)); // ✅ Add this
                    log?.WriteLine($"Header: {string.Join(", ", csv.HeaderRecord)}");

                    csv.Read(); // skip flags row
                    log?.WriteLine("Skipped flags row.");


                    int rowIndex = 0;
                    int rowMax = 10000;
                    bool launchPointFound = false;

                    while (csv.Read())
                    {
                        if (rowIndex > rowMax)
                        {
                            log?.WriteLine("Row limit hit!");
                            break;
                        }

                        // ✅ Clean Power-Out safely
                        string rawPowerOut = csv.GetField<string>("Power-Out");
                        double powerOut = 0.0;
                        if (!string.IsNullOrWhiteSpace(rawPowerOut))
                        {
                            rawPowerOut = rawPowerOut.Replace("b", "").Replace("%", "").Trim();
                            double.TryParse(rawPowerOut, out powerOut);
                        }

                        if (!launchPointFound && powerOut >= 5.0)
                        {
                            launchPointFound = true;
                            log?.WriteLine($"Launch found at row {rowIndex}");
                        }

                        if (!launchPointFound)
                        {
                            rowIndex++;
                            continue;
                        }

                        // ✅ Fallback for RPM
                        string rpmField = csv.HeaderRecord.Contains("RPM") ? "RPM" : "Speed";

                        // ⬇️ Normalize weird CSV header names to clean field names

                        // ✅ Parse all other fields safely
                        double GetDouble(string col)
                        {

                            string raw = csv.GetField<string>(col);
                            if (string.IsNullOrWhiteSpace(raw)) return 0.0;
                            raw = raw.Replace("b", "").Replace("%", "").Trim();
                            return double.TryParse(raw, out double result) ? result : 0.0;
                        }

                        var point = new DataPoint
                        {
                            Time = rowIndex * 0.05,
                            Throttle = GetDouble("Throttle"),
                            PowerOut = powerOut,
                            Voltage = GetDouble("Voltage"),
                            Ripple = GetDouble("Ripple"),
                            Current = GetDouble("Current"),
                            Speed = GetDouble(rpmField),
                            Temperature = GetDouble("Temperature"),
                            MotorTemp = GetDouble("Motor Temp."),
                            MotorTiming = GetDouble("Motor Timing."),
                            Acceleration = GetDouble("Acceleration."),

                        };

                        runData.DataPoints.Add(point);
                        log?.WriteLine($"Row {rowIndex}: ADDED — Time={point.Time:F2} Speed={point.Speed}");
                        log?.WriteLine($"Row {rowIndex}: ADDED — Time={point.Time:F2} MotorTemp={point.MotorTemp}");
                        Logger.Log($"Row {rowIndex}: ADDED — Time={point.Time:F2} Acceleration={point.Acceleration}");


                        rowIndex++;
                    }

                    log?.WriteLine($"=== TOTAL POINTS LOADED: {runData.DataPoints.Count} ===");
                }
            }

            // ✅ Trim logic based on data
            if (runData.DataPoints.Count > 100 && runData.DataPoints[^1].Time - runData.DataPoints[0].Time > 3.0)
            {
                int launchIndex = DetectDragStartIndex(runData.DataPoints);
                Logger.Log($"CsvLoader: LaunchIndex = {launchIndex}");


                if (launchIndex == -1)
                {
                    MessageBox.Show(
                        "No drag pass detected in this log.\nAuto-trim was skipped.",
                        "DragOverlay",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );
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

            // ✅ Debug — log acceleration values before trimming
            if (runData.DataPoints.Count > 0)
            {
                var accelBeforeTrim = runData.DataPoints.Select(dp => dp.Acceleration).Take(20).ToArray();
                Logger.Log($"🧪 Raw Acceleration (pre-trim): {string.Join(", ", accelBeforeTrim)}");
            }


            // ✅ Log trimmed acceleration values (after AutoTrim or raw if skipped)
            if (runData.DataPoints.Count > 0)
            {
                var accelVals = runData.DataPoints.Select(dp => dp.Acceleration).Take(10).ToArray();
                Logger.Log($"🧪 Trimmed Acceleration values: {string.Join(", ", accelVals)}");
            }

            log?.WriteLine($"=== CsvLoader.Load() EXIT — Rows: {runData.DataPoints.Count} ===");
            log?.Close();

            Logger.Log($"CsvLoader.Load() exit — Rows: {runData.DataPoints.Count}");

            return runData;

        }

        private static int DetectDragStartIndex(List<DataPoint> data)
        {
            int sustainWindow = 5; // require ~5 samples in a row (≈250 ms if 50 ms/sample)

            for (int i = 1; i < data.Count - sustainWindow; i++)
            {
                double prevThrottle = data[i - 1].Throttle;
                double currThrottle = data[i].Throttle;

                double currPower = data[i].PowerOut;
                double currAccel = data[i].Acceleration;
                double currCurrent = data[i].Current;

                // --- Rule 1: throttle spike must sustain ---
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

                // --- Rule 2: fallback multi-signal score, also sustained ---
                int triggerScore = 0;
                if (currPower > 65.0) triggerScore++;
                if (data[i].Speed > 5000) triggerScore++;
                if (currAccel > 1.0) triggerScore++;
                if (currThrottle > 1.7) triggerScore++; // throttle in ms, not %
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

            // ✅ Shift all points so drag launch (t0) becomes 0.0
            foreach (var p in trimmed)
                p.Time -= t0;

            return trimmed;
        }

    }
}
