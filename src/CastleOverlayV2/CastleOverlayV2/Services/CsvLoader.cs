// File: /src/Services/CsvLoader.cs

using CastleOverlayV2.Models;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using System.IO;

namespace CastleOverlayV2.Services
{
    public static class CsvLoader
    {
        public static RunData Load(string filePath)
        {
            Console.WriteLine("=== CsvLoader.Load() ENTERED ===");

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

            var logPath = Path.Combine(Directory.GetCurrentDirectory(), "debug_log.txt");
            using (var log = new StreamWriter(logPath, false))
            using (var reader = new StreamReader(filePath))
            {
                log.WriteLine("=== CSV LOAD DEBUG START ===");

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
                    }
                    else break;
                }

                log.WriteLine($"Skipped {skipCount} metadata lines.");

                using (var csv = new CsvReader(reader, config))
                {
                    csv.Read();
                    csv.ReadHeader();
                    log.WriteLine($"Header: {string.Join(", ", csv.HeaderRecord)}");

                    csv.Read(); // skip flags row
                    log.WriteLine("Skipped flags row.");

                    int rowIndex = 0;
                    int rowMax = 10000;
                    bool launchPointFound = false;

                    while (csv.Read())
                    {
                        if (rowIndex > rowMax)
                        {
                            log.WriteLine("Row limit hit!");
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
                            log.WriteLine($"Launch found at row {rowIndex}");
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
                        log.WriteLine($"Row {rowIndex}: ADDED — Time={point.Time:F2} Speed={point.Speed}");
                        rowIndex++;
                    }


                    log.WriteLine($"=== TOTAL POINTS LOADED: {runData.DataPoints.Count} ===");
                }
            }

            // ✅ Auto-trim logic only if long run
            if (runData.DataPoints.Count > 3000)
            {
                int launchIndex = DetectDragStartIndex(runData.DataPoints);
                runData.DataPoints = AutoTrim(runData.DataPoints, launchIndex);
            }

            Console.WriteLine($"=== CsvLoader.Load() EXIT — Rows: {runData.DataPoints.Count} ===");
            return runData;
        }

        private static int DetectDragStartIndex(List<DataPoint> data)
        {
            for (int i = 1; i < data.Count; i++) // start at 1 to check previous
            {
                double prev = data[i - 1].Throttle;
                double curr = data[i].Throttle;

                if (prev <= 1.60 && curr > 1.60)
                    return i;
            }

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
