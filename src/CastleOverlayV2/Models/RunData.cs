// File: src/CastleOverlayV2/Models/RunData.cs
using System.Collections.Generic;
using System.Linq;

namespace CastleOverlayV2.Models
{
    public class RunData
    {
        public string FileName { get; set; }

        // Absolute path of the original source file on disk. Tracked so the project saver
        // can embed the original bytes back into a .dragoverlay package without
        // reconstructing CSV content from parsed values. Null if loaded from somewhere
        // other than a local file (rare).
        public string? SourcePath { get; set; }

        // ✅ Castle logs use this (single combined point list)
        public List<DataPoint> DataPoints { get; set; }

        // ✅ RaceBox logs use this (channel → time series of Castle-compatible points)
        public Dictionary<string, List<DataPoint>> Data { get; set; }

        // ✅ Used to distinguish Castle vs RaceBox
        public bool IsRaceBox { get; set; }

        public List<double>? SplitTimes { get; set; }

        // ✅ User-friendly split labels from CSV (e.g., "66 feet", "132 feet")
        public List<string>? SplitLabels { get; set; }

        // 🆕 Per-run constant time shift applied at plot-time (milliseconds; + = shift right)
        public double TimeShiftMs { get; set; }

        public TuneSettings? Tune { get; set; }

        // ── Reversible manual trim (Castle runs) ──────────────────────────────
        // BaselineDataPoints holds the full post-load sample list (whatever auto-trim
        // produced, or the full re-zeroed log when auto-trim was skipped). DataPoints is
        // the displayed/working subset. Manual trim sets a time window over the baseline
        // in the run's own (re-zeroed) time coordinate; null = no bound on that side.
        // Reset clears the window and restores the baseline.
        public List<DataPoint>? BaselineDataPoints { get; set; }
        public double? TrimStartTime { get; set; }
        public double? TrimEndTime { get; set; }

        public RunData()
        {
            DataPoints = new List<DataPoint>();
            Data = new Dictionary<string, List<DataPoint>>();
            IsRaceBox = false;
            TimeShiftMs = 0;
        }

        /// <summary>
        /// Capture the current <see cref="DataPoints"/> as the immutable trim baseline,
        /// if one hasn't been captured yet. Call once after load.
        /// </summary>
        public void CaptureTrimBaseline()
        {
            BaselineDataPoints ??= new List<DataPoint>(DataPoints);
        }

        /// <summary>
        /// Recompute <see cref="DataPoints"/> from the baseline and the current trim window.
        /// No-op for RaceBox runs or before a baseline exists. Inverted/empty windows that
        /// would discard every sample are ignored (the request is treated as a no-op).
        /// </summary>
        public void ApplyManualTrim()
        {
            if (IsRaceBox || BaselineDataPoints == null)
                return;

            IEnumerable<DataPoint> kept = BaselineDataPoints;
            if (TrimStartTime is double start)
                kept = kept.Where(p => p.Time >= start);
            if (TrimEndTime is double end)
                kept = kept.Where(p => p.Time <= end);

            var result = kept.ToList();
            if (result.Count == 0)
                return; // would empty the run — ignore the trim

            DataPoints = result;
        }
    }
}
