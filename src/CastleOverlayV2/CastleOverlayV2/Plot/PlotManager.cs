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
    /// Includes per-channel scaling factors for single-Y-axis tuning.
    /// </summary>
    public class PlotManager
    {
        private readonly FormsPlot _plot;

        // ✅ Hover cursor line
        private VerticalLine _cursor;

        // ✅ All scatters for multi-log overlay
        private readonly List<Scatter> _scatters = new();

        // ✅ Emit hover data for toggle bar
        public event Action<Dictionary<string, double?[]>> CursorMoved;

        // ✅ Per-channel scale factors (Phase 5.1 — hardcoded)
        private readonly Dictionary<string, double> _channelScales = new()
        {
            ["Speed"] = 1.0,
            ["Throttle"] = 1.0,
            ["Voltage"] = 0.1,
            ["Current"] = 0.5,
            ["Ripple"] = 1.0,
            ["PowerOut"] = 0.5,
            ["MotorTemp"] = 0.2,
            ["MotorTiming"] = 1.0,
            ["Acceleration"] = 2.0,
            ["GovGain"] = 1.0
        };

        public PlotManager(FormsPlot plotControl)
        {
            _plot = plotControl ?? throw new ArgumentNullException(nameof(plotControl));
            SetupPlotDefaults();

            // ✅ Wire mouse move ONCE
            _plot.MouseMove += FormsPlot_MouseMove;
        }

        /// <summary>
        /// Plot multiple runs with Castle colors, line styles, and per-channel scaling.
        /// </summary>
        public void PlotRuns(List<RunData> runs)
        {
            if (runs == null || runs.Count == 0)
                throw new ArgumentException("No runs to plot.");

            Console.WriteLine($"=== PlotRuns: {runs.Count} runs ===");

            _plot.Plot.Clear();
            _scatters.Clear();

            // ✅ Always recreate hover line
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
                    scatter.Label = channelLabel; // Must match toggle bar name
                    scatter.Color = ColorMap.GetColor(i); // Run-based colors
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
        /// Single run fallback.
        /// </summary>
        public void LoadRun(RunData run)
        {
            if (run == null)
                throw new ArgumentNullException(nameof(run));

            PlotRuns(new List<RunData> { run });
        }

        /// <summary>
        /// Keep hover line synced with X and emit cursor data.
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

                    var points = scatter.Data.GetScatterPoints();
                    if (points == null || points.Count == 0)
                        continue;

                    var nearest = points.OrderBy(p => Math.Abs(p.X - mouseCoord.X)).First();
                    channelValues[i] = nearest.Y;
                }

                valuesAtCursor[channelName] = channelValues;
            }

            CursorMoved?.Invoke(valuesAtCursor);
            _plot.Refresh();
        }

        /// <summary>
        /// Apply default plot labels.
        /// </summary>
        private void SetupPlotDefaults()
        {
            _plot.Plot.Title("Castle Log Overlay Tool");
            _plot.Plot.XLabel("Time (s)");
        }

        /// <summary>
        /// Return all channels for a run, scaled per factor.
        /// </summary>
        private IEnumerable<(string Label, double[] Ys)> GetChannels(RunData run)
        {
            yield return ("Speed", ScaleChannel(run.DataPoints.Select(dp => dp.Speed), _channelScales["Speed"]));
            yield return ("Throttle", ScaleChannel(run.DataPoints.Select(dp => dp.Throttle), _channelScales["Throttle"]));
            yield return ("Voltage", ScaleChannel(run.DataPoints.Select(dp => dp.Voltage), _channelScales["Voltage"]));
            yield return ("Current", ScaleChannel(run.DataPoints.Select(dp => dp.Current), _channelScales["Current"]));
            yield return ("Ripple", ScaleChannel(run.DataPoints.Select(dp => dp.Ripple), _channelScales["Ripple"]));
            yield return ("PowerOut", ScaleChannel(run.DataPoints.Select(dp => dp.PowerOut), _channelScales["PowerOut"]));
            yield return ("MotorTemp", ScaleChannel(run.DataPoints.Select(dp => dp.MotorTemp), _channelScales["MotorTemp"]));
            yield return ("MotorTiming", ScaleChannel(run.DataPoints.Select(dp => dp.MotorTiming), _channelScales["MotorTiming"]));
            yield return ("Acceleration", ScaleChannel(run.DataPoints.Select(dp => dp.Acceleration), _channelScales["Acceleration"]));
            yield return ("GovGain", ScaleChannel(run.DataPoints.Select(dp => dp.GovGain), _channelScales["GovGain"]));
        }

        /// <summary>
        /// Scale a channel’s values by factor.
        /// </summary>
        private double[] ScaleChannel(IEnumerable<double> values, double factor)
        {
            return values.Select(v => v * factor).ToArray();
        }

        /// <summary>
        /// Toggle channel visibility.
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
        /// Refresh plot.
        /// </summary>
        public void RefreshPlot()
        {
            _plot.Refresh();
        }

        /// <summary>
        /// Manually re-fit axes to visible data.
        /// </summary>
        public void FitToData()
        {
            _plot.Plot.Axes.AutoScale();
            _plot.Refresh();
        }
    }
}
