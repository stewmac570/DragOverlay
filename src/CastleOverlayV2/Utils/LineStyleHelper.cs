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
                0 or 3 => ScottPlot.LinePattern.Solid,    // Run 1 and RaceBox 1
                1 or 4 => ScottPlot.LinePattern.Dashed,   // Run 2 and RaceBox 2
                2 or 5 => ScottPlot.LinePattern.Dotted,   // Run 3 and RaceBox 3
                99 => ScottPlot.LinePattern.Dashed,         // RaceBox Split Lines
                _ => ScottPlot.LinePattern.Solid
            };
        }
    }
}
