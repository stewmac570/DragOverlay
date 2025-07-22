using System.Collections.Generic;

namespace CastleOverlayV2.Models
{
    /// <summary>
    /// Holds metadata parsed from the header of a RaceBox CSV file.
    /// Includes run summaries, discipline names, and validity.
    /// </summary>
    public class RaceBoxData
    {
        /// <summary>
        /// Number of runs listed in the file (from the "Runs" field).
        /// </summary>
        public int RunCount { get; set; }

        /// <summary>
        /// Split names (e.g., "6 feet", "66 feet", "132 feet").
        /// </summary>
        public List<string> Disciplines { get; set; } = new List<string>();

        /// <summary>
        /// List of split times for each run.
        /// Each sublist contains times for one run.
        /// </summary>
        public List<List<double>> RunTimes { get; set; } = new List<List<double>>();

        /// <summary>
        /// Index of the first complete run (0-based).
        /// Null if no complete run is found.
        /// </summary>
        public int? FirstCompleteRunIndex { get; set; }
    }
}
