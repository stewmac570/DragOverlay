using CastleOverlayV2.Utils;
using ScottPlot;

namespace CastleOverlayV2.Tests;

public class LineStyleHelperTests
{
    // Note: GetLineWidth was removed when Phase 3 (#74) made line width focus-based
    // (set in PlotManager.WidthFor), not slot-based. Run identity is now line style only.

    [Theory]
    [InlineData(0, "Solid")]  // Castle Run 1
    [InlineData(3, "Solid")]  // RaceBox 1 (paired with Run 1)
    [InlineData(1, "Dashed")] // Castle Run 2
    [InlineData(4, "Dashed")] // RaceBox 2 (paired with Run 2)
    [InlineData(2, "Dotted")] // Castle Run 3
    [InlineData(5, "Dotted")] // RaceBox 3 (paired with Run 3)
    [InlineData(99, "Dashed")] // Split lines marker
    [InlineData(-1, "Solid")]  // Out of range — default
    [InlineData(7, "Solid")]   // Out of range — default
    public void GetLinePattern_returns_expected(int index, string expectedName)
    {
        var expected = expectedName switch
        {
            "Solid" => LinePattern.Solid,
            "Dashed" => LinePattern.Dashed,
            "Dotted" => LinePattern.Dotted,
            _ => throw new System.ArgumentException($"unknown pattern: {expectedName}")
        };

        Assert.Equal(expected, LineStyleHelper.GetLinePattern(index));
    }

    [Fact]
    public void Castle_and_RaceBox_pairs_share_LinePattern()
    {
        // The slot-pairing convention (Castle slot N ↔ RaceBox slot N+3) requires
        // matched line patterns so a Castle/RaceBox overlay reads as one run.
        Assert.Equal(LineStyleHelper.GetLinePattern(0), LineStyleHelper.GetLinePattern(3));
        Assert.Equal(LineStyleHelper.GetLinePattern(1), LineStyleHelper.GetLinePattern(4));
        Assert.Equal(LineStyleHelper.GetLinePattern(2), LineStyleHelper.GetLinePattern(5));
    }
}
