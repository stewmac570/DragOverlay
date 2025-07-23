using CastleOverlayV2.Models;
using CsvHelper;
using CsvHelper.Configuration;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

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

            Logger.Log("[RaceBoxLoader] LoadTelemetry started...");

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

                Logger.Log("[RaceBoxLoader] Starting CSV read loop...");
                while (csv.Read())
                {
                    int fieldCount = csv.Parser.Count;
                    var fields = new List<string>();
                    for (int i = 0; i < fieldCount; i++)
                        fields.Add(csv.GetField(i));

                    allRows.Add(fields.ToArray());
                }
                Logger.Log($"[RaceBoxLoader] CSV read complete — total rows: {allRows.Count}");

                // === Header-based Metadata Logging ===
                Logger.Log("[RaceBoxLoader] Reading run count from row 8...");
                int runCount = int.Parse(allRows[7][1]);  // Row 8: Runs
                Logger.Log($"RaceBox file: Detected {runCount} runs");

                Logger.Log("[RaceBoxLoader] Reading discipline string from row 9...");
                string disciplineLine = allRows[8][1];    // Row 9: Disciplines
                Logger.Log($"RaceBox file: Disciplines = {disciplineLine}");

                // === Telemetry Start Row ===
                int telemetryHeaderRow = 8 + runCount + 1;
                Logger.Log($"[RaceBoxLoader] Telemetry header row index = {telemetryHeaderRow}");

                if (telemetryHeaderRow >= allRows.Count)
                {
                    Logger.Log("[RaceBoxLoader] Telemetry header row is out of bounds!");
                    MessageBox.Show("Telemetry section missing or incomplete.", "RaceBox Import Failed");
                    return points;
                }

                string[] headers = allRows[telemetryHeaderRow];

                Logger.Log("[RaceBoxLoader] Columns detected:");
                foreach (var h in headers)
                    Logger.Log($"  Header: '{h}'");

                Logger.Log($"[RaceBoxLoader] Header row columns: {headers.Length}");

                int timeIndex = GetHeaderIndex(headers, "Time");
                int speedIndex = GetHeaderIndex(headers, "Speed (m/s)");
                int gForceXIndex = GetHeaderIndex(headers, "GForceX (g)");
                int runColIndex = GetHeaderIndex(headers, "Run");

                if (timeIndex == -1 || speedIndex == -1 || gForceXIndex == -1 || runColIndex == -1)
                {
                    Logger.Log("[RaceBoxLoader] Required telemetry columns missing.");
                    throw new Exception("Required telemetry columns missing.");
                }

                Logger.Log("[RaceBoxLoader] All required columns found. Filtering telemetry rows...");

                // === Filter by Run Index ===
                var telemetryRows = allRows.Skip(telemetryHeaderRow + 1)
                                           .Where(r => r.Length > runColIndex &&
                                                       int.TryParse(r[runColIndex], out int run) &&
                                                       run == selectedRunIndex)
                                           .ToList();

                Logger.Log($"[RaceBoxLoader] Telemetry rows matched to run {selectedRunIndex}: {telemetryRows.Count}");

                if (telemetryRows.Count == 0)
                {
                    Logger.Log("[RaceBoxLoader] No telemetry rows found for selected run.");
                    throw new Exception("No telemetry rows found for selected run.");
                }

                Logger.Log("[RaceBoxLoader] Parsing telemetry timestamps...");
                DateTime baseTime = DateTime.Parse(telemetryRows[0][timeIndex]);

                for (int i = 0; i < telemetryRows.Count; i++)
                {
                    var row = telemetryRows[i];

                    DateTime currentTime = DateTime.Parse(row[timeIndex]);
                    TimeSpan offset = currentTime - baseTime;

                    double speedMps = double.TryParse(row[speedIndex], out var s) ? s : 0.0;
                    double gForceX = double.TryParse(row[gForceXIndex], out var g) ? g : 0.0;
                    int runIndex = int.TryParse(row[runColIndex], out var r) ? r : -1;
                    int record = int.TryParse(row[0], out var rec) ? rec : -1;

                    var point = new RaceBoxPoint
                    {
                        Record = record,
                        Time = offset,
                        SpeedMph = speedMps * 2.23694,
                        GForceX = gForceX,
                        RunIndex = runIndex
                    };

                    if (i % 100 == 0)
                        Logger.Log($"RB Point [{i}]: t={offset.TotalSeconds:F3}s, Speed={point.SpeedMph:F1}mph, Gx={point.GForceX:F2}, Record={record}");

                    points.Add(point);
                }

                Logger.Log($"[RaceBoxLoader] Load complete — {points.Count} points loaded.");
            }

            return points;
        }


        /// <summary>
        /// Loads only the header info: discipline, run count, and first complete run index.
        /// </summary>
        public static RaceBoxData LoadHeaderOnly(string filePath)
        {
            Logger.Log("[RaceBoxLoader] LoadHeaderOnly started...");

            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = false,
                MissingFieldFound = null,
                BadDataFound = null,
                Delimiter = ","
            });

            var allRows = new List<string[]>();

            Logger.Log("[RaceBoxLoader] Starting CSV read loop in LoadHeaderOnly...");

            while (csv.Read())
            {
                int fieldCount = csv.Parser.Count;
                if (fieldCount > 100)
                    fieldCount = 100;

                var row = new List<string>();
                for (int i = 0; i < fieldCount; i++)
                    row.Add(csv.GetField(i));

                allRows.Add(row.ToArray());
            }

            Logger.Log($"[RaceBoxLoader] CSV read complete in LoadHeaderOnly — {allRows.Count} rows");

            int runCount = int.Parse(allRows[7][1]);
            string discipline = allRows[8][1];

            int firstCompleteRunIndex = -1;
            for (int i = 0; i < runCount; i++)
            {
                var row = allRows[9 + i]; // e.g. "Run 1 times", "Run 2 times", etc.
                if (row.Length > 1)
                {
                    var timesRaw = row[1]; // second column
                    if (!string.IsNullOrWhiteSpace(timesRaw))
                    {
                        var times = timesRaw.Split(';');
                        bool allNonZero = times.All(t => double.TryParse(t, out var val) && val > 0);

                        if (allNonZero)
                        {
                            string label = row[0]; // e.g., "Run 6 times"
                            string[] labelParts = label.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                            if (labelParts.Length >= 2 && int.TryParse(labelParts[1], out int runNumber))
                            {
                                firstCompleteRunIndex = runNumber;  // ✅ actual run number in the "Run" column
                                break;
                            }
                        }

                    }
                }
            }


            Logger.Log($"[RaceBox] Header loaded: {runCount} runs, Discipline: {discipline}, FirstComplete: {(firstCompleteRunIndex >= 0 ? firstCompleteRunIndex + 1 : "None")}");
            
            return new RaceBoxData
            {
                RunCount = runCount,
                Discipline = discipline,
                FirstCompleteRunIndex = firstCompleteRunIndex >= 0 ? firstCompleteRunIndex : null
            };

        }
        private static int GetHeaderIndex(string[] headers, string name)
        {
            for (int i = 0; i < headers.Length; i++)
            {
                if (headers[i].Trim().Equals(name, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return -1;
        }


    }
}
