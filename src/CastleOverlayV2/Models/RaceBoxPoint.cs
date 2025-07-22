using System;

namespace CastleOverlayV2.Models
{
    /// <summary>
    /// Represents a single telemetry data point from a RaceBox CSV file.
    /// </summary>
    public class RaceBoxPoint
    {
        public TimeSpan Time { get; set; }            // Relative to t=0
        public double SpeedMph { get; set; }          // Converted from m/s
        public double GForceX { get; set; }
        public int RunIndex { get; set; }             // From the Run column
    }
}
