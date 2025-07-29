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
using System.Text;
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

        private readonly Dictionary<int, List<VerticalLine>> _splitLinesBySlot = new();



        // === Castle Axis References ===
        private IYAxis throttleAxis;
        private IYAxis rpmAxis;
        private IYAxis voltageAxis;
        private IYAxis currentAxis;
        private IYAxis rippleAxis;
        private IYAxis powerAxis;
        private IYAxis escTempAxis;
        private IYAxis motorTempAxis;
        private IYAxis motorTimingAxis;
        private IYAxis accelAxis;

        // === RaceBox Axis References ===
        private IYAxis raceBoxSpeedAxis;
        private IYAxis raceBoxGxAxis;




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



        //--------------------------------------------------------------------------------------------------//
        /// <summary>
        /// Plot multiple runs with Castle colors, line styles, and per-channel scaling.
        /// </summary>
        public void PlotRuns(Dictionary<int, RunData> runsBySlot)
        {

            // 🔄 Clear existing split lines (new structure per-slot)
            // 🔄 Clear visible split lines only — preserve hidden ones
            // 🔄 Only remove split lines for runs that are currently visible
            var toRemove = _splitLinesBySlot
                .Where(kvp => _runVisibility.TryGetValue(kvp.Key, out bool vis) && vis)
                .ToList();

            foreach (var kvp in toRemove)
            {
                foreach (var line in kvp.Value)
                    _plot.Plot.Remove(line);

                _splitLinesBySlot.Remove(kvp.Key);
            }




            Logger.Log("📊 PlotRuns() — ENTERED");

            if (runsBySlot == null || runsBySlot.Count == 0)
            {
                Logger.Log("❌ PlotRuns() — runsBySlot is null or empty");
                return;
            }

            Logger.Log($"📊 PlotRuns() — Total loaded slots: {runsBySlot.Count}");

            // === ✅ PLOT CASTLE RUNS ===
            // === ✅ PLOT EACH RUN (RaceBox vs Castle) ===
           

            Logger.Log("📊 PlotRuns() — Current channel visibility map:");
            foreach (var kvp in _channelVisibility)
            {
                Logger.Log($"   • {kvp.Key} = {(kvp.Value ? "Visible" : "Hidden")}");
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

            // 🔁 Ensure visibility defaults to true for all runs
            foreach (var slot in runsBySlot.Keys)
            {
                if (!_runVisibility.ContainsKey(slot))
                    _runVisibility[slot] = true;
            }

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
            SetupAllAxes();

            //-----------------------------------------------------------//

            // === ✅ PLOT ALL RUNS ===
            //-----------------------------------------------------------//
            // === ✅ PLOT EACH RUN ===
            // Loops through every loaded log (1 or more Castle runs)
            foreach (var kvp in runsBySlot)
            {
                int slot = kvp.Key;
                RunData run = kvp.Value;

                if (run == null)
                    continue;

                // ✅ Always ensure slot is in _runVisibility
                if (!_runVisibility.ContainsKey(slot))
                    _runVisibility[slot] = true;

                // ✅ Skip Castle block if RaceBox
                if (run.IsRaceBox)
                    continue;



                if (run.DataPoints.Count == 0)
                    continue;


            //-----------------------------------------------------------//
            // === ✅ Extract X values (Time) for this run ===
            double[] xs = run.DataPoints.Select(dp => dp.Time).ToArray();

                //-----------------------------------------------------------//
                // === ✅ Loop through all channels (Throttle, RPM, Voltage, etc.) ===
                foreach (var (channelLabel, rawYs, scaledYs) in GetChannelsWithRaw(run))
                {

                    if (channelLabel == "Motor Temp.")
                    {
                        Logger.Log($"🧪 Plotting Motor Temp — Count: {rawYs.Length}");
                        Logger.Log($"   Values: {string.Join(", ", rawYs.Take(5))}");
                    }

                    Logger.Log($"[Castle] Channel: {channelLabel}");
                    //-----------------------------------------------------------//
                    // === ✅ Always use the raw, real-unit data ===
                    // This keeps your overlay in real Castle Link units (RPM, volts, ms)
                    double[] ysToPlot = rawYs;

                    if (_isFourPoleMode && channelLabel == "RPM")
                        ysToPlot = rawYs.Select(v => v * 0.5).ToArray();



                    // === ✅ Create the scatter plot for this channel ===
                    //if (channelLabel == "Throttle") // 👈 Replace with the name of the blue line
                        //continue;
                    Scatter scatter = _plot.Plot.Add.Scatter(xs, ysToPlot);
                    scatter.Label = channelLabel;

                    if (channelLabel == "MotorTemp")
                    {
                        scatter.Color = ScottPlot.Colors.Magenta;
                        scatter.LineWidth = 6;
                    }

                    if (channelLabel == "Acceleration")
                    {
                        Logger.Log($"🧪 Plotting Acceleration — Count: {rawYs.Length}");
                        Logger.Log($"   Values: {string.Join(", ", rawYs.Take(5))}");
                    }


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

            foreach (var kvp in runsBySlot)
            {
                int slot = kvp.Key;
                RunData run = kvp.Value;

                if (run != null && run.IsRaceBox)
                {
                    Logger.Log($"📈 Always plotting RaceBox run in slot {slot} (regardless of visibility)...");
                    PlotRaceBoxRun(slot, run);

                    // Then control visibility AFTER they're added
                    bool isVisible = _runVisibility.TryGetValue(slot, out var vis) ? vis : true;

                    // Hide the lines if this run is hidden
                    if (!isVisible && _splitLinesBySlot.TryGetValue(slot, out var lines))
                    {
                        foreach (var line in lines)
                            line.IsVisible = false;
                    }

                    // Hide the scatters manually
                    foreach (var scatter in _scatters)
                    {
                        if (_scatterSlotMap.TryGetValue(scatter, out int scatterSlot) && scatterSlot == slot)
                        {
                            string label = scatter.Label;
                            bool channelOn = _channelVisibility.TryGetValue(label, out var chanVis) ? chanVis : true;
                            scatter.IsVisible = isVisible && channelOn;
                        }
                    }
                }


            }

            // === ✅ FINAL PLOT SETTINGS ===

            // Auto-scale axes based on plotted data and axis rules
            _plot.Plot.Axes.AutoScale(); // Respects LockedVertical and other axis rules

            // DO NOT hide all axes globally — individual axes already styled (ticks/labels hidden as needed)
            // foreach (var axis in _plot.Plot.Axes.GetAxes())
            //     axis.IsVisible = false;
            var yAxes = _plot.Plot.Axes.Left; // default primary axis
            Logger.Log($"📏 Y-Axis Range: {yAxes.Range.Min} → {yAxes.Range.Max}");

            Logger.Log("All visible plots:");
            foreach (var s in _scatters.Where(s => s.IsVisible))
                Logger.Log($"• {s.Label}, Points: {_rawYMap[s].Length}, Axis: {s.Axes.YAxis.Label.Text}");


            // Maintain consistent padding around the plot area
            PixelPadding padding = new(left: 40, right: 40, top: 10, bottom: 50);
            _plot.Plot.Layout.Fixed(padding);

            // Optionally hide the legend (unless needed later)
            _plot.Plot.Legend.IsVisible = false;

            // Refresh the plot with all changes
            _plot.Refresh();
         }
        //====================================================================================//
        private void SetupAllAxes()
        {
            _plot.Plot.Axes.Rules.Clear();

            // === Throttle Axis
            throttleAxis = _plot.Plot.Axes.Left;
            throttleAxis.Label.Text = "Throttle (ms)";
            _plot.Plot.Axes.Rules.Add(new LockedVertical(throttleAxis, 1.4, 2.0));
            HideAxis(throttleAxis);

            // === RPM Axis
            rpmAxis = _plot.Plot.Axes.AddRightAxis();
            rpmAxis.Label.Text = "RPM";
            double rpmMax = _isFourPoleMode ? 100000 : 200000;
            _plot.Plot.Axes.Rules.Add(new LockedVertical(rpmAxis, 0, rpmMax));
            HideAxis(rpmAxis);

            // === Voltage Axis
            voltageAxis = _plot.Plot.Axes.AddLeftAxis();
            voltageAxis.Label.Text = "Voltage (V)";
            _plot.Plot.Axes.Rules.Add(new LockedVertical(voltageAxis, 6.0, 9.0));
            HideAxis(voltageAxis);

            // === Current Axis
            currentAxis = _plot.Plot.Axes.AddRightAxis();
            currentAxis.Label.Text = "Current (A)";
            _plot.Plot.Axes.Rules.Add(new LockedVertical(currentAxis, 0, 800));
            HideAxis(currentAxis);

            // === Ripple Axis
            rippleAxis = _plot.Plot.Axes.AddRightAxis();
            rippleAxis.Label.Text = "Ripple (V)";
            _plot.Plot.Axes.Rules.Add(new LockedVertical(rippleAxis, 0, 5.0));
            HideAxis(rippleAxis);

            // === PowerOut Axis
            powerAxis = _plot.Plot.Axes.AddLeftAxis();
            powerAxis.Label.Text = "Power Out (W)";
            _plot.Plot.Axes.Rules.Add(new LockedVertical(powerAxis, 0, 110));
            HideAxis(powerAxis);

            // === ESC Temp Axis
            escTempAxis = _plot.Plot.Axes.AddRightAxis();
            escTempAxis.Label.Text = "ESC Temp (°C)";
            _plot.Plot.Axes.Rules.Add(new LockedVertical(escTempAxis, 20, 120));
            HideAxis(escTempAxis);

            // === Motor Temp Axis
            motorTempAxis = _plot.Plot.Axes.AddRightAxis();
            motorTempAxis.Label.Text = "Motor Temp (°C)";
            _plot.Plot.Axes.Rules.Add(new LockedVertical(motorTempAxis, 20, 120));
            HideAxis(motorTempAxis);

            // === Motor Timing Axis
            motorTimingAxis = _plot.Plot.Axes.AddRightAxis();
            motorTimingAxis.Label.Text = "Motor Timing (deg)";
            _plot.Plot.Axes.Rules.Add(new LockedVertical(motorTimingAxis, 0, 120));
            HideAxis(motorTimingAxis);

            // === Acceleration Axis
            accelAxis = _plot.Plot.Axes.AddRightAxis();
            accelAxis.Label.Text = "Acceleration (g)";
            _plot.Plot.Axes.Rules.Add(new LockedVertical(accelAxis, -5, 7));
            HideAxis(accelAxis);

            // === RaceBox Speed Axis
            raceBoxSpeedAxis = _plot.Plot.Axes.AddRightAxis();
            raceBoxSpeedAxis.Label.Text = "Speed (mph)";
            _plot.Plot.Axes.Rules.Add(new LockedVertical((IYAxis)raceBoxSpeedAxis, 0, 110));
            HideAxis(raceBoxSpeedAxis);

            // === RaceBox G-Force X Axis
            raceBoxGxAxis = _plot.Plot.Axes.AddRightAxis();
            raceBoxGxAxis.Label.Text = "G-Force X (g)";
            _plot.Plot.Axes.Rules.Add(new LockedVertical((IYAxis)raceBoxGxAxis, -5, 7));
            HideAxis(raceBoxGxAxis);
        }

        private void HideAxis(IAxis axis)
        {
            axis.Label.IsVisible = false;
            axis.TickLabelStyle.IsVisible = false;
            axis.MajorTickStyle.Length = 0;
            axis.MinorTickStyle.Length = 0;
            axis.FrameLineStyle.Width = 0;
        }

        //===============================================================================//
         private void PlotRaceBoxRun(int slot, RunData run)
        {
            Logger.Log($"PlotRaceBoxRun called for slot {slot}, run.SplitTimes count: {run.SplitTimes?.Count ?? 0}");

            if (run == null || !run.IsRaceBox)
                return;

            Logger.Log($"✅ RaceBox run confirmed for slot {slot}");

            var raceBoxChannels = new[] { "RaceBox Speed", "RaceBox G-Force X" };

            foreach (var rbChannel in raceBoxChannels)
            {
                if (!run.Data.TryGetValue(rbChannel, out var rbPoints) || rbPoints.Count == 0)
                {
                    Logger.Log($"❌ No data found for {rbChannel}");
                    continue;
                }

                var rbTyped = rbPoints.OfType<CastleOverlayV2.Models.DataPoint>().ToList();

                if (rbTyped.Count == 0)
                {
                    Logger.Log($"❌ RaceBox channel '{rbChannel}' has no valid DataPoints");
                    continue;
                }

                var validPoints = rbTyped.Where(p => Math.Abs(p.Y) > 0.01).ToList();
                if (validPoints.Count < 2)
                {
                    Logger.Log($"⚠️ Skipping '{rbChannel}' due to low Y-variation or too few points");
                    continue;
                }

                double[] xs = validPoints.Select(p => p.Time).ToArray();
                double[] ys = validPoints.Select(p => p.Y).ToArray();

                Logger.Log($"✅ Sample Y values for {rbChannel}: {string.Join(", ", ys.Take(5))}");

                var scatter = _plot.Plot.Add.Scatter(xs, ys);
                _scatterSlotMap[scatter] = slot;
                scatter.Label = rbChannel;
                scatter.Color = ChannelColorMap.GetColor(rbChannel);
                scatter.LinePattern = LineStyleHelper.GetLinePattern(slot - 1);
                scatter.LineWidth = (float)LineStyleHelper.GetLineWidth(slot - 1);
                scatter.Axes.XAxis = _plot.Plot.Axes.Bottom;

                bool isChannelVisible = _channelVisibility.TryGetValue(rbChannel, out var visible) && visible;
                bool isRunVisible = _runVisibility.TryGetValue(slot, out var slotVisible) && slotVisible;
                scatter.IsVisible = isChannelVisible && isRunVisible;

                // Assign correct Y-axis
                if (rbChannel == "RaceBox Speed")
                    scatter.Axes.YAxis = raceBoxSpeedAxis;
                else if (rbChannel == "RaceBox G-Force X")
                    scatter.Axes.YAxis = raceBoxGxAxis;
                else
                    scatter.Axes.YAxis = throttleAxis;

                Logger.Log($"[RaceBox] Plotting: Slot {slot}, Channel '{rbChannel}', Visible={scatter.IsVisible}");

                _scatters.Add(scatter);
                _rawYMap[scatter] = ys;
            }
            Logger.Log($"🧪 DEBUG: SplitTimes = {(run.SplitTimes == null ? "null" : string.Join(", ", run.SplitTimes))}");
            Logger.Log($"🧪 DEBUG: Count = {(run.SplitTimes?.Count ?? 0)}");
            Logger.Log($"Adding split lines for slot {slot}: {string.Join(", ", run.SplitTimes)}");

            // ✅ Add vertical split lines after all RaceBox channels are plotted
            if (run.SplitTimes != null && run.SplitTimes.Any())
            {
                Logger.Log($"📏 Drawing {run.SplitTimes.Count} RaceBox split lines...");
                AddRaceBoxSplitLines(slot, run.SplitTimes);

            }

        }

        //===============================================================================//

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

            if (run.DataPoints.Count > 0)
            {
                var temps = run.DataPoints.Select(dp => dp.MotorTemp).Take(10).ToArray();
                Logger.Log($"🧪 Sample MotorTemp values: {string.Join(", ", temps)}");
            }


            yield return ("MotorTemp", GetRaw(dp => dp.MotorTemp), GetRaw(dp => dp.MotorTemp));
            yield return ("MotorTiming", GetRaw(dp => dp.MotorTiming), GetRaw(dp => dp.MotorTiming));

            var accelValues = GetRaw(dp => dp.Acceleration);
            Logger.Log($"🧪 GetChannelsWithRaw(): Acceleration sample = {string.Join(", ", accelValues.Take(5))}");
            yield return ("Acceleration", accelValues, accelValues);

        }


        //=======================================================================

        public void ToggleRunVisibility(int slot, bool isVisibleNow)
        {
            _runVisibility[slot] = isVisibleNow;

            Logger.Log($"🔁 Toggled Run Visibility — Slot {slot}, NowVisible={isVisibleNow}");

            bool hasAnyScatters = _scatters.Any(s => _scatterSlotMap.TryGetValue(s, out int sSlot) && sSlot == slot);

            if (isVisibleNow && !hasAnyScatters)
            {
                Logger.Log($"♻️ No active plots for slot {slot}. Forcing replot.");

                if (_runsBySlot.TryGetValue(slot, out RunData run))
                {
                    if (run.IsRaceBox)
                    {
                        Logger.Log($"♻️ Replotting hidden RaceBox slot {slot}...");
                        PlotRaceBoxRun(slot, run);
                    }
                    else
                    {
                        PlotRuns(new Dictionary<int, RunData>(_runsBySlot));
                        return;
                    }
                }
            }

            // 🔄 Show/hide matching scatters
            foreach (var scatter in _scatters)
            {
                if (_scatterSlotMap.TryGetValue(scatter, out int scatterSlot) && scatterSlot == slot)
                {
                    string channel = scatter.Label;
                    bool channelOn = _channelVisibility.TryGetValue(channel, out bool vis) ? vis : true;
                    scatter.IsVisible = isVisibleNow && channelOn;
                }
            }

            // 🔄 Show/hide split lines
            if (_splitLinesBySlot.TryGetValue(slot, out var lines))
            {
                foreach (var line in lines)
                    line.IsVisible = isVisibleNow;
            }
            else if (isVisibleNow && _runsBySlot.TryGetValue(slot, out RunData run2) && run2.IsRaceBox && run2.SplitTimes?.Count > 0)
            {
                Logger.Log($"➕ Rebuilding missing split lines for slot {slot} after re-show");
                AddRaceBoxSplitLines(slot, run2.SplitTimes);
            }

            _plot.Refresh();
        }




        //========================================================================
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



        //==========================================================================
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

            if (_splitLinesBySlot.TryGetValue(slot, out var lines))
            {
                foreach (var line in lines)
                {
                    line.IsVisible = newState;
                }
            }

            _plot.Refresh();

            return newState; // <--- Make sure this is here and method ends with this return
        }




        //===============================================================================
        public void ResetEmptyPlot()
        {
            _plot.Plot.Clear();
            _plot.Plot.Axes.Rules.Clear();

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
            _runVisibility[slot] = run != null;

            if (run == null)
            {
                Logger.Log($"🗑️ Clearing all scatters for deleted run in slot {slot}");

                // Remove all plotted data lines for this slot
                var toRemove = _scatterSlotMap
                    .Where(kvp => kvp.Value == slot)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var scatter in toRemove)
                {
                    _plot.Plot.Remove(scatter);
                    _scatters.Remove(scatter);
                    _scatterSlotMap.Remove(scatter);
                    _rawYMap.Remove(scatter);
                }

                Logger.Log($"🧼 Removed {toRemove.Count} scatters from slot {slot}");

                // Remove RaceBox split lines for this slot if they exist
                if (_splitLinesBySlot.TryGetValue(slot, out var lines))
                {
                    Logger.Log($"🧼 Removing {lines.Count} RaceBox split lines from slot {slot}");
                    foreach (var line in lines)
                        _plot.Plot.Remove(line);

                    _splitLinesBySlot.Remove(slot);
                }
            }
        }


        private void AddRaceBoxSplitLines(int slot, List<double> splitTimes)
        {
            if (_splitLinesBySlot.ContainsKey(slot))
            {
                foreach (var line in _splitLinesBySlot[slot])
                    _plot.Plot.Remove(line);
                _splitLinesBySlot.Remove(slot);
            }

            var lines = new List<VerticalLine>();

            foreach (double t in splitTimes)
            {
                var vLine = _plot.Plot.Add.VerticalLine(t);
                vLine.LinePattern = LineStyleHelper.GetLinePattern(99);
                vLine.LineWidth = 1;
                vLine.Color = ScottPlot.Colors.Gray.WithAlpha(100);
                lines.Add(vLine);
            }

            _splitLinesBySlot[slot] = lines;
        }

    }
}
