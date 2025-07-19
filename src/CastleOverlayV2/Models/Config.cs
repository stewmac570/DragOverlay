using System.Collections.Generic;

namespace CastleOverlayV2.Models
{
    /// <summary>
    /// Represents the structure of the config.json file.
    /// Stores user preferences for channel visibility and other settings.
    /// </summary>
    public class Config
    {
        public bool IsFourPoleMode { get; set; } = false;

        /// <summary>
        /// Stores ON/OFF toggle states for each channel.
        /// Key: channel name (e.g., "RPM"), Value: true (ON) or false (OFF)
        /// </summary>
        public Dictionary<string, bool> ChannelVisibility { get; set; }

        /// <summary>
        /// Launch point alignment threshold (e.g., Power-Out or Current threshold).
        /// Used by AlignmentService for t=0.
        /// </summary>
        public double AlignmentThreshold { get; set; } = 1.0;

        public bool EnableDebugLogging { get; set; } = false;

        public string BuildNumber { get; set; } = "1.01";

        /// <summary>
        /// Constructor — initializes default visibility states for all known channels.
        /// </summary>
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
                { "Acceleration", true }
            };
        }
    }
}
