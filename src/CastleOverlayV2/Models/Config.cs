using System.Collections.Generic;

namespace CastleOverlayV2.Models
{

    public class Config
    {
        public bool IsFourPoleMode { get; set; } = false;


        public Dictionary<string, bool> ChannelVisibility { get; set; }


        public double AlignmentThreshold { get; set; } = 1.0;

        public bool EnableDebugLogging { get; set; } = false;

        public string BuildNumber { get; set; } = "1.12";


        public Config()
        {
            ChannelVisibility = new Dictionary<string, bool>
            {
                { "RPM", true },
                { "Throttle", true },
                { "Voltage", true },
                { "Current", true },
                { "Ripple", true },
                { "PowerOut", true },
                { "MotorTemp", true },
                { "ESC Temp", true },
                { "MotorTiming", true },
                { "Acceleration", true },
                 { "RaceBox Speed", true },
                 { "RaceBox G-Force X", true }
            };
        }
    }
}
