namespace CastleOverlayV2.Utils
{
    public static class LineStyleHelper
    {
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
