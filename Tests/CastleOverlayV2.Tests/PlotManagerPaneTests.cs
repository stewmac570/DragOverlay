using CastleOverlayV2.Models;
using CastleOverlayV2.Plot;
using CastleOverlayV2.Services;
using CastleOverlayV2.Utils;
using ScottPlot.Plottables;
using ScottPlot.WinForms;
using System.Runtime.ExceptionServices;
using System.Windows.Forms;

namespace CastleOverlayV2.Tests;

public sealed class PlotManagerPaneTests
{
    [Fact]
    public void By_source_uses_first_visible_loaded_channel_as_each_pane_default()
    {
        using var firstPlot = new FormsPlot();
        var manager = new PlotManager(firstPlot);

        manager.ApplyBySourcePanes();
        LoadAllSources(manager);

        Assert.Equal("RPM", manager.GetFocusedChannel(1));
        Assert.Equal("RaceBox Speed", manager.GetFocusedChannel(2));
        Assert.Equal(MiniLoggerLoader.RpmChannel, manager.GetFocusedChannel(3));
    }

    [Fact]
    public void Every_populated_pane_renders_one_focused_y_scale()
    {
        using var firstPlot = new FormsPlot();
        var manager = new PlotManager(firstPlot);
        manager.ApplyBySourcePanes();
        LoadAllSources(manager);
        manager.PlotRuns(new Dictionary<int, RunData>(manager.Runs));

        FormsPlot[] plots = Descendants(manager.ChartHost).OfType<FormsPlot>().ToArray();
        Assert.Equal(3, plots.Length);
        Assert.All(plots, plot =>
        {
            int visibleScales = plot.Plot.Axes.GetAxes(ScottPlot.Edge.Left)
                .Concat(plot.Plot.Axes.GetAxes(ScottPlot.Edge.Right))
                .Count(axis => axis.Label.IsVisible && axis.TickLabelStyle.IsVisible);
            Assert.Equal(1, visibleScales);
        });
    }

    [Fact]
    public void Every_focused_channel_axis_is_inside_the_figure_without_moving_other_traces()
    {
        using var firstPlot = new FormsPlot();
        var manager = new PlotManager(firstPlot);
        LoadAllSources(manager);
        manager.PlotRuns(new Dictionary<int, RunData>(manager.Runs));

        FormsPlot plotControl = Assert.Single(Descendants(manager.ChartHost).OfType<FormsPlot>());
        string[] channels =
        {
            "RPM", "Throttle %", "Voltage", "Current", "Ripple", "PowerOut",
            "ESC Temp", "MotorTemp", "MotorTiming", "Acceleration",
            "RaceBox Speed", "RaceBox Distance", "RaceBox G-Force X",
            MiniLoggerLoader.ThrottleChannel, MiniLoggerLoader.RpmChannel,
            MiniLoggerLoader.WheelSpeedChannel
        };

        foreach (string channel in channels)
        {
            manager.SetFocusedChannel(channel);
            plotControl.Plot.RenderInMemory(1400, 400);

            ScottPlot.IAxis[] sideAxes = plotControl.Plot.Axes.GetAxes(ScottPlot.Edge.Left)
                .Concat(plotControl.Plot.Axes.GetAxes(ScottPlot.Edge.Right))
                .ToArray();
            ScottPlot.IAxis focusedAxis = Assert.Single(sideAxes,
                axis => axis.IsVisible && axis.Label.IsVisible && axis.TickLabelStyle.IsVisible);

            ScottPlot.Layout layout = plotControl.Plot.LastRender.Layout;
            ScottPlot.PixelRect panelRect = focusedAxis.GetPanelRect(
                layout.DataRect,
                layout.PanelSizes[focusedAxis],
                layout.PanelOffsets[focusedAxis]);

            Assert.InRange(panelRect.Left, layout.FigureRect.Left, layout.FigureRect.Right);
            Assert.InRange(panelRect.Right, layout.FigureRect.Left, layout.FigureRect.Right);
            Assert.InRange(panelRect.Top, layout.FigureRect.Top, layout.FigureRect.Bottom);
            Assert.InRange(panelRect.Bottom, layout.FigureRect.Top, layout.FigureRect.Bottom);
            if (focusedAxis.Edge == ScottPlot.Edge.Left)
                Assert.Equal(layout.DataRect.Left, panelRect.Right, 3);
            else
                Assert.Equal(layout.DataRect.Right, panelRect.Left, 3);
        }

        Scatter voltage = Assert.Single(plotControl.Plot.GetPlottables<Scatter>(),
            scatter => scatter.LegendText == "Voltage");
        manager.SetFocusedChannel("Voltage");
        plotControl.Plot.RenderInMemory(1400, 400);
        ScottPlot.Pixel voltagePixel = plotControl.Plot.GetPixel(
            new ScottPlot.Coordinates(0, 7), voltage.Axes.XAxis, voltage.Axes.YAxis);

        manager.SetFocusedChannel("RaceBox Speed");
        plotControl.Plot.RenderInMemory(1400, 400);
        ScottPlot.Pixel voltagePixelAfterFocusChange = plotControl.Plot.GetPixel(
            new ScottPlot.Coordinates(0, 7), voltage.Axes.XAxis, voltage.Axes.YAxis);

        Assert.Equal(voltagePixel.X, voltagePixelAfterFocusChange.X, 3);
        Assert.Equal(voltagePixel.Y, voltagePixelAfterFocusChange.Y, 3);
    }

    [Fact]
    public void Focusing_a_channel_changes_only_its_own_pane()
    {
        using var firstPlot = new FormsPlot();
        var manager = new PlotManager(firstPlot);
        manager.ApplyBySourcePanes();
        LoadAllSources(manager);

        manager.SetFocusedChannel("Voltage");

        Assert.Equal("Voltage", manager.GetFocusedChannel(1));
        Assert.Equal("RaceBox Speed", manager.GetFocusedChannel(2));
        Assert.Equal(MiniLoggerLoader.RpmChannel, manager.GetFocusedChannel(3));

        manager.SetFocusedChannel("RaceBox Distance");

        Assert.Equal("Voltage", manager.GetFocusedChannel(1));
        Assert.Equal("RaceBox Distance", manager.GetFocusedChannel(2));
        Assert.Equal(MiniLoggerLoader.RpmChannel, manager.GetFocusedChannel(3));
    }

    [Fact]
    public void Moving_the_focused_channel_to_an_empty_pane_moves_its_focus()
    {
        using var firstPlot = new FormsPlot();
        var manager = new PlotManager(firstPlot);
        manager.SetPaneCount(2);
        manager.SetRun(1, CastleRun());
        manager.SetFocusedChannel("Voltage");

        manager.SetChannelPane("Voltage", 2);

        Assert.Equal("RPM", manager.GetFocusedChannel(1));
        Assert.Equal("Voltage", manager.GetFocusedChannel(2));
    }

    [Fact]
    public void Hiding_a_panes_focused_channel_falls_back_in_channel_order()
    {
        using var firstPlot = new FormsPlot();
        var manager = new PlotManager(firstPlot);
        manager.SetRun(1, CastleRun());
        manager.SetFocusedChannel("Voltage");

        manager.SetChannelVisibility("Voltage", false);

        Assert.Equal("RPM", manager.GetFocusedChannel(1));
    }

    [Fact]
    public void Pane_state_without_saved_focus_uses_defaults_after_data_loads()
    {
        using var firstPlot = new FormsPlot();
        var manager = new PlotManager(firstPlot);

        manager.ApplyPaneState(
            3,
            BySourceAssignments(),
            new[] { 1.0 / 3, 1.0 / 3, 1.0 / 3 });
        LoadAllSources(manager);

        Assert.Equal("RPM", manager.GetFocusedChannel(1));
        Assert.Equal("RaceBox Speed", manager.GetFocusedChannel(2));
        Assert.Equal(MiniLoggerLoader.RpmChannel, manager.GetFocusedChannel(3));
    }

    [Fact]
    public void Saved_pane_focus_waits_for_its_data_then_restores()
    {
        using var firstPlot = new FormsPlot();
        var manager = new PlotManager(firstPlot);
        var savedFocus = new Dictionary<int, string>
        {
            [1] = "Voltage",
            [2] = "RaceBox Distance",
            [3] = MiniLoggerLoader.WheelSpeedChannel
        };

        manager.ApplyPaneState(
            3,
            BySourceAssignments(),
            new[] { 1.0 / 3, 1.0 / 3, 1.0 / 3 },
            savedFocus);

        Assert.Empty(manager.PaneFocusedChannels);
        Assert.Equal(savedFocus, manager.SavedPaneFocusedChannels);

        LoadAllSources(manager);

        Assert.Equal("Voltage", manager.GetFocusedChannel(1));
        Assert.Equal("RaceBox Distance", manager.GetFocusedChannel(2));
        Assert.Equal(MiniLoggerLoader.WheelSpeedChannel, manager.GetFocusedChannel(3));
    }

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

    private static void LoadAllSources(PlotManager manager)
    {
        manager.SetRun(1, CastleRun());
        manager.SetRun(RunSlots.Gps(1), ChannelRun(isMiniLogger: false));
        manager.SetRun(RunSlots.DataLog(1), ChannelRun(isMiniLogger: true));
    }

    private static RunData CastleRun() => new()
    {
        DataPoints = new List<DataPoint>
        {
            new() { Time = 0, Speed = 10 },
            new() { Time = 1, Speed = 20 }
        }
    };

    private static RunData ChannelRun(bool isMiniLogger)
    {
        var run = new RunData
        {
            IsRaceBox = !isMiniLogger,
            IsMiniLogger = isMiniLogger,
            DataPoints = new List<DataPoint>
            {
                new() { Time = 0 },
                new() { Time = 1 }
            }
        };
        IEnumerable<string> channels = isMiniLogger
            ? MiniLoggerLoader.Channels
            : new[] { "RaceBox Speed", "RaceBox Distance", "RaceBox G-Force X" };
        foreach (string channel in channels)
        {
            run.Data[channel] = new List<DataPoint>
            {
                new() { Time = 0, Y = 1 },
                new() { Time = 1, Y = 2 }
            };
        }
        return run;
    }

    private static Dictionary<string, int> BySourceAssignments() =>
        ChannelText.Order.ToDictionary(
            channel => channel,
            channel => channel.StartsWith("RaceBox ", StringComparison.OrdinalIgnoreCase) ? 2
                : channel.StartsWith("Logger ", StringComparison.OrdinalIgnoreCase) ? 3
                : 1,
            StringComparer.OrdinalIgnoreCase);
}
