using System.Collections.Generic;

namespace CastleOverlayV2.Utils
{
    public static class ChannelColorMap
    {
        private static readonly Dictionary<string, ScottPlot.Color> ChannelColors = new()
        {
            { "Voltage", new ScottPlot.Color(255, 0, 0) },         // #FF0000
            { "Ripple", new ScottPlot.Color(128, 0, 128) },        // #800080
            { "Current", new ScottPlot.Color(0, 128, 0) },         // #008000
            { "PowerOut", new ScottPlot.Color(70, 130, 180) },     // #4682B4
            { "MotorTemp", new ScottPlot.Color(147, 112, 219) },
            { "RPM", new ScottPlot.Color(165, 42, 42) },           // #A52A2A

            // Legacy Castle format (ms)
            //{ "Throttle", new ScottPlot.Color(0, 0, 0) },          // #000000

            // New percent-based channel
            { "Throttle %", new ScottPlot.Color(0, 0, 0) },        // #000000

            { "Acceleration", new ScottPlot.Color(65, 105, 225) }, // #4169E1
            { "MotorTiming", new ScottPlot.Color(0, 0, 128) },     // #000080
            { "ESC Temp", new ScottPlot.Color(255, 0, 250) },      // #FF00FA

            // ✅ RaceBox channels
            { "RaceBox Speed", new ScottPlot.Color(255, 140, 0) },   // #FF8C00
            { "RaceBox G-Force X", new ScottPlot.Color(0, 206, 209) } // #00CED1
        };

        public static ScottPlot.Color GetColor(string channelName)
        {
            if (ChannelColors.TryGetValue(channelName, out var color))
                return color;

            // Fallback: neutral gray instead of exception
            return new ScottPlot.Color(128, 128, 128);
        }
    }
}
