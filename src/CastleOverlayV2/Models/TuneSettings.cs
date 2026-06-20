namespace CastleOverlayV2.Models
{
    public sealed class TuneSettings
    {
        public string SourceFileName { get; set; } = "";
        public string DeviceName { get; set; } = "";
        public string Firmware { get; set; } = "";
        public string CreatedText { get; set; } = "";

        public List<TuneCurvePoint> ThrottleCurve { get; } = new();
        public List<TuneCurvePoint> BoostByThrottleCurve { get; } = new();

        public bool BoostEnabled { get; set; }
        public string BoostModeEnableText { get; set; } = "";
        public string BoostTimingMode { get; set; } = "";
        public double? BoostTimingAmountDeg { get; set; }
        public double? BoostStartRpm { get; set; }
        public double? BoostEndRpm { get; set; }
        public double? BoostStartThrottlePct { get; set; }
        public double? BoostEndThrottlePct { get; set; }

        public bool TurboEnabled { get; set; }
        public string TurboEnableText { get; set; } = "";
        public double? TurboTimingAmountDeg { get; set; }
        public double? TurboActivationThrottlePct { get; set; }
        public double? TurboAccelDegPerSecond { get; set; }
        public double? TurboDecelDegPerSecond { get; set; }

        public string MaxPower { get; set; } = "";
        public string DragBrake { get; set; } = "";
        public string SampleFrequency { get; set; } = "";

        public RadioTuneSettings Radio { get; set; } = new();
    }
}
