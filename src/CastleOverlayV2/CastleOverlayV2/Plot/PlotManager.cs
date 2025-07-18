using CastleOverlayV2.Models;
using CastleOverlayV2.Utils;
using ScottPlot;
using ScottPlot.AxisRules;
using ScottPlot.Colormaps;
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
        private IScatterSource xs;

        // ✅ All scatters for multi-log overlay
        private readonly List<Scatter> _scatters = new();

        // ✅ Holds raw Y-values for each Scatter to show true hover values
        private readonly Dictionary<Scatter, double[]> _rawYMap = new();


        // ✅ Emit hover data for toggle bar
        public event Action<Dictionary<string, double?[]>> CursorMoved;

        private Dictionary<Scatter, int> _scatterRunMap = new();

        //private Dictionary<int, bool> _runVisibility = new();

        // Track whether each run (0, 1, 2) is visible
        private readonly Dictionary<int, bool> _runVisibility = new();

        //Toggle visibility state for a specific run
       
        // ✅ Per-channel scale factors (Phase 5.1 — hardcoded)
        private readonly Dictionary<string, double> _channelScales = new()
        {
            ["RPM"] = 1.0,
            ["Throttle"] = 1.0,
            ["Voltage"] = 0.1,
            ["Current"] = 0.5,
            ["Ripple"] = 1.0,
            ["PowerOut"] = 0.5,
            ["MotorTemp"] = 0.2,
            ["MotorTiming"] = 1.0,
            ["Acceleration"] = 2.0,
            
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
            // === ✅ SAFETY CHECK ===
            if (runs == null || runs.Count == 0 || runs.All(r => r == null || r.DataPoints.Count == 0))
            {
                Console.WriteLine("No valid runs to plot. Resetting plot.");
                ResetEmptyPlot();
                return;
            }


            if (runs == null || runs.Count == 0)
                throw new ArgumentException("No runs to plot.");

            Console.WriteLine($"=== PlotRuns: {runs.Count} runs ===");

            // === ✅ CLEAN RESET ===
            _plot.Plot.Clear();
            _scatters.Clear();
            _rawYMap.Clear();
            _plot.Plot.Axes.Rules.Clear();

            // === ✅ ADD HOVER CURSOR ===
            _cursor = _plot.Plot.Add.VerticalLine(0);
            _cursor.LinePattern = ScottPlot.LinePattern.Dashed;
            _cursor.Color = ScottPlot.Colors.Black;

            // === ✅ X AXIS: Time ===
            var xAxis = _plot.Plot.Axes.Bottom;
            xAxis.Label.Text = "Time (s)";
            xAxis.TickGenerator = new ScottPlot.TickGenerators.NumericAutomatic(); // ✅ Use default generator





            //-----------------------------------------------------------//
            // === ✅ Y AXIS: Throttle ===
            var throttleAxis = _plot.Plot.Axes.Left;
            throttleAxis.Label.Text = "Throttle (ms)";
            var throttleRule = new LockedVertical(throttleAxis, 1.4, 2.0);
            _plot.Plot.Axes.Rules.Add(throttleRule);
            throttleAxis.TickLabelStyle.IsVisible = false;
            throttleAxis.Label.IsVisible = false;
            throttleAxis.MajorTickStyle.Length = 0;
            throttleAxis.MinorTickStyle.Length = 0;
            throttleAxis.FrameLineStyle.Width = 0;
            //-----------------------------------------------------------//

            // === ✅ Y AXIS: Speed (RPM) ===
            var rpmAxis = _plot.Plot.Axes.AddRightAxis();
            rpmAxis.LabelText = "RPM";
            double rpmMax = _isFourPoleMode ? 100000 : 200000;
            var rpmRule = new LockedVertical(rpmAxis, 0, rpmMax);

            _plot.Plot.Axes.Rules.Add(rpmRule);
            rpmAxis.TickLabelStyle.IsVisible = false;
            rpmAxis.Label.IsVisible = false;
            rpmAxis.MajorTickStyle.Length = 0;
            rpmAxis.MinorTickStyle.Length = 0;
            rpmAxis.FrameLineStyle.Width = 0;
            //-----------------------------------------------------------//

            // === ✅ Y AXIS: Voltage ===
            var voltageAxis = _plot.Plot.Axes.AddLeftAxis();
            voltageAxis.Label.Text = "Voltage (V)";
            var voltageRule = new LockedVertical(voltageAxis, 6.0, 9.0);
            _plot.Plot.Axes.Rules.Add(voltageRule);
            voltageAxis.TickLabelStyle.IsVisible = false;
            voltageAxis.Label.IsVisible = false;
            voltageAxis.MajorTickStyle.Length = 0;
            voltageAxis.MinorTickStyle.Length = 0;
            voltageAxis.FrameLineStyle.Width = 0;
            //-----------------------------------------------------------//

            // === ✅ Y AXIS: Current ===
            var currentAxis = _plot.Plot.Axes.AddRightAxis();
            currentAxis.LabelText = "Current (A)";
            var currentRule = new LockedVertical(currentAxis, 0, 800);
            _plot.Plot.Axes.Rules.Add(currentRule);
            currentAxis.TickLabelStyle.IsVisible = false;
            currentAxis.Label.IsVisible = false;
            currentAxis.MajorTickStyle.Length = 0;
            currentAxis.MinorTickStyle.Length = 0;
            currentAxis.FrameLineStyle.Width = 0;
            //-----------------------------------------------------------//

            // === ✅ Y AXIS: Ripple ===
            var rippleAxis = _plot.Plot.Axes.AddRightAxis();
            rippleAxis.LabelText = "Ripple (V)";
            var rippleRule = new LockedVertical(rippleAxis, 0, 5.0);
            _plot.Plot.Axes.Rules.Add(rippleRule);
            rippleAxis.TickLabelStyle.IsVisible = false;
            rippleAxis.Label.IsVisible = false;
            rippleAxis.MajorTickStyle.Length = 0;
            rippleAxis.MinorTickStyle.Length = 0;
            rippleAxis.FrameLineStyle.Width = 0;
            //-----------------------------------------------------------//

            // === ✅ Y AXIS: PowerOut ===
            var powerAxis = _plot.Plot.Axes.AddLeftAxis();
            powerAxis.Label.Text = "Power Out (W)";
            var powerRule = new LockedVertical(powerAxis, 0, 110);
            _plot.Plot.Axes.Rules.Add(powerRule);
            powerAxis.TickLabelStyle.IsVisible = false;
            powerAxis.Label.IsVisible = false;
            powerAxis.MajorTickStyle.Length = 0;
            powerAxis.MinorTickStyle.Length = 0;
            powerAxis.FrameLineStyle.Width = 0;
            //-----------------------------------------------------------//

            // === ✅ Y AXIS: ESCTemp ===
            var escTempAxis = _plot.Plot.Axes.AddRightAxis();
            escTempAxis.LabelText = "ESC Temp (°C)";
            var escTempRule = new LockedVertical(escTempAxis, 20, 120);
            _plot.Plot.Axes.Rules.Add(escTempRule);
            escTempAxis.TickLabelStyle.IsVisible = false;
            escTempAxis.Label.IsVisible = false;
            escTempAxis.MajorTickStyle.Length = 0;
            escTempAxis.MinorTickStyle.Length = 0;
            escTempAxis.FrameLineStyle.Width = 0;

            //-----------------------------------------------------------//

            // === ✅ Y AXIS: MotorTemp ===
            var motorTempAxis = _plot.Plot.Axes.AddRightAxis();
            motorTempAxis.LabelText = "Motor Temp (°C)";
            var motorTempRule = new LockedVertical(motorTempAxis, 20, 120);
            _plot.Plot.Axes.Rules.Add(motorTempRule);
            motorTempAxis.TickLabelStyle.IsVisible = false;
            motorTempAxis.Label.IsVisible = false;
            motorTempAxis.MajorTickStyle.Length = 0;
            motorTempAxis.MinorTickStyle.Length = 0;
            motorTempAxis.FrameLineStyle.Width = 0;
            //-----------------------------------------------------------//

            // === ✅ Y AXIS: MotorTiming ===
            var motorTimingAxis = _plot.Plot.Axes.AddRightAxis();
            motorTimingAxis.LabelText = "Motor Timing (deg)";
            var motorTimingRule = new LockedVertical(motorTimingAxis, 0, 120);
            _plot.Plot.Axes.Rules.Add(motorTimingRule);
            motorTimingAxis.TickLabelStyle.IsVisible = false;
            motorTimingAxis.Label.IsVisible = false;
            motorTimingAxis.MajorTickStyle.Length = 0;
            motorTimingAxis.MinorTickStyle.Length = 0;
            motorTimingAxis.FrameLineStyle.Width = 0;
            //-----------------------------------------------------------//

            // === ✅ Y AXIS: Acceleration ===
            var accelAxis = _plot.Plot.Axes.AddRightAxis();
            accelAxis.LabelText = "Acceleration (g)";
            var accelRule = new LockedVertical(accelAxis, -5, 10);
            _plot.Plot.Axes.Rules.Add(accelRule);
            accelAxis.TickLabelStyle.IsVisible = false;
            accelAxis.Label.IsVisible = false;
            accelAxis.MajorTickStyle.Length = 0;
            accelAxis.MinorTickStyle.Length = 0;
            accelAxis.FrameLineStyle.Width = 0;
            //-----------------------------------------------------------//

  
            // === ✅ PLOT ALL RUNS ===
            //-----------------------------------------------------------//
            // === ✅ PLOT EACH RUN ===
            // Loops through every loaded log (1 or more Castle runs)
            for (int i = 0; i < runs.Count; i++)
            {

                if (!_runVisibility.ContainsKey(i))
                    _runVisibility[i] = true;

                var run = runs[i];
                if (run == null || run.DataPoints.Count == 0)
                    continue;

                //-----------------------------------------------------------//
                // === ✅ Extract X values (Time) for this run ===
                double[] xs = run.DataPoints.Select(dp => dp.Time).ToArray();

                //-----------------------------------------------------------//
                // === ✅ Loop through all channels (Throttle, RPM, Voltage, etc.) ===
                foreach (var (channelLabel, rawYs, scaledYs) in GetChannelsWithRaw(run))
                {
                    //-----------------------------------------------------------//
                    // === ✅ Always use the raw, real-unit data ===
                    // This keeps your overlay in real Castle Link units (RPM, volts, ms)
                    double[] ysToPlot = rawYs;

                    if (_isFourPoleMode && channelLabel == "RPM")
                        ysToPlot = rawYs.Select(v => v * 0.5).ToArray();


                    //-----------------------------------------------------------//
                    // === ✅ Create the scatter plot for this channel ===
                    Scatter scatter = _plot.Plot.Add.Scatter(xs, ysToPlot);
                    scatter.Label = channelLabel;                   // label must match your toggle bar
                    scatter.Color = ChannelColorMap.GetColor(channelLabel);           // color for this run
                    scatter.LinePattern = LineStyleHelper.GetLinePattern(i); // line style for this run
                    scatter.LineWidth = (float)LineStyleHelper.GetLineWidth(i); // ⬅️ Set per-log thickness
                    scatter.Axes.XAxis = xAxis;                     // always map to the Time axis
                    _scatterRunMap[scatter] = i; // ✅ manually track run index

                    bool isChannelVisible = _channelVisibility.TryGetValue(channelLabel, out var chanVis) ? chanVis : true;
                    bool isRunVisible = _runVisibility.TryGetValue(i, out var runVis) ? runVis : true;
                    scatter.IsVisible = isChannelVisible && isRunVisible;



                    //-----------------------------------------------------------//
                    // === ✅ Map this channel to its correct hidden or visible Y-axis ===
                    // This keeps each channel using its own true-unit scale.
                    if (channelLabel == "RPM") scatter.Axes.YAxis = rpmAxis;
                    else if (channelLabel == "Throttle") scatter.Axes.YAxis = throttleAxis;
                    else if (channelLabel == "Voltage") scatter.Axes.YAxis = voltageAxis;
                    else if (channelLabel == "Current") scatter.Axes.YAxis = currentAxis;
                    else if (channelLabel == "Ripple") scatter.Axes.YAxis = rippleAxis;
                    else if (channelLabel == "PowerOut") scatter.Axes.YAxis = powerAxis;
                    else if (channelLabel == "ESC Temp") scatter.Axes.YAxis = escTempAxis;
                    else if (channelLabel == "MotorTemp") scatter.Axes.YAxis = motorTempAxis;
                    else if (channelLabel == "MotorTiming") scatter.Axes.YAxis = motorTimingAxis;
                    else if (channelLabel == "Acceleration") scatter.Axes.YAxis = accelAxis;
                    else scatter.Axes.YAxis = throttleAxis; // fallback

                    bool channelOn = _channelVisibility.TryGetValue(channelLabel, out var vis) ? vis : true;
                    bool runOn = _runVisibility.ContainsKey(i) ? _runVisibility[i] : true;
                    scatter.IsVisible = channelOn && runOn;



                    //-----------------------------------------------------------//
                    // === ✅ Add scatter to lists for hover and toggle bar ===
                    _scatters.Add(scatter);
                    _rawYMap[scatter] = rawYs;  // store true raw values for hover
                }
            }


            // === ✅ FINAL PLOT SETTINGS ===

            // Auto-scale axes based on plotted data and axis rules
            _plot.Plot.Axes.AutoScale(); // Respects LockedVertical and other axis rules

            // DO NOT hide all axes globally — individual axes already styled (ticks/labels hidden as needed)
            // foreach (var axis in _plot.Plot.Axes.GetAxes())
            //     axis.IsVisible = false;

            // Maintain consistent padding around the plot area
            PixelPadding padding = new(left: 40, right: 40, top: 10, bottom: 50);
            _plot.Plot.Layout.Fixed(padding);

            // Optionally hide the legend (unless needed later)
            _plot.Plot.Legend.IsVisible = false;

            // Refresh the plot with all changes
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

                    // Find nearest point by X
                    var nearest = points.OrderBy(p => Math.Abs(p.X - mouseCoord.X)).First();

                    // Find index of this point in the series

                    int index = -1;
                    for (int j = 0; j < points.Count; j++)
                    {
                        if (points[j].X == nearest.X)
                        {
                            index = j;
                            break;
                        }
                    }
                    // Use raw Y for hover
                    if (_rawYMap.TryGetValue(scatter, out var rawYs) && index >= 0 && index < rawYs.Length)
                    {
                        double value = rawYs[index];
                        channelValues[i] = (channelName == "RPM" && _isFourPoleMode) ? value * 0.5 : value;

                    }
                    else
                    {
                        channelValues[i] = null; // fallback
                    }

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
            // ✅ Set the title once
            _plot.Plot.Title("Castle Log Overlay Tool");

            // ✅ Give space for the title
            PixelPadding padding = new(left: 40, right: 40, top: 10, bottom: 50);
            _plot.Plot.Layout.Fixed(padding);

            // ✅ Add any other permanent styles here
            _plot.Plot.XLabel("Time (s)");
        }

        /// <summary>
        /// Return all channels for a run, scaled per factor.
        /// </summary>
        private IEnumerable<(string Label, double[] RawYs, double[] ScaledYs)> GetChannelsWithRaw(RunData run)
        {
            // Helper to get raw data from the log
            double[] GetRaw(Func<Models.DataPoint, double> selector)
                => run.DataPoints.Select(selector).ToArray();

            // === ✅ ALL CHANNELS: Use raw data only ===
            yield return ("RPM", GetRaw(dp => dp.Speed), GetRaw(dp => dp.Speed));
            yield return ("Throttle", GetRaw(dp => dp.Throttle), GetRaw(dp => dp.Throttle));
            yield return ("Voltage", GetRaw(dp => dp.Voltage), GetRaw(dp => dp.Voltage));
            yield return ("Current", GetRaw(dp => dp.Current), GetRaw(dp => dp.Current));
            yield return ("Ripple", GetRaw(dp => dp.Ripple), GetRaw(dp => dp.Ripple));
            yield return ("PowerOut", GetRaw(dp => dp.PowerOut), GetRaw(dp => dp.PowerOut));
            yield return ("ESC Temp", GetRaw(dp => dp.Temperature), GetRaw(dp => dp.Temperature));
            yield return ("MotorTemp", GetRaw(dp => dp.MotorTemp), GetRaw(dp => dp.MotorTemp));
            yield return ("MotorTiming", GetRaw(dp => dp.MotorTiming), GetRaw(dp => dp.MotorTiming));
            yield return ("Acceleration", GetRaw(dp => dp.Acceleration), GetRaw(dp => dp.Acceleration));
        }



        /// <summary>
        /// Scale a channel’s values by factor.
        /// </summary>
        private double[] ScaleChannel(IEnumerable<double> values, double factor)
        {
            return values.Select(v => v * factor).ToArray();
        }

        /// <summary>
        /// Normalize Castle Link PWM to deviation from neutral.
        /// 1.5 ms = 0; 2.0 ms = +1; 1.0 ms = -1.
        /// </summary>
        private double[] ScaleThrottleChannel(IEnumerable<double> values, double factor)
        {
            return values.Select(v => ((v - 1.5) / 0.5) * factor).ToArray();
        }

        public void ToggleRunVisibility(int runIndex, bool isVisibleNow)
        {
            _runVisibility[runIndex] = isVisibleNow;

            foreach (var scatter in _scatters)
            {
                if (_scatterRunMap.TryGetValue(scatter, out int tag) && tag == runIndex)
                {
                    string channel = scatter.Label;
                    bool channelOn = _channelVisibility.TryGetValue(channel, out bool vis) ? vis : true;
                    scatter.IsVisible = isVisibleNow && channelOn;
                }
            }

            _plot.Refresh();
        }

        /// <summary>
        /// Toggle channel visibility.
        /// </summary>
        public void SetChannelVisibility(string channelName, bool isVisible)
        {
            _channelVisibility[channelName] = isVisible;

            foreach (var scatter in _scatters)
            {
                if (scatter.Label == channelName && _scatterRunMap.TryGetValue(scatter, out int runIndex))
                {
                    bool runVisible = _runVisibility.ContainsKey(runIndex) ? _runVisibility[runIndex] : true;
                    scatter.IsVisible = runVisible && isVisible;
                }
            }

            _plot.Refresh();
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

        private Dictionary<string, bool> _channelVisibility = new();

        public void SetInitialChannelVisibility(Dictionary<string, bool> visibilityMap)
        {
            _channelVisibility = visibilityMap ?? new();
        }
        

        public bool ToggleRunVisibility(int runIndex)
        {
            bool currentlyVisible = _runVisibility.ContainsKey(runIndex) ? _runVisibility[runIndex] : true;
            bool newState = !currentlyVisible;
            _runVisibility[runIndex] = newState;

            foreach (var scatter in _scatters)
            {
                if (_scatterRunMap.TryGetValue(scatter, out int tag) && tag == runIndex)
                {
                    string channel = scatter.Label;
                    bool channelOn = _channelVisibility.TryGetValue(channel, out bool vis) ? vis : true;

                    // ✅ Only show if both channel and run are active
                    scatter.IsVisible = newState && channelOn;
                }
            }

            _plot.Refresh();
            return newState;
        }
        public void ResetEmptyPlot()
        {
            _plot.Plot.Clear();
            _plot.Plot.Axes.Rules.Clear();

            // ✅ Invisible dummy line to prevent auto-resize bugs
            var dummy = _plot.Plot.Add.Signal(new double[] { 0, 0 });
            dummy.Color = ScottPlot.Colors.Transparent;
            dummy.LineWidth = 0;

            // ✅ Maintain layout (title space)
            PixelPadding padding = new(left: 40, right: 40, top: 10, bottom: 50);
            _plot.Plot.Layout.Fixed(padding);

            // ✅ Any runtime-only settings
            _plot.Plot.Legend.IsVisible = false;

            _plot.Refresh();
        }

        private bool _isFourPoleMode = false;

        public void SetFourPoleMode(bool isFourPole)
        {
            _isFourPoleMode = isFourPole;
        }


    }
}
