// File: /src/Services/CsvLoader.cs

using CastleOverlayV2.Models;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using System.IO;
using System.Diagnostics;

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
            Debug.WriteLine("=== CsvLoader.Load() ENTERED ===");

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

            File.WriteAllText("C:\\Temp\\csvloader_test.txt", "CsvLoader reached file open");
            StreamWriter log = null;


if (_configService.IsDebugLoggingEnabled())
{
    try
    {
        string logPath = Path.Combine(Directory.GetCurrentDirectory(), "debug_log.txt");
        log = new StreamWriter(logPath, false);
        log.WriteLine("=== CSV LOAD DEBUG START ===");
    }
    catch (IOException ex)
    {
        Debug.WriteLine($"[DEBUG] Could not open log file: {ex.Message}");
        log = null;
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
                            MotorTemp = GetDouble("Temperature"),
                            MotorTiming = GetDouble("Motor Timing."),
                            Acceleration = GetDouble("Acceleration.")
                        };

                        runData.DataPoints.Add(point);
                        log?.WriteLine($"Row {rowIndex}: ADDED — Time={point.Time:F2} Speed={point.Speed}");
                        rowIndex++;
                    }

                    log?.WriteLine($"=== TOTAL POINTS LOADED: {runData.DataPoints.Count} ===");
                }
            }

            // ✅ Trim logic based on data
            if (runData.DataPoints.Count > 100 && runData.DataPoints[^1].Time - runData.DataPoints[0].Time > 3.0)
            {
                int launchIndex = DetectDragStartIndex(runData.DataPoints);
                Debug.WriteLine($"[DEBUG] LaunchIndex: {launchIndex}");

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

                    Debug.WriteLine($"[DEBUG] Trimmed Count: {runData.DataPoints.Count}");
                    Debug.WriteLine($"[DEBUG] First Point Time: {runData.DataPoints[0].Time}");
                    Debug.WriteLine($"[DEBUG] Last Point Time: {runData.DataPoints[^1].Time}");
                }
            }
            else
            {
                Debug.WriteLine("[DEBUG] Skipping AutoTrim: log too short or too brief.");
            }

            log?.WriteLine($"=== CsvLoader.Load() EXIT — Rows: {runData.DataPoints.Count} ===");
            log?.Close();

            Debug.WriteLine($"=== CsvLoader.Load() EXIT — Rows: {runData.DataPoints.Count} ===");
            return runData;
        }


        private static int DetectDragStartIndex(List<DataPoint> data)
        {
            for (int i = 1; i < data.Count; i++)
            {
                double prevThrottle = data[i - 1].Throttle;
                double currThrottle = data[i].Throttle;

                double currPower = data[i].PowerOut;
                double currRPM = data[i].Speed;
                double currAccel = data[i].Acceleration;
                double currCurrent = data[i].Current;

                // Rule 1: classic throttle spike
                if (prevThrottle <= 1.65 && currThrottle > 1.65 && currPower > 10 && currAccel > 1.0)
{
    Debug.WriteLine($"DetectDragStartIndex: Launch detected (throttle spike) at row {i}");
    return i;
}

// Rule 2: fallback — high power event
int triggerScore = 0;
if (currPower > 65.0) triggerScore++;
if (currRPM > 5000) triggerScore++;
if (currAccel > 1.0) triggerScore++;
if (currThrottle > 40.0) triggerScore++;
if (currCurrent > 5.0) triggerScore++;

if (triggerScore >= 3)
{
    Debug.WriteLine($"DetectDragStartIndex: Launch detected (fallback triggerScore={triggerScore}) at row {i}");
    return i;
}

            }

            Debug.WriteLine("DetectDragStartIndex: ❌ No drag pass detected.");
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
