// File: /src/Plot/PlotManager.cs

using CastleOverlayV2.Models;
using ScottPlot;
using ScottPlot.Colormaps;
using ScottPlot.Plottables;
using ScottPlot.WinForms;
using System;
using System.Diagnostics;
using System.Linq;

namespace CastleOverlayV2.Plot
{
    /// <summary>
    /// Manages the ScottPlot plot for displaying one or more Castle runs.
    /// Phase 1: POC — single run, single channel (RPM), hover cursor.
    /// </summary>
    public class PlotManager
    {
        private readonly FormsPlot _plot;  // ✅ From WinForms Quickstart
        private Scatter _scatter;       // ✅ From PlottableManagement
        private AxisLine _cursor;            // ✅ From AxisLineQuickstart

        public PlotManager(FormsPlot plotControl)
        {
            _plot = plotControl ?? throw new ArgumentNullException(nameof(plotControl));
            SetupPlotDefaults();
        }

        /// <summary>
        /// Load a single RunData and plot the RPM channel.
        /// </summary>
        public void LoadRun(RunData run)
        {
            if (run == null || run.DataPoints == null || run.DataPoints.Count == 0)
                throw new ArgumentException("RunData is empty or null.");

            // X-axis: Time
            double[] xs = run.DataPoints.Select(dp => dp.Time).ToArray();

            // Extract and normalize each channel
            double[] Norm(IEnumerable<double> values)
            {
                double max = values.Max();
                return max == 0 ? values.ToArray() : values.Select(v => v / max).ToArray();
            }

            var channels = new (string Label, double[] Values)[]
            {
        ("Speed", Norm(run.DataPoints.Select(dp => dp.Speed))),
        ("Throttle", Norm(run.DataPoints.Select(dp => dp.Throttle))),
        ("Voltage", Norm(run.DataPoints.Select(dp => dp.Voltage))),
        ("Current", Norm(run.DataPoints.Select(dp => dp.Current))),
        ("Ripple", Norm(run.DataPoints.Select(dp => dp.Ripple))),
        ("PowerOut", Norm(run.DataPoints.Select(dp => dp.PowerOut))),
        ("MotorTemp", Norm(run.DataPoints.Select(dp => dp.MotorTemp))),
        ("MotorTiming", Norm(run.DataPoints.Select(dp => dp.MotorTiming))),
        ("Acceleration", Norm(run.DataPoints.Select(dp => dp.Acceleration))),
        ("GovGain", Norm(run.DataPoints.Select(dp => dp.GovGain))),
            };

            Console.WriteLine($"First: xs[0]={xs[0]}, last={xs.Last()}");

            _plot.Plot.Clear();

            // Add each channel
            foreach (var (label, ys) in channels)
            {
                var scatter = _plot.Plot.Add.Scatter(xs, ys);
                scatter.Label = label;
            }

            // Auto-scale
            _plot.Plot.Axes.AutoScale();

            // Hide Y-axis numbers and label
            _plot.Plot.Axes.Left.TickGenerator = new ScottPlot.TickGenerators.NumericFixedInterval(1.0)
            {
                LabelFormatter = _ => ""
            };
            _plot.Plot.Axes.Left.Label.Text = "";

            // Keep X-axis label
            _plot.Plot.XLabel("Time (s)");

            // Add a vertical line cursor that follows the mouse X position
            var vLine = _plot.Plot.Add.VerticalLine(0);
            vLine.LinePattern = ScottPlot.LinePattern.Dashed;
            vLine.Color = new ScottPlot.Color(0, 0, 0); // RGB for black
     

            // Add a text label that will move with the line
            var vLabel = _plot.Plot.Add.Text("", 0, 0);
            vLabel.Alignment = Alignment.UpperLeft;
            vLabel.Color = new ScottPlot.Color(0, 0, 0);

            // Connect to mouse move
            _plot.MouseMove += (s, e) =>
            {
                Pixel mousePixel = new(e.X, e.Y);
                Coordinates mouseCoord = _plot.Plot.GetCoordinates(mousePixel);

                vLine.X = mouseCoord.X;

                // Optional: update a label if you have one
                // vLabel.Text = $"{mouseCoord.X:F2}";
                // vLabel.X = mouseCoord.X;

                _plot.Refresh();
            };



            _plot.Refresh();
        }


        /// <summary>
        /// Sets up a vertical hover cursor that tracks the mouse X coordinate.
        /// Castle Link 2 style: dashed vertical line.
        /// </summary>


        /// <summary>
        /// Apply basic default plot settings.
        /// </summary>
        private void SetupPlotDefaults()
        {
            _plot.Plot.Title("Castle Log Overlay Tool — Phase 1 POC");
            _plot.Plot.XLabel("Time (s)");
            //_plot.Plot.YLabel("RPM");
            //_plot.Plot.ShowLegend();

        }
    }
}
