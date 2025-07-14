using System.Collections.Generic;

namespace CastleOverlayV2.Models
{
    /// <summary>
    /// Represents the structure of the config.json file.
    /// Stores user preferences for channel visibility and other settings.
    /// </summary>
    public class Config
    {
        /// <summary>
        /// Stores ON/OFF toggle states for each channel.
        /// Key: channel name (e.g., "RPM"), Value: true (ON) or false (OFF)
        /// </summary>
        public Dictionary<string, bool> ChannelVisibility { get; set; } = new Dictionary<string, bool>();

        /// <summary>
        /// Launch point alignment threshold (e.g., Power-Out or Current threshold).
        /// Used by AlignmentService for t=0.
        /// </summary>
        public double AlignmentThreshold { get; set; } = 1.0;

        /// <summary>
        /// Constructor — initializes empty defaults if needed.
        /// </summary>
        public Config()
        {
        }
    }
}
