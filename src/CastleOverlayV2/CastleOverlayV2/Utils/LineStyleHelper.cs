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
                0 => ScottPlot.LinePattern.Solid,
                1 => ScottPlot.LinePattern.Dashed,
                2 => ScottPlot.LinePattern.Dotted,
                _ => ScottPlot.LinePattern.Solid
            };
        }
    }
}
