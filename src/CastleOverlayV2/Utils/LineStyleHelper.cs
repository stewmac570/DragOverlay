using ScottPlot;

namespace CastleOverlayV2.Utils
{
    /// <summary>
    /// Run identity = line style (Run 1/RB 1 = solid, Run 2/RB 2 = dashed, Run 3/RB 3 = dotted).
    /// Channel identity = colour. Focus identity = opacity + line width (in <see cref="Plot.PlotManager"/>).
    /// </summary>
    public static class LineStyleHelper
    {
        public static ScottPlot.LinePattern GetLinePattern(int index)
        {
            return index switch
            {
                0 or 3 or 6 => ScottPlot.LinePattern.Solid,    // Castle 1, GPS 1, Data log 1
                1 or 4 or 7 => ScottPlot.LinePattern.Dashed,   // Castle 2, GPS 2, Data log 2
                2 or 5 or 8 => ScottPlot.LinePattern.Dotted,   // Castle 3, GPS 3, Data log 3
                99 => ScottPlot.LinePattern.Dashed,         // RaceBox Split Lines
                _ => ScottPlot.LinePattern.Solid
            };
        }
    }
}
