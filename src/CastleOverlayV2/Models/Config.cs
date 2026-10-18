using System.Collections.Generic;

namespace CastleOverlayV2.Models
{

    /// <summary>Line size for one channel: Height 0.1..1 of the chart, Position 0 (bottom)..1 (top).</summary>
    public class ChannelBand
    {
        public double Height { get; set; } = 1.0;
        public double Position { get; set; } = 0.0;
    }

    public class Config
    {
        public bool IsFourPoleMode { get; set; } = false;


        public Dictionary<string, bool> ChannelVisibility { get; set; }


        public double AlignmentThreshold { get; set; } = 1.0;

        public bool EnableDebugLogging { get; set; } = false;

        public string BuildNumber { get; set; } = "1.16";

        // ---- Default open folders (Settings dialog) -----------------------
        public string CastleLogDirectory { get; set; } = "";
        public string RaceBoxLogDirectory { get; set; } = "";
        public string TuneDirectory { get; set; } = "";

        // ---- Voltage smoothing (Settings dialog) --------------------------
        public bool VoltageSmoothingEnabled { get; set; } = false;
        public int VoltageSmoothingWindow { get; set; } = 5; // odd, 3..15

        // ---- Channel list display choices (Logs page) ---------------------
        // Channel name -> "#RRGGBB". Missing = the default palette colour.
        public Dictionary<string, string> ChannelColors { get; set; } = new();
        public List<string> BoldChannels { get; set; } = new();
        // Channel name -> share of the chart height and where that band sits.
        public Dictionary<string, ChannelBand> ChannelBands { get; set; } = new();


        public Config()
        {
            ChannelVisibility = new Dictionary<string, bool>
            {
                { "RPM", true },
                { "Throttle %", true },
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
