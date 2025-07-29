using ScottPlot;

namespace CastleOverlayV2.Utils
{
    public static class LineStyleHelper
    {
        // ⬇️ Thickness per log run (index 0 = Log 1)
        public static double GetLineWidth(int runIndex)
        {
            return runIndex switch
            {
                0 => 2.5, // Log 1 — boldest
                1 => 1.5, // Log 2 — medium
                2 => 1, // Log 3 — thinnest
                _ => 2.0
            };
        }

        public static ScottPlot.LinePattern GetLinePattern(int index)
        {
            return index switch
            {
                0 or 3 => ScottPlot.LinePattern.Solid,    // Run 1 and RaceBox 1
                1 or 4 => ScottPlot.LinePattern.Dashed,   // Run 2 and RaceBox 2
                2 or 5 => ScottPlot.LinePattern.Dotted,   // Run 3 and RaceBox 3
                99 => ScottPlot.LinePattern.Dashed,         // ✅ RaceBox Split Lines
                _ => ScottPlot.LinePattern.Solid
            };
        }

    }
}
