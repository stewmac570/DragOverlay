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

                // ✅ 1️⃣ Skip Castle metadata lines starting with #
                // ✅ 1️⃣ Skip Castle metadata lines starting with #
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
                log.Flush();


                log.WriteLine($"Skipped {skipCount} metadata lines.");

                using (var csv = new CsvReader(reader, config))
                {


                    csv.Read();
                    csv.ReadHeader();
                    log.WriteLine($"Header: {string.Join(", ", csv.HeaderRecord)}");

                    csv.Read(); // skip flags row
                    log.WriteLine("Skipped flags row.");

                    int rowIndex = 0;
                    int rowMax = 1000; // debug guard
                    bool launchPointFound = false;



                    while (csv.Read())
                    {
                        if (rowIndex > rowMax)
                        {
                            log.WriteLine("Row limit hit!");
                            break;  // ✅ Now it knows what to break: the while loop
                        }
                        if (rowIndex > rowMax)
                        {
                            log.WriteLine("=== Row limit hit! Stopping to prevent hang ===");
                            break;
                        }

                        double powerOut = csv.GetField<double>("Power-Out");
                        log.WriteLine($"Row {rowIndex}: Power-Out={powerOut}");

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

                        // ✅ Fallback for legacy logs where RPM might be called "Speed"
                        string rpmField = csv.HeaderRecord.Contains("RPM") ? "RPM" : "Speed";

                        var point = new DataPoint
                        {
                            Time = rowIndex * 0.05,
                            Throttle = csv.GetField<double>("Throttle"),
                            PowerOut = powerOut,
                            Voltage = csv.GetField<double>("Voltage"),
                            Ripple = csv.GetField<double>("Ripple"),
                            Current = csv.GetField<double>("Current"),
                            Speed = csv.GetField<double>(rpmField),  // ✅ use fallback-safe field
                            Temperature = csv.GetField<double>("Temperature"),
                            MotorTemp = csv.GetField<double>("Temperature"),
                            MotorTiming = csv.GetField<double>("Motor Timing."),
                            Acceleration = csv.GetField<double>("Acceleration."),
                        };


                        runData.DataPoints.Add(point);
                        log.WriteLine($"Row {rowIndex}: ADDED — Time={point.Time:F2} Speed={point.Speed}");
                        rowIndex++;
                    }

                    log.WriteLine($"=== TOTAL POINTS LOADED: {runData.DataPoints.Count} ===");
                }
            }

            Console.WriteLine($"=== CsvLoader.Load() EXIT — Rows: {runData.DataPoints.Count} ===");
            return runData;
        }
    }
}
