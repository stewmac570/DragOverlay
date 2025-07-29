namespace CastleOverlayV2.Models
{
    public class RaceBoxData
    {
        public int RunCount { get; set; }
        public string Discipline { get; set; }
        public int? FirstCompleteRunIndex { get; set; }
        public List<double>? SplitTimes { get; set; }

    }
}
