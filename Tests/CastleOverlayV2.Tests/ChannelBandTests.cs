using CastleOverlayV2.Plot;
using Xunit;

namespace CastleOverlayV2.Tests;

public class ChannelBandTests
{
    // Where a value lands on the chart, 0 = bottom edge, 1 = top edge.
    private static double ChartFraction(double value, (double Min, double Max) axis) =>
        (value - axis.Min) / (axis.Max - axis.Min);

    [Fact]
    public void Full_height_keeps_the_original_range()
    {
        var axis = PlotManager.BandAxisRange(0, 800, 1.0, 0.0);
        Assert.Equal(0, axis.Min, 6);
        Assert.Equal(800, axis.Max, 6);
    }

    [Fact]
    public void Bottom_band_puts_the_data_in_the_lower_part_of_the_chart()
    {
        var axis = PlotManager.BandAxisRange(0, 800, 0.45, 0.0);
        Assert.Equal(0.0, ChartFraction(0, axis), 6);
        Assert.Equal(0.45, ChartFraction(800, axis), 6);
    }

    [Fact]
    public void Top_band_puts_the_data_in_the_upper_part_of_the_chart()
    {
        var axis = PlotManager.BandAxisRange(6, 9, 0.25, 1.0);
        Assert.Equal(0.75, ChartFraction(6, axis), 6);
        Assert.Equal(1.0, ChartFraction(9, axis), 6);
    }

    [Fact]
    public void Middle_band_is_centred()
    {
        var axis = PlotManager.BandAxisRange(-5, 7, 0.5, 0.5);
        Assert.Equal(0.25, ChartFraction(-5, axis), 6);
        Assert.Equal(0.75, ChartFraction(7, axis), 6);
    }
}
