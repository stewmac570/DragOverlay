using CastleOverlayV2.Models;
using CastleOverlayV2.Services;
using CastleOverlayV2.Utils;
using ScottPlot;
using ScottPlot.AxisRules;
using ScottPlot.Plottables;
using ScottPlot.WinForms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Channels;
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

        // ✅ Holds raw Y-values for each Scatter to show true hover values
        private readonly Dictionary<Scatter, double[]> _rawYMap = new();

        // ✅ Maps each scatter to the RunData it belongs to
        private readonly Dictionary<Scatter, int> _scatterSlotMap = new();

        // ✅ Emit hover data for toggle bar
        public event Action<Dictionary<string, double?[]>> CursorMoved;

        // ✅ Track whether each run is visible
        // ✅ Track whether each run is visible
        private readonly Dictionary<int, bool> _runVisibility = new();


        // ✅ Store all loaded runs
        private readonly Dictionary<int, RunData> _runsBySlot = new();
        public IReadOnlyDictionary<int, RunData> Runs => _runsBySlot;

        private IAxis raceBoxSpeedAxis;
        private IAxis raceBoxGxAxis;



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
        public void PlotRuns(Dictionary<int, RunData> runsBySlot)
        {
            Logger.Log("📊 PlotRuns() — ENTERED");

            if (runsBySlot == null || runsBySlot.Count == 0)
            {
                Logger.Log("❌ PlotRuns() — runsBySlot is null or empty");
                return;
            }

            Logger.Log($"📊 PlotRuns() — Total loaded slots: {runsBySlot.Count}");

            foreach (var kvp in runsBySlot)
            {
                int slot = kvp.Key;
                RunData run = kvp.Value;

                if (run == null)
                {
                    Logger.Log($"⚠️  Slot {slot} → run is null");
                    continue;
                }

                int pointCount = run.DataPoints?.Count ?? 0;
                Logger.Log($"   • Slot {slot} — {pointCount} points");
            }

            Logger.Log("📊 PlotRuns() — Current channel visibility map:");
            foreach (var kvp in _channelVisibility)
            {
                Logger.Log($"   • {kvp.Key} = {(kvp.Value ? "Visible" : "Hidden")}");
            }

            // ✅ Reset visibility for each loaded run using run object as key
            foreach (var kvp in runsBySlot)
            {
                int slot = kvp.Key;
                RunData run = kvp.Value;

                if (run == null || (run.DataPoints.Count == 0 && !run.IsRaceBox))
                    continue;


                if (!_runVisibility.ContainsKey(slot))
                    _runVisibility[slot] = true;
            }

            LogVisibilityStates();




            // ✅ Force-enable RaceBox channels if not in visibility map yet
            _channelVisibility.TryAdd("RaceBox Speed", true);
            _channelVisibility.TryAdd("RaceBox G-Force X", true);

            Logger.Log("🔍 Current Run Visibility States:");
            foreach (var kvp in _runVisibility)
                Logger.Log($"   • Slot {kvp.Key}: {(kvp.Value ? "Visible" : "Hidden")}");

            Logger.Log("🔍 Current Channel Visibility States:");
            foreach (var kvp in _channelVisibility)
                Logger.Log($"   • Channel '{kvp.Key}': {(kvp.Value ? "Visible" : "Hidden")}");


            // === ✅ SAFETY CHECK ===
            if (runsBySlot == null || runsBySlot.Count == 0 ||
                runsBySlot.All(kvp =>
                    kvp.Value == null ||
                    (kvp.Value.DataPoints.Count == 0 && !kvp.Value.IsRaceBox)
                ))
            {
                ResetEmptyPlot();
                return;
            }


            if (runsBySlot == null || runsBySlot.Count == 0)
                throw new ArgumentException("No runs to plot.");

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
            // === Y AXIS: RaceBox Speed (mph) ===
            //-----------------------------------------------------------//
            // === Y AXIS: RaceBox Speed (mph) ===
            raceBoxSpeedAxis = _plot.Plot.Axes.AddRightAxis();
            raceBoxSpeedAxis.Label.Text = "Speed (mph)";
            _plot.Plot.Axes.Rules.Add(new LockedVertical((IYAxis)raceBoxSpeedAxis, 0, 150));
            raceBoxSpeedAxis.Label.IsVisible = false;
            raceBoxSpeedAxis.TickLabelStyle.IsVisible = false;
            raceBoxSpeedAxis.MajorTickStyle.Length = 0;
            raceBoxSpeedAxis.MinorTickStyle.Length = 0;
            raceBoxSpeedAxis.FrameLineStyle.Width = 0;

            // === Y AXIS: RaceBox G-Force X (g) ===
            raceBoxGxAxis = _plot.Plot.Axes.AddRightAxis();
            raceBoxGxAxis.Label.Text = "G-Force X (g)";
            _plot.Plot.Axes.Rules.Add(new LockedVertical((IYAxis)raceBoxGxAxis, -1, 5));
            raceBoxGxAxis.Label.IsVisible = false;
            raceBoxGxAxis.TickLabelStyle.IsVisible = false;
            raceBoxGxAxis.MajorTickStyle.Length = 0;
            raceBoxGxAxis.MinorTickStyle.Length = 0;
            raceBoxGxAxis.FrameLineStyle.Width = 0;




            //-----------------------------------------------------------//

            // === ✅ PLOT ALL RUNS ===
            //-----------------------------------------------------------//
            // === ✅ PLOT EACH RUN ===
            // Loops through every loaded log (1 or more Castle runs)
            foreach (var kvp in runsBySlot)
            {
                int slot = kvp.Key;
                RunData run = kvp.Value;

                if (!_runVisibility.ContainsKey(slot))
                    _runVisibility[slot] = true;



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



                    // === ✅ Create the scatter plot for this channel ===
                    Scatter scatter = _plot.Plot.Add.Scatter(xs, ysToPlot);
                    scatter.Label = channelLabel;
                    scatter.Color = ChannelColorMap.GetColor(channelLabel);
                    scatter.LinePattern = LineStyleHelper.GetLinePattern(slot - 1);
                    scatter.LineWidth = (float)LineStyleHelper.GetLineWidth(slot - 1);
                    scatter.Axes.XAxis = xAxis;
                    _scatterSlotMap[scatter] = slot;

                    bool isChannelVisible = _channelVisibility.TryGetValue(channelLabel, out var chanVis) ? chanVis : true;
                    bool isRunVisible = _runVisibility.TryGetValue(slot, out var runVis) ? runVis : true;

                    //scatter.IsVisible = isChannelVisible && isRunVisible;


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
                    bool runVisTemp = _runVisibility.TryGetValue(slot, out var temp) ? temp : true;

                    scatter.IsVisible = channelOn; // Ignore run visibility for now

                    scatter.IsVisible = channelOn && runVisTemp;

                    Logger.Log($"Plotting scatter: Run Slot {slot}, Channel '{channelLabel}', Visible={scatter.IsVisible}");




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

            foreach (var raceboxEntry in runsBySlot)
            {
                Logger.Log($"🔍 Entering RaceBox plot loop — Slot {raceboxEntry.Key}");

                int raceboxSlot = raceboxEntry.Key;
                RunData raceboxRun = raceboxEntry.Value;

                if (raceboxRun == null || !raceboxRun.IsRaceBox)
                    continue;

                Logger.Log($"✅ RaceBox run confirmed for slot {raceboxSlot}");


                var raceBoxChannels = new[] { "RaceBox Speed", "RaceBox G-Force X" };

                foreach (var rbChannel in raceBoxChannels)
                {
                    if (!raceboxRun.Data.TryGetValue(rbChannel, out var rbPoints) || rbPoints.Count == 0)
                    {
                        Logger.Log($"❌ No data found for {rbChannel}");
                        continue;
                    }

                    Logger.Log($"🔢 Raw point type = {rbPoints.FirstOrDefault()?.GetType().Name}, Count = {rbPoints.Count}");

                    var rbTyped = rbPoints.OfType<CastleOverlayV2.Models.DataPoint>().ToList();

                    if (rbTyped.Count == 0)
                    {
                        Logger.Log($"❌ RaceBox channel '{rbChannel}' has no valid DataPoints");
                        continue;
                    }


                    double[] xsRb = rbTyped.Select(p => p.Time).ToArray();
                    double[] ysRb = rbChannel switch
                    {
                        "RaceBox Speed" => rbTyped.Select(p => p.Y).ToArray(),
                        "RaceBox G-Force X" => rbTyped.Select(p => p.Y).ToArray(),
                        _ => Enumerable.Repeat(0.0, rbTyped.Count).ToArray()
                    };


                    Logger.Log($"✅ Sample Y values for {rbChannel}: {string.Join(", ", ysRb.Take(5))}");


                    var rbScatter = _plot.Plot.Add.Scatter(xsRb, ysRb);
                    Logger.Log($"[RaceBox] First 5 Y values for '{rbChannel}': {string.Join(", ", ysRb.Take(5))}");
                    Logger.Log($"[RaceBox] Min = {ysRb.Min()}, Max = {ysRb.Max()}");


                    rbScatter.Label = rbChannel;
                    rbScatter.Color = ChannelColorMap.GetColor(rbChannel);
                    rbScatter.LinePattern = LineStyleHelper.GetLinePattern(raceboxSlot - 1);
                    rbScatter.LineWidth = (float)LineStyleHelper.GetLineWidth(raceboxSlot - 1);
                    rbScatter.Axes.XAxis = xAxis;

                    bool isRbChannelVisible = _channelVisibility.TryGetValue(rbChannel, out var visible) && visible;
                    bool isRbRunVisible = _runVisibility.TryGetValue(raceboxSlot, out var slotVisible) && slotVisible;


                    rbScatter.IsVisible = isRbChannelVisible && isRbRunVisible;

                    Logger.Log($"📊 Plotting '{rbChannel}' → Run {raceboxSlot} → ChannelVisible={isRbChannelVisible}, RunVisible={isRbRunVisible}");
                    Logger.Log($"    → Xs: {xsRb.Length} pts, Ys: {ysRb.Length} pts");

                    // === Y-Axis Assignment (cast as needed for YAxis) ===
                    if (rbChannel == "RaceBox Speed")
                        rbScatter.Axes.YAxis = (IYAxis)raceBoxSpeedAxis;
                    else if (rbChannel == "RaceBox G-Force X")
                        rbScatter.Axes.YAxis = (IYAxis)raceBoxGxAxis;
                    else
                        rbScatter.Axes.YAxis = (IYAxis)throttleAxis;

                    Logger.Log($"[RaceBox] Plotting: Slot {raceboxSlot}, Channel '{rbChannel}', Visible={rbScatter.IsVisible}");

                    _scatters.Add(rbScatter);
                    _rawYMap[rbScatter] = ysRb;
                }
            }
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

            var singleSlot = new Dictionary<int, RunData> { [1] = run };
            PlotRuns(singleSlot);
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




        public void ToggleRunVisibility(int slot, bool isVisibleNow)
        {
            _runVisibility[slot] = isVisibleNow;

            foreach (var scatter in _scatters)
            {
                if (_scatterSlotMap.TryGetValue(scatter, out int scatterSlot) && scatterSlot == slot)
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
                if (scatter.Label == channelName && _scatterSlotMap.TryGetValue(scatter, out int slot))
                {
                    bool runVisible = _runVisibility.TryGetValue(slot, out bool runVis) ? runVis : true;
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
            Logger.Log("🟢 SetInitialChannelVisibility() called:");
            foreach (var kvp in visibilityMap)
            {
                Logger.Log($"   • {kvp.Key} = {(kvp.Value ? "Visible" : "Hidden")}");
            }

            _channelVisibility = visibilityMap ?? new();
        }




        public bool ToggleRunVisibility(int slot)
        {
            bool currentlyVisible = _runVisibility.TryGetValue(slot, out bool isVisible) ? isVisible : true;
            bool newState = !currentlyVisible;
            _runVisibility[slot] = newState;

            foreach (var scatter in _scatters)
            {
                if (_scatterSlotMap.TryGetValue(scatter, out int scatterSlot) && scatterSlot == slot)
                {
                    string channel = scatter.Label;
                    bool channelOn = _channelVisibility.TryGetValue(channel, out bool vis) ? vis : true;
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
        public bool GetRunVisibility(int slot)
        {
            return _runVisibility.TryGetValue(slot, out bool vis) ? vis : true;
        }

        public void LogVisibilityStates()
        {
            Logger.Log("🔍 Current Run Visibility States:");
            foreach (var kvp in _runVisibility)
            {
                Logger.Log($"   Run Slot {kvp.Key}: {(kvp.Value ? "Visible" : "Hidden")}");
            }

            Logger.Log("🔍 Current Channel Visibility States:");
            foreach (var kvp in _channelVisibility)
            {
                Logger.Log($"   Channel '{kvp.Key}': {(kvp.Value ? "Visible" : "Hidden")}");
            }
        }

        public void SetRunVisibility(int slotIndex, bool isVisible)
        {
            _runVisibility[slotIndex] = isVisible;
            Logger.Log($"SetRunVisibility: slot {slotIndex} set to {(isVisible ? "Visible" : "Hidden")}");
        }

        public void SetRun(int slot, RunData run)
        {
            _runsBySlot[slot] = run;
        }
    }
}
