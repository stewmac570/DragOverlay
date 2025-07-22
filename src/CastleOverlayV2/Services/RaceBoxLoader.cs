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
    public class RaceBoxLoader
    {
        /// <summary>
        /// Parses the telemetry section from a RaceBox CSV file and extracts all points for the selected run.
        /// </summary>
        public List<RaceBoxPoint> LoadTelemetry(string filePath, int selectedRunIndex)
        {
            var points = new List<RaceBoxPoint>();

            using (var reader = new StreamReader(filePath))
            using (var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = false,
                MissingFieldFound = null,
                BadDataFound = null,
                Delimiter = ","
            }))
            {
                var allRows = new List<string[]>();

                while (csv.Read())
                {
                    var row = new List<string>();
                    for (int i = 0; csv.TryGetField(i, out string value); i++)
                        row.Add(value);
                    allRows.Add(row.ToArray());
                }

                // === Header-based Metadata Logging ===
                int runCount = int.Parse(allRows[7][1]);  // Row 8: Runs
                Logger.Log($"RaceBox file: Detected {runCount} runs");

                string disciplineLine = allRows[8][1];    // Row 9: Disciplines
                Logger.Log($"RaceBox file: Disciplines = {disciplineLine}");

                // === Telemetry Start Row ===
                int telemetryHeaderRow = 8 + runCount + 1;
                string[] headers = allRows[telemetryHeaderRow];

                int timeIndex = Array.IndexOf(headers, "Time");
                int speedIndex = Array.IndexOf(headers, "Speed (m/s)");
                int gForceXIndex = Array.IndexOf(headers, "GForceX");
                int runColIndex = Array.IndexOf(headers, "Run");

                if (timeIndex == -1 || speedIndex == -1 || gForceXIndex == -1 || runColIndex == -1)
                    throw new Exception("Required telemetry columns missing.");

                // === Filter by Run Index ===
                var telemetryRows = allRows.Skip(telemetryHeaderRow + 1)
                                           .Where(r => r.Length > runColIndex &&
                                                       int.TryParse(r[runColIndex], out int run) &&
                                                       run == selectedRunIndex)
                                           .ToList();

                if (telemetryRows.Count == 0)
                    throw new Exception("No telemetry rows found for selected run.");

                Logger.Log($"Selected Run {selectedRunIndex + 1} — matching all disciplines");
                Logger.Log($"Telemetry rows extracted: {telemetryRows.Count}");

                // === Parse into RaceBoxPoint ===
                DateTime baseTime = DateTime.Parse(telemetryRows[0][timeIndex]);

                foreach (var row in telemetryRows)
                {
                    DateTime currentTime = DateTime.Parse(row[timeIndex]);
                    TimeSpan offset = currentTime - baseTime;

                    double speedMps = double.TryParse(row[speedIndex], out var s) ? s : 0.0;
                    double gForceX = double.TryParse(row[gForceXIndex], out var g) ? g : 0.0;
                    int runIndex = int.TryParse(row[runColIndex], out var r) ? r : -1;

                    var point = new RaceBoxPoint
                    {
                        Time = offset,
                        SpeedMph = speedMps * 2.23694, // Convert m/s → mph
                        GForceX = gForceX,
                        RunIndex = runIndex
                    };

                    Logger.Log($"RB Point: t={offset.TotalSeconds:F3}s, Speed={point.SpeedMph:F1}mph, Gx={point.GForceX:F2}");

                    points.Add(point);
                }
            }

            return points;
        }
    }
}
