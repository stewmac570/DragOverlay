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

        // Mini Logger runs (rc-logger, 1000 Hz) also use .Data and live in the RaceBox slots.
        public bool IsMiniLogger { get; set; }

        // Raw Mini Logger values retained for the TC tab. These deliberately do not use the
        // app's 2P/4P display mode: MotorRpm is the firmware's real motor RPM.
        public List<MiniLoggerSample> MiniLoggerSamples { get; set; } = new();

        // True for any run whose real values live in .Data rather than .DataPoints.
        public bool HasChannelData => IsRaceBox || IsMiniLogger;

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

        // Trim windows before each trim or reset, newest on top, so cuts undo one at a time.
        public Stack<(double? Start, double? End)> TrimHistory { get; set; } = new();

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
        /// Returns false when nothing was applied.
        /// </summary>
        public bool ApplyManualTrim()
        {
            if (HasChannelData || BaselineDataPoints == null)
                return false;

            IEnumerable<DataPoint> kept = BaselineDataPoints;
            if (TrimStartTime is double start)
                kept = kept.Where(p => p.Time >= start);
            if (TrimEndTime is double end)
                kept = kept.Where(p => p.Time <= end);

            var result = kept.ToList();
            if (result.Count == 0)
                return false; // would empty the run — ignore the trim

            DataPoints = result;
            return true;
        }

        /// <summary>Set a new trim window, remembering the old one for undo. False if it would empty the run.</summary>
        public bool SetTrimWindow(double? start, double? end)
        {
            var previous = (TrimStartTime, TrimEndTime);
            TrimStartTime = start;
            TrimEndTime = end;
            if (!ApplyManualTrim())
            {
                (TrimStartTime, TrimEndTime) = previous;
                return false;
            }
            TrimHistory.Push(previous);
            return true;
        }

        /// <summary>Put back the trim window from before the last trim or reset. False if there is none.</summary>
        public bool UndoTrim()
        {
            if (TrimHistory.Count == 0 || BaselineDataPoints == null)
                return false;
            (TrimStartTime, TrimEndTime) = TrimHistory.Pop();
            ApplyManualTrim();
            return true;
        }
    }
}
