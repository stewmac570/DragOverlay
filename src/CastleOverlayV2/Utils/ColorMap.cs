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

            // Mini Logger channels — throttle and RPM share the Castle axes, so they need
            // hues that read apart from the Castle white throttle and rust RPM.
            { "Logger Throttle %", new ScottPlot.Color(0xFF, 0xE0, 0x66) },  // #FFE066  yellow
            { "Logger Throttle Out %", new ScottPlot.Color(0x66, 0xE0, 0xFF) }, // #66E0FF cyan
            { "Logger TC State", new ScottPlot.Color(0xD5, 0x8C, 0xFF) },       // #D58CFF violet
            { "Logger RPM", new ScottPlot.Color(0xF2, 0x9E, 0x86) },         // #F29E86  light rust
            { "Logger Wheel Speed", new ScottPlot.Color(0xFF, 0x8F, 0xB1) }, // #FF8FB1  pink
            { "Logger Rear Speed", new ScottPlot.Color(0x66, 0xCC, 0xFF) },  // #66CCFF  sky blue
            { "Logger Slip %", new ScottPlot.Color(0xB8, 0xE9, 0x86) },      // #B8E986  lime
            { "Logger Wheel rev/s", new ScottPlot.Color(0xFF, 0x8F, 0xB1) }, // same quantity as Wheel Speed
        };

        public static ScottPlot.Color GetColor(string channelName)
        {
            if (ChannelColors.TryGetValue(channelName, out var color))
                return color;

            return new ScottPlot.Color(0x9A, 0xA3, 0xB2);
        }
    }
}
