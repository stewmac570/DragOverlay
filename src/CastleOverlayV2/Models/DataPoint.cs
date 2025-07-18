// File: /src/Models/DataPoint.cs

namespace CastleOverlayV2.Models
{
    /// <summary>
    /// Represents one row of parsed data from the Castle .csv log.
    /// </summary>
    public class DataPoint
    {
        public double Time { get; set; }
        public double Throttle { get; set; }
        public double PowerOut { get; set; }
        public double Voltage { get; set; }
        public double Ripple { get; set; }
        public double Current { get; set; }
        public double Speed { get; set; }
        public double Temperature { get; set; }
        public double MotorTemp { get; set; }
        public double MotorTiming { get; set; }
        public double Acceleration { get; set; }
        public double GovGain { get; set; }

    }
}
