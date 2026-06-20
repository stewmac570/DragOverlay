using System.Collections.Generic;

namespace CastleOverlayV2.Utils
{
    /// <summary>
    /// Channel → trace color. Restored to the Castle Link 2 reference palette to match
    /// the v1.12 screenshot (Resources/main-ui-v1.12_1.png) — users compare traces against
    /// Castle Link 2 side-by-side, so the hues need to match.
    /// </summary>
    public static class ChannelColorMap
    {
        // Castle-style palette, brightened a touch for the dark plot background.
        // Same hue family as Castle Link 2 — each channel reads as the "same color" —
        // but lifted in luminance/saturation so traces actually pop. Throttle is
        // white because black-on-black is invisible.
        private static readonly Dictionary<string, ScottPlot.Color> ChannelColors = new()
        {
            // Castle ESC channels
            { "RPM", new ScottPlot.Color(0xCD, 0x57, 0x3F) },           // #CD573F  warm rust (was brown)
            { "Throttle %", new ScottPlot.Color(0xF0, 0xF0, 0xF0) },    // #F0F0F0  white  (was black — invisible on dark)
            { "Voltage", new ScottPlot.Color(0xFF, 0x4D, 0x4D) },       // #FF4D4D  punchy red
            { "Current", new ScottPlot.Color(0x2E, 0xD9, 0x57) },       // #2ED957  bright green
            { "Ripple", new ScottPlot.Color(0xC8, 0x55, 0xD6) },        // #C855D6  brighter purple
            { "PowerOut", new ScottPlot.Color(0x6C, 0xB0, 0xE0) },      // #6CB0E0  brighter steel blue
            { "MotorTemp", new ScottPlot.Color(0xB1, 0x8B, 0xF5) },     // #B18BF5  brighter medium purple
            { "ESC Temp", new ScottPlot.Color(0xFF, 0x4A, 0xE0) },      // #FF4AE0  brighter magenta
            { "MotorTiming", new ScottPlot.Color(0x4A, 0x7A, 0xFF) },   // #4A7AFF  lifted navy
            { "Acceleration", new ScottPlot.Color(0x6F, 0xA1, 0xFF) },  // #6FA1FF  brighter royal blue

            // RaceBox GPS channels
            { "RaceBox Speed", new ScottPlot.Color(0xFF, 0xA0, 0x33) },     // #FFA033  brighter orange
            { "RaceBox Distance", new ScottPlot.Color(0x8C, 0xCB, 0x35) },  // #8CCB35  brighter green
            { "RaceBox G-Force X", new ScottPlot.Color(0x33, 0xE6, 0xE6) }, // #33E6E6  brighter turquoise
        };

        public static ScottPlot.Color GetColor(string channelName)
        {
            if (ChannelColors.TryGetValue(channelName, out var color))
                return color;

            return new ScottPlot.Color(0x9A, 0xA3, 0xB2);
        }
    }
}
