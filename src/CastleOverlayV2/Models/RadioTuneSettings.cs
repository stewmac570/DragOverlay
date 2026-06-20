namespace CastleOverlayV2.Models
{
    public sealed class RadioTuneSettings
    {
        public string ProfileName { get; set; } = "";
        public string ThrottleSpeedMode { get; set; } = "";
        public int Mode { get; set; } = 1;
        public string SpeedType { get; set; } = "Normal";
        public double? AllTurnPercent { get; set; }
        public double? AllReturnPercent { get; set; }
        public double? HighTurnPercent { get; set; }
        public double? HighReturnPercent { get; set; }
        public double? MiddleTurnPercent { get; set; }
        public double? MiddleReturnPercent { get; set; }
        public double? LowTurnPercent { get; set; }
        public double? LowReturnPercent { get; set; }
        public double? AllPercent { get; set; }
        public double? HighPercent { get; set; }
        public double? Point1Percent { get; set; }
        public double? MiddlePercent { get; set; }
        public double? Point2Percent { get; set; }
        public double? LowPercent { get; set; }

        public RadioTuneSettings Clone() => new()
        {
            ProfileName = ProfileName,
            ThrottleSpeedMode = ThrottleSpeedMode,
            Mode = Mode,
            SpeedType = SpeedType,
            AllTurnPercent = AllTurnPercent,
            AllReturnPercent = AllReturnPercent,
            HighTurnPercent = HighTurnPercent,
            HighReturnPercent = HighReturnPercent,
            MiddleTurnPercent = MiddleTurnPercent,
            MiddleReturnPercent = MiddleReturnPercent,
            LowTurnPercent = LowTurnPercent,
            LowReturnPercent = LowReturnPercent,
            AllPercent = AllPercent,
            HighPercent = HighPercent,
            Point1Percent = Point1Percent,
            MiddlePercent = MiddlePercent,
            Point2Percent = Point2Percent,
            LowPercent = LowPercent
        };
    }
}
