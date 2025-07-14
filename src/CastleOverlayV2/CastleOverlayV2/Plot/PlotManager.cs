using CastleOverlayV2.Models;
using CastleOverlayV2.Utils;
using ScottPlot;
using ScottPlot.Plottables;
using ScottPlot.WinForms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace CastleOverlayV2.Plot
{
    /// <summary>
    /// Manages the ScottPlot chart for displaying multiple Castle runs.
    /// Supports up to 3 runs with Castle colors, line styles, legend, and hover cursor.
    /// </summary>
    public class PlotManager
    {
        private readonly FormsPlot _plot;

        // ✅ The vertical hover cursor line
        private VerticalLine _cursor;

        // ✅ Holds all scatter plottables for multi-log overlay
        private readonly List<Scatter> _scatters = new();


        // ✅ NEW: Emit hover data for toggle bar
        public event Action<Dictionary<string, double?[]>> CursorMoved;

        public PlotManager(FormsPlot plotControl)
        {
            _plot = plotControl ?? throw new ArgumentNullException(nameof(plotControl));
            SetupPlotDefaults();

            // ✅ Wire mouse move ONCE
            _plot.MouseMove += FormsPlot_MouseMove;
        }

        /// <summary>
        /// Plot multiple runs with Castle colors and line styles.
        /// </summary>
        public void PlotRuns(List<RunData> runs)
        {
            if (runs == null || runs.Count == 0)
                throw new ArgumentException("No runs to plot.");

            Console.WriteLine($"=== PlotRuns: {runs.Count} runs ===");

            _plot.Plot.Clear();
            _scatters.Clear();

            // ✅ Always recreate the vertical line after clearing
            _cursor = _plot.Plot.Add.VerticalLine(0);
            _cursor.LinePattern = ScottPlot.LinePattern.Dashed;
            _cursor.Color = ScottPlot.Colors.Black;

            for (int i = 0; i < runs.Count; i++)
            {
                var run = runs[i];
                if (run == null || run.DataPoints.Count == 0)
                    continue;

                double[] xs = run.DataPoints.Select(dp => dp.Time).ToArray();

                foreach (var (channelLabel, ys) in GetChannels(run))
                {
                    Scatter scatter = _plot.Plot.Add.Scatter(xs, ys);

                    scatter.Label = channelLabel; // ✅ Must match toggle bar channel name!
                    scatter.Color = ColorMap.GetColor(i);
                    scatter.LinePattern = LineStyleHelper.GetLinePattern(i);

                    _scatters.Add(scatter);
                }
            }

            _plot.Plot.Axes.AutoScale();
            _plot.Plot.XLabel("Time (s)");
            _plot.Plot.Title("Castle Log Overlay — Multi-Run");

            _plot.Plot.Axes.Left.TickGenerator = new ScottPlot.TickGenerators.NumericFixedInterval(1.0)
            {
                LabelFormatter = _ => ""
            };
            _plot.Plot.Axes.Left.Label.Text = "";

            _plot.Plot.ShowLegend();
            _plot.Plot.Legend.Location = Alignment.UpperRight;

            _plot.Refresh();
        }

        /// <summary>
        /// Phase 1 fallback: plot a single run.
        /// </summary>
        public void LoadRun(RunData run)
        {
            if (run == null)
                throw new ArgumentNullException(nameof(run));

            PlotRuns(new List<RunData> { run });
        }

        /// <summary>
        /// Handles mouse move to keep vertical hover line following X.
        /// Raises hover values for ChannelToggleBar.
        /// </summary>
        private void FormsPlot_MouseMove(object sender, MouseEventArgs e)
        {
            if (_cursor == null)
                return;

            var mousePixel = new Pixel(e.X, e.Y);
            var mouseCoord = _plot.Plot.GetCoordinates(mousePixel);

            _cursor.X = mouseCoord.X;

            var valuesAtCursor = new Dictionary<string, double?[]>();

            var channels = _scatters
                .GroupBy(s => s.Label)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var kvp in channels)
            {
                string channelName = kvp.Key;
                List<Scatter> scatters = kvp.Value;

                var channelValues = new double?[3];

                for (int i = 0; i < scatters.Count; i++)
                {
                    var scatter = scatters[i];

                    if (!scatter.IsVisible)
                        continue;

                    // ✅ The real way: use GetScatterPoints()
                    var points = scatter.Data.GetScatterPoints();

                    if (points == null || points.Count == 0)
                        continue;

                    // Find the closest point by X
                    var nearest = points.OrderBy(p => Math.Abs(p.X - mouseCoord.X)).First();
                    channelValues[i] = nearest.Y;
                }

                valuesAtCursor[channelName] = channelValues;
            }

            CursorMoved?.Invoke(valuesAtCursor);
            _plot.Refresh();
        }






        /// <summary>
        /// Apply basic default plot settings.
        /// </summary>
        private void SetupPlotDefaults()
        {
            _plot.Plot.Title("Castle Log Overlay Tool");
            _plot.Plot.XLabel("Time (s)");
        }

        /// <summary>
        /// Returns all Castle channels for a single run, normalized.
        /// </summary>
        private IEnumerable<(string Label, double[] Ys)> GetChannels(RunData run)
        {
            yield return ("Speed", Normalize(run.DataPoints.Select(dp => dp.Speed)));
            yield return ("Throttle", Normalize(run.DataPoints.Select(dp => dp.Throttle)));
            yield return ("Voltage", Normalize(run.DataPoints.Select(dp => dp.Voltage)));
            yield return ("Current", Normalize(run.DataPoints.Select(dp => dp.Current)));
            yield return ("Ripple", Normalize(run.DataPoints.Select(dp => dp.Ripple)));
            yield return ("PowerOut", Normalize(run.DataPoints.Select(dp => dp.PowerOut)));
            yield return ("MotorTemp", Normalize(run.DataPoints.Select(dp => dp.MotorTemp)));
            yield return ("MotorTiming", Normalize(run.DataPoints.Select(dp => dp.MotorTiming)));
            yield return ("Acceleration", Normalize(run.DataPoints.Select(dp => dp.Acceleration)));
            yield return ("GovGain", Normalize(run.DataPoints.Select(dp => dp.GovGain)));
        }

        /// <summary>
        /// Normalize channel values to [0, 1] for clean scaling.
        /// </summary>
        private double[] Normalize(IEnumerable<double> values)
        {
            double max = values.Max();
            return max == 0 ? values.ToArray() : values.Select(v => v / max).ToArray();
        }

        /// <summary>
        /// ✅ Toggle a channel’s visibility.
        /// </summary>
        public void SetChannelVisibility(string channelName, bool isVisible)
        {
            foreach (var scatter in _scatters)
            {
                if (scatter.Label == channelName)
                {
                    scatter.IsVisible = isVisible;
                }
            }
        }

        /// <summary>
        /// ✅ Refresh the plot.
        /// </summary>
        public void RefreshPlot()
        {
            _plot.Refresh();
        }
    }
}
