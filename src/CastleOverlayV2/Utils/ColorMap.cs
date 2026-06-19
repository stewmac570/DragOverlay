using System.Collections.Generic;

namespace CastleOverlayV2.Utils
{
    /// <summary>
    /// Channel → trace color, dark-theme palette from <c>Docs/DragOverlay_UI_Spec.md</c> §5.
    /// Channel hue is stable; focus state (handled in <see cref="Plot.PlotManager"/>) changes only opacity.
    /// </summary>
    public static class ChannelColorMap
    {
        private static readonly Dictionary<string, ScottPlot.Color> ChannelColors = new()
        {
            // Castle ESC channels
            { "RPM", new ScottPlot.Color(0x4C, 0x9A, 0xED) },          // #4C9AED  blue
            { "Throttle %", new ScottPlot.Color(0xEF, 0x9F, 0x27) },   // #EF9F27  amber
            { "Current", new ScottPlot.Color(0x2B, 0xB4, 0x8A) },      // #2BB48A  teal
            { "Voltage", new ScottPlot.Color(0xE0, 0x74, 0x4B) },      // #E0744B  coral
            { "PowerOut", new ScottPlot.Color(0x7F, 0xB6, 0xF2) },     // #7FB6F2  light blue
            { "Ripple", new ScottPlot.Color(0x97, 0xC4, 0x59) },       // #97C459  green
            { "ESC Temp", new ScottPlot.Color(0xE2, 0x4B, 0x4A) },     // #E24B4A  red
            { "MotorTemp", new ScottPlot.Color(0xBA, 0x75, 0x17) },    // #BA7517  deep amber
            { "MotorTiming", new ScottPlot.Color(0xB4, 0xB2, 0xA9) },  // #B4B2A9  warm grey
            { "Acceleration", new ScottPlot.Color(0xD8, 0x5A, 0x30) }, // #D85A30  dark coral

            // RaceBox GPS channels
            { "RaceBox Speed", new ScottPlot.Color(0xD4, 0x53, 0x7E) },     // #D4537E  pink
            { "RaceBox G-Force X", new ScottPlot.Color(0x9F, 0x8F, 0xE0) }, // #9F8FE0  light purple
        };

        public static ScottPlot.Color GetColor(string channelName)
        {
            if (ChannelColors.TryGetValue(channelName, out var color))
                return color;

            // Fallback for any unmapped channel: text.secondary grey.
            return new ScottPlot.Color(0x9A, 0xA3, 0xB2);
        }
    }
}
