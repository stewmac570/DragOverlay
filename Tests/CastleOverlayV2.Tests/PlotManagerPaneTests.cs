using CastleOverlayV2.Plot;
using ScottPlot.WinForms;
using System.Runtime.ExceptionServices;
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

    [Fact]
    public void Three_panes_apply_equal_and_saved_heights_after_layout_and_resize()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var form = new Form
                {
                    FormBorderStyle = FormBorderStyle.None,
                    ShowInTaskbar = false,
                    StartPosition = FormStartPosition.Manual,
                    Location = new Point(-32000, -32000),
                    ClientSize = new Size(1500, 900)
                };
                using var firstPlot = new FormsPlot();
                var manager = new PlotManager(firstPlot);
                int heightChangeEvents = 0;
                manager.PaneHeightsChanged += _ => heightChangeEvents++;
                manager.ChartHost.Dock = DockStyle.Fill;
                form.Controls.Add(manager.ChartHost);
                form.Show();

                manager.SetPaneCount(3);
                Application.DoEvents();
                AssertPaneShares(manager.ChartHost, new[] { 1.0 / 3, 1.0 / 3, 1.0 / 3 });

                manager.SetPaneHeights(new[] { 0.5, 0.3, 0.2 });
                form.ClientSize = new Size(1200, 720);
                Application.DoEvents();
                AssertPaneShares(manager.ChartHost, new[] { 0.5, 0.3, 0.2 });
                Assert.Equal(0, heightChangeEvents);
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static void AssertPaneShares(Control chartHost, IReadOnlyList<double> expected)
    {
        int[] heights = Descendants(chartHost).OfType<FormsPlot>()
            .Select(plot => plot.Height)
            .ToArray();
        Assert.Equal(expected.Count, heights.Length);

        double total = heights.Sum();
        for (int index = 0; index < heights.Length; index++)
            Assert.InRange(heights[index] / total, expected[index] - 0.01, expected[index] + 0.01);
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
