using CastleOverlayV2.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace CastleOverlayV2.Services
{
    public static class RaceBoxLoader
    {
        /// <summary>
        /// Loads only the header metadata from a RaceBox CSV file.
        /// Extracts run count, disciplines, run times, and identifies the first complete run.
        /// </summary>
        public static RaceBoxData LoadHeaderOnly(string filePath)
        {
            var lines = File.ReadAllLines(filePath);
            var result = new RaceBoxData();

            int runCount = 0;
            int runStartLine = 0;

            // Step 1: Read "Runs" value and find Run X lines
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];

                if (line.StartsWith("Runs", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = line.Split(',');
                    if (parts.Length > 1 && int.TryParse(parts[1], out runCount))
                    {
                        result.RunCount = runCount;
                        runStartLine = i + 1; // First "Run X times" line follows
                    }
                }

                if (line.StartsWith("Disciplines", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = line.Split(',');
                    if (parts.Length > 1)
                    {
                        var disciplineString = parts[1];
                        var splits = disciplineString.Split(';');
                        foreach (var s in splits)
                            result.Disciplines.Add(s.Trim());
                    }
                }
            }

            // Step 2: Extract Run X times
            for (int i = 0; i < result.RunCount; i++)
            {
                int lineIndex = runStartLine + i;
                if (lineIndex >= lines.Length)
                    break;

                var parts = lines[lineIndex].Split(',');
                if (parts.Length < 2)
                    continue;

                var timesRaw = parts[1].Split(';');
                var runTimes = new List<double>();
                bool allNonZero = true;

                foreach (var t in timesRaw)
                {
                    if (double.TryParse(t.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double val))
                    {
                        runTimes.Add(val);
                        if (val == 0)
                            allNonZero = false;
                    }
                    else
                    {
                        runTimes.Add(0);
                        allNonZero = false;
                    }
                }

                result.RunTimes.Add(runTimes);

                if (allNonZero && result.FirstCompleteRunIndex == null)
                {
                    result.FirstCompleteRunIndex = i;
                }
            }

            return result;
        }
    }
}
