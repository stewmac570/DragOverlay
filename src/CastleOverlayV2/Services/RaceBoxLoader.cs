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

                // 🛡 Validate RaceBox signature in Row 1
                // ✅ At least 9 rows, and row 8 = run count, row 9 = disciplines with semicolons
                bool hasEnoughRows = allRows.Count > 8;
                bool hasRunCount = hasEnoughRows && allRows[7].Length > 1 && int.TryParse(allRows[7][1], out _);
                bool hasDisciplines = hasEnoughRows && allRows[8].Length > 1 && allRows[8][1].Contains(';');

                if (!hasRunCount || !hasDisciplines)
                {
                    Logger.Log("❌ File rejected: not a RaceBox CSV — missing run count or disciplines format.");
                    MessageBox.Show("This file is not a valid RaceBox export.\n\nExpected at least 9 header rows and discipline labels.",
                        "Invalid File", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return null;
                }

              
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
            // === Auto-trim: detect drag launch and clip to ±2.5s ===
            int launchIndex = DetectRaceBoxLaunchIndex(points);
            double t0 = points[launchIndex].Time.TotalSeconds;
            double tMin = t0 - 0.5;
            double tMax = t0 + 2.5;

            Logger.Log($"[RaceBoxLoader] Trimming to ±2.5s around launch (t0 = {t0:F2}s)...");

            points = points
                .Where(p => p.Time.TotalSeconds >= tMin && p.Time.TotalSeconds <= tMax)
                .ToList();

            // Shift timestamps so launch becomes t = 0
            foreach (var p in points)
                p.Time = TimeSpan.FromSeconds(p.Time.TotalSeconds - t0);

            Logger.Log($"[RaceBoxLoader] Trimmed points: {points.Count}");

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

            // 🛡 Validate RaceBox signature in Row 1
            if (allRows.Count == 0 || allRows[0].Length == 0 || !string.Join(",", allRows[0]).Contains("RaceBox"))
            {
                Logger.Log("❌ File rejected: not a RaceBox CSV. First row = " +
                    (allRows.Count > 0 ? string.Join(",", allRows[0]) : "[empty]"));
                MessageBox.Show("This file is not a valid RaceBox export.\n\nPlease select a real RaceBox CSV file.",
                    "Invalid File", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }


            // 🛡️ Validate header rows exist before accessing them
            if (allRows.Count <= 8)
                throw new Exception("RaceBox file is too short — missing expected header rows (need at least 9).");

            // 🛡️ Validate expected columns exist
            if (allRows[7].Length <= 1)
                throw new Exception("Row 8 is missing the run count value in column 2.");


            if (allRows[8].Length <= 1)
                throw new Exception("Row 9 is missing the discipline value in column 2.");
            // ✅ Safe to parse
            int runCount = int.Parse(allRows[7][1]);

            // 🛡️ Defensive check for missing discipline value
            if (allRows[8].Length <= 1 || string.IsNullOrWhiteSpace(allRows[8][1]))
            {
                Logger.Log("❌ Row 9 discipline value missing or empty. Raw row: " + string.Join(",", allRows[8]));
                throw new Exception("Row 9 is missing the discipline value in column 2.");
            }

            // ✅ Extract split labels from semicolon-separated string
            string discipline = allRows[8][1];  // e.g., "6 feet;66 feet;132 feet;140 feet"
            Logger.Log($"[RaceBoxLoader] Raw discipline string: {discipline}");

            var splitLabels = discipline
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .ToList();


            int firstCompleteRunIndex = -1;

            List<double>? splitTimes = null;

            for (int i = 0; i < runCount; i++)
            {
                int rowIndex = 9 + i;

                if (allRows.Count <= rowIndex)
                {
                    Logger.Log($"❌ RaceBoxLoader: expected row {rowIndex}, but only {allRows.Count} rows available");
                    continue;
                }

                var row = allRows[rowIndex];

                if (row.Length <= 1)
                {
                    Logger.Log($"❌ RaceBoxLoader: row {rowIndex} too short — Length = {row.Length}");
                    continue;
                }

                var timesRaw = row[1]; // second column

                if (!string.IsNullOrWhiteSpace(timesRaw))
                {
                    var times = timesRaw.Split(';');
                    var parsed = times
                        .Select(t => double.TryParse(t, out var val) ? val : 0)
                        .ToList();

                    bool allNonZero = parsed.All(val => val > 0);

                    if (allNonZero)
                    {
                        string label = row[0]; // e.g., "Run 6 times"
                        string[] labelParts = label.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (labelParts.Length >= 2 && int.TryParse(labelParts[1], out int runNumber))
                        {
                            firstCompleteRunIndex = runNumber;  // ✅ actual run number in the "Run" column
                            splitTimes = parsed;
                            break;
                        }
                    }
                }
            }



            Logger.Log($"[RaceBox] Header loaded: {runCount} runs, Discipline: {discipline}, FirstComplete: {(firstCompleteRunIndex >= 0 ? firstCompleteRunIndex + 1 : "None")}");

            return new RaceBoxData
            {
                RunCount = runCount,
                Discipline = discipline,
                FirstCompleteRunIndex = firstCompleteRunIndex >= 0 ? firstCompleteRunIndex : null,
                SplitTimes = splitTimes,
                SplitLabels = splitLabels
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
        private static int DetectRaceBoxLaunchIndex(List<RaceBoxPoint> points)
        {
            for (int i = 0; i < points.Count - 6; i++)
            {
                double s0 = points[i].SpeedMph;
                double s1 = points[i + 1].SpeedMph;
                double s2 = points[i + 2].SpeedMph;
                double s3 = points[i + 3].SpeedMph;
                double s4 = points[i + 4].SpeedMph;
                double s5 = points[i + 5].SpeedMph;

                // Look for a smooth and rapid increase
                if ((s1 - s0 > 0.2) &&
                    (s2 - s1 > 0.2) &&
                    (s3 - s2 > 0.2) &&
                    (s4 - s3 > 0.2) &&
                    (s5 - s4 > 0.2))
                {
                    Logger.Log($"[RaceBoxLoader] Launch detected at row {i} — Speed rising from {s0:F2} to {s5:F2} mph");
                    return i;
                }
            }

            Logger.Log("[RaceBoxLoader] No launch point detected — fallback to first row.");
            return 0;
        }
    }
}
