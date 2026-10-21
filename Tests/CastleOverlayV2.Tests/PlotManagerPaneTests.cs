using CastleOverlayV2.Plot;
using ScottPlot.WinForms;
using System.Windows.Forms;

namespace CastleOverlayV2.Tests;

public sealed class PlotManagerPaneTests
{
    [Fact]
    public void Three_panes_link_time_axis_in_every_direction_and_survive_reset()
    {
        using var firstPlot = new FormsPlot();
        var manager = new PlotManager(firstPlot);
        manager.SetPaneCount(3);
        FormsPlot[] plots = Descendants(manager.ChartHost).OfType<FormsPlot>().ToArray();

        Assert.Equal(3, plots.Length);

        plots[2].Plot.Axes.SetLimitsX(2.0, 4.0);
        plots[2].Plot.Axes.ApplyLinkedAxisRules();
        Assert.All(plots, plot =>
        {
            var limits = plot.Plot.Axes.GetLimits();
            Assert.Equal(2.0, limits.Left, 6);
            Assert.Equal(4.0, limits.Right, 6);
        });

        manager.ResetEmptyPlot();
        plots[0].Plot.Axes.SetLimitsX(-1.0, 1.0);
        plots[0].Plot.Axes.ApplyLinkedAxisRules();
        Assert.All(plots, plot =>
        {
            var limits = plot.Plot.Axes.GetLimits();
            Assert.Equal(-1.0, limits.Left, 6);
            Assert.Equal(1.0, limits.Right, 6);
        });
    }

    [Fact]
    public void Pane_assignments_and_heights_are_exposed_as_user_facing_state()
    {
        using var firstPlot = new FormsPlot();
        var manager = new PlotManager(firstPlot);
        manager.SetPaneCount(3);

        manager.SetChannelPane("RaceBox Speed", 2);
        manager.SetPaneHeights(new[] { 0.5, 0.3, 0.2 });

        Assert.Equal(2, manager.ChannelPanes["RaceBox Speed"]);
        Assert.Equal(new[] { 0.5, 0.3, 0.2 }, manager.PaneHeights);
    }

    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (Control descendant in Descendants(child))
                yield return descendant;
        }
    }
}
