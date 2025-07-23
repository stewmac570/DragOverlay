namespace CastleOverlayV2.Models
{



    public class DataPoint
    {
        public double Time { get; set; }

        // Castle fields
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

        // ✅ RaceBox Y-axis data for plotting (used for RaceBox Speed / G-X only)
        public double Y { get; set; }
    }
}