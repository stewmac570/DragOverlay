namespace CastleOverlayV2.Models
{
    public class RunData
    {
        public string FileName { get; set; }

        // ✅ Castle logs use this (single combined point list)
        public List<DataPoint> DataPoints { get; set; }

        // ✅ RaceBox logs use this (channel → time series of Castle-compatible points)
        public Dictionary<string, List<DataPoint>> Data { get; set; }

        // ✅ Raw RaceBox point data (for debugging, optional)
        public Dictionary<string, List<RaceBoxPoint>> RaceBoxData { get; set; }

        // ✅ Used to distinguish Castle vs RaceBox
        public bool IsRaceBox { get; set; }

        public List<double>? SplitTimes { get; set; }

        // 🆕 NEW: User-friendly split labels from CSV (e.g., "66 feet", "132 feet")
        public List<string>? SplitLabels { get; set; }
  

        public RunData()
        {
            DataPoints = new List<DataPoint>();
            Data = new Dictionary<string, List<DataPoint>>();
            RaceBoxData = new Dictionary<string, List<RaceBoxPoint>>();
            IsRaceBox = false;
        }
    }
}
