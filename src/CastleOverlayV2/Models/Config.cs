using System.Collections.Generic;
using Newtonsoft.Json;

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

        /// <summary>This app's version. Bump with installer.iss MyAppVersion when releasing.</summary>
        public const string CurrentBuildNumber = "1.18";

        // Written to config.json for reference; always reset to CurrentBuildNumber on load.
        public string BuildNumber { get; set; } = CurrentBuildNumber;

        // ---- Default open folders (Settings dialog) -----------------------
        public string CastleLogDirectory { get; set; } = "";
        public string RaceBoxLogDirectory { get; set; } = "";
        public string TuneDirectory { get; set; } = "";
        public string DataLogDirectory { get; set; } = "";

        /// <summary>Front wheel outside diameter used to convert Mini Logger wheel rev/s to mph.</summary>
        public double FrontTyreDiameterMm { get; set; } = 70.0;

        /// <summary>Rear wheel outside diameter used for the Mini Logger derived rear speed.</summary>
        public double? RearWheelDiameterMm { get; set; }

        public int? PinionTeeth { get; set; }
        public int? SpurTeeth { get; set; }

        /// <summary>Additional gearbox reduction. 1.0 means no gearbox.</summary>
        public double? TransmissionRatio { get; set; } = 1.0;

        // ---- TC curve builder (blank/null until the user enters a value) ----
        public double? TcCurveMargin { get; set; }
        public bool? TcCurveMarginIsPercent { get; set; }
        public double? TcCurveEndSeconds { get; set; }

        // Auto trim on: logs are cut to -0.5 s .. +2.5 s around the launch. Off: full logs.
        public bool AutoTrim { get; set; } = true;

        // ---- Voltage smoothing (Settings dialog) --------------------------
        public bool VoltageSmoothingEnabled { get; set; } = false;
        public int VoltageSmoothingWindow { get; set; } = 5; // odd, 3..15

        // ---- Channel list display choices (Logs page) ---------------------
        // Channel name -> "#RRGGBB". Missing = the default palette colour.
        public Dictionary<string, string> ChannelColors { get; set; } = new();
        public List<string> BoldChannels { get; set; } = new();
        // Channels shown in the box that follows the chart cursor.
        public List<string> CursorChannels { get; set; } = new() { "RPM" };
        // Width of the channel list (dragged by its right edge).
        public int ChannelListWidth { get; set; } = 400;
        // Channel name -> share of the chart height and where that band sits.
        public Dictionary<string, ChannelBand> ChannelBands { get; set; } = new();
        // Split-view state. Pane numbers are user-facing (1..3); heights are normalized shares.
        public int PaneCount { get; set; } = 1;
        public Dictionary<string, int> ChannelPanes { get; set; } = new();
        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public List<double> PaneHeights { get; set; } = new() { 1.0 };
        // User-facing pane number -> focused channel. Optional in older config files.
        public Dictionary<int, string> PaneFocusedChannels { get; set; } = new();


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
