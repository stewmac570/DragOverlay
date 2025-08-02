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
using System.Reflection.Emit;
using System.Text;
using System.Threading.Channels;
using System.Windows.Forms;
using ScottPlot.AxisPanels;

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
        // ✅ Tracks Text labels for each slot's split lines
        private readonly Dictionary<int, List<Text>> _splitLabelsBySlot = new();
               
        private ScottPlot.IYAxis? _splitLabelAxis;

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
            _plot.Plot.Clear();

            // ⛔ No title or legend
            _plot.Plot.Title(null);
            _plot.Plot.Legend.IsVisible = false;

            // ✅ Custom Tick Generator with both major and minor ticks
            var tickGen = new ScottPlot.TickGenerators.NumericManual();

            for (double pos = 0.0; pos <= 10.0; pos += 0.05)
                tickGen.AddMajor(pos, pos.ToString("0.00"));

            for (double pos = 0.005; pos <= 10.0; pos += 0.005)
                tickGen.AddMinor(pos);

            _plot.Plot.Axes.Bottom.TickGenerator = tickGen;

            // ✅ Grid line styling for both major and minor
            _plot.Plot.Grid.XAxisStyle.MajorLineStyle.Color = ScottPlot.Colors.Black.WithAlpha(40);
            _plot.Plot.Grid.XAxisStyle.MajorLineStyle.Width = 1;

            _plot.Plot.Grid.XAxisStyle.MinorLineStyle.Color = ScottPlot.Colors.Gray.WithAlpha(25);
            _plot.Plot.Grid.XAxisStyle.MinorLineStyle.Width = 0.5f;


            // ✅ Label settings
            _plot.Plot.Axes.Bottom.Label.Text = "Time (s)";
            _plot.Plot.Axes.Bottom.Label.FontSize = 12;

            _plot.Refresh();
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

            Logger.Log("📊 PlotRuns() — Current channel visibility map:");
            foreach (var kvp in _channelVisibility)
            {
                Logger.Log($"   • {kvp.Key} = {(kvp.Value ? "Visible" : "Hidden")}");
            }

            LogVisibilityStates();


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
            _splitLabelAxis = null;

            // === ✅ ADD HOVER CURSOR ===
            _cursor = _plot.Plot.Add.VerticalLine(0);
            _cursor.LinePattern = ScottPlot.LinePattern.Dashed;
            _cursor.Color = ScottPlot.Colors.Red;

            // === ✅ X AXIS: Time ===
            var xAxis = _plot.Plot.Axes.Bottom;

            // ✅ Show bottom axis visuals (for loaded logs)
            xAxis.Label.IsVisible = true;
            xAxis.TickLabelStyle.IsVisible = true;
            xAxis.MajorTickStyle.Length = 5;
            xAxis.MinorTickStyle.Length = 3;
            xAxis.FrameLineStyle.Width = 1;

            xAxis.Label.Text = "Time (s)";
            xAxis.TickGenerator = new ScottPlot.TickGenerators.NumericAutomatic(); // ✅ Use default generator


            //-----------------------------------------------------------//
            SetupAllAxes();

            // 🔒 prepare hidden axis for split-time labels once
            EnsureSplitLabelAxis();

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
                double[] xs = run.DataPoints.Select(dp => dp.Time + _castleTimeShift).ToArray();

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

                    // 🔁 Re-render now to ensure Y-axis has real bounds before adding labels
                    _plot.Refresh();

                    // ✅ Add split lines and labels after rendering
                    if (run.SplitTimes?.Count > 0)
                        AddRaceBoxSplitLines(slot, run.SplitTimes, run.SplitLabels, includeZero: true);

                    // 🔁 Then control visibility AFTER they're added
                    bool isVisible = _runVisibility.TryGetValue(slot, out var vis) ? vis : true;

                    // Hide split lines if run is hidden
                    if (!isVisible && _splitLinesBySlot.TryGetValue(slot, out var lines))
                    {
                        foreach (var line in lines)
                            line.IsVisible = false;
                    }

                    // Hide split labels if run is hidden
                    if (!isVisible && _splitLabelsBySlot.TryGetValue(slot, out var lbls))
                        {
                        foreach (var lbl in lbls)
                        lbl.IsVisible = false;
                        }


                    // Hide scatters if run is hidden
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


            var yAxes = _plot.Plot.Axes.Left; // default primary axis
            Logger.Log($"📏 Y-Axis Range: {yAxes.Range.Min} → {yAxes.Range.Max}");

            Logger.Log("All visible plots:");
            foreach (var s in _scatters.Where(s => s.IsVisible))
                Logger.Log($"• {s.Label}, Points: {_rawYMap[s].Length}, Axis: {s.Axes.YAxis.Label.Text}");

            // Maintain consistent padding around the plot area
            PixelPadding padding = new(left: 40, right: 40, top: 0, bottom: 50);
            _plot.Plot.Layout.Fixed(padding);

            // Optionally hide the legend (unless needed later)
            _plot.Plot.Legend.IsVisible = false;
            // ✅ Configure automatic ticks with minor spacing
            var tickGen = new ScottPlot.TickGenerators.NumericAutomatic();
            tickGen.MinorTickGenerator = new ScottPlot.TickGenerators.EvenlySpacedMinorTickGenerator(5);
            _plot.Plot.Axes.Bottom.TickGenerator = tickGen;

            // ✅ Vertical grid lines only
            _plot.Plot.Grid.XAxisStyle.IsVisible = true;
            _plot.Plot.Grid.YAxisStyle.IsVisible = false;

            // ✅ Grid appearance
            _plot.Plot.Grid.MajorLineColor = ScottPlot.Colors.Grey.WithAlpha(75);
            _plot.Plot.Grid.MinorLineColor = ScottPlot.Colors.Grey.WithAlpha(25);
            _plot.Plot.Grid.MajorLineWidth = 2;
            _plot.Plot.Grid.MinorLineWidth = 1;


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
            if (axis == null)
            {
                Logger.Log("⚠️ [HideAxis] Attempted to hide a null axis — skipping.");
                return;
            }

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

            // ✅ Add vertical split line at t = 0.00s (start of RaceBox run)
            if (!_splitLinesBySlot.ContainsKey(slot))
                _splitLinesBySlot[slot] = new List<VerticalLine>();

            var startLine = _plot.Plot.Add.VerticalLine(0);
            startLine.LinePattern = LineStyleHelper.GetLinePattern(99);
            startLine.LineWidth = 1;
            startLine.Color = ScottPlot.Colors.Gray.WithAlpha(100);
            _splitLinesBySlot[slot].Add(startLine);

            Logger.Log($"📏 Added RaceBox START line at t = 0.00s for slot {slot}");

            // ✅ Add discipline split lines (6ft, 66ft, 132ft...)
            if (run.SplitTimes != null && run.SplitTimes.Any())
            {
                Logger.Log($"📏 Drawing {run.SplitTimes.Count} RaceBox split lines...");
                AddRaceBoxSplitLines(slot, run.SplitTimes, run.SplitLabels, includeZero: true);
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

        /// <summary>
        /// Toggle visibility of every plottable that belongs to a run slot
        /// (scatters, split lines, split labels, etc.)
        /// </summary>
        public void ToggleRunVisibility(int slot, bool isVisibleNow)
        {
            _runVisibility[slot] = isVisibleNow;
            Logger.Log($"🔁 Toggled Run Visibility — Slot {slot}, NowVisible={isVisibleNow}");

            /* ----------------------------------------------------------------
             * 1. If the user tries to show a slot that was never plotted (or
             *    its scatters were disposed), rebuild it on-demand.
             * -------------------------------------------------------------- */
            bool hasAnyScatters = _scatters.Any(s =>
                _scatterSlotMap.TryGetValue(s, out int sSlot) && sSlot == slot);

            if (isVisibleNow && !hasAnyScatters)
            {
                Logger.Log($"♻️ No active plots for slot {slot}. Forcing re-plot.");
                if (_runsBySlot.TryGetValue(slot, out RunData run))
                {
                    if (run.IsRaceBox)
                    {
                        Logger.Log($"♻️ Re-plotting hidden RaceBox slot {slot}...");
                        PlotRaceBoxRun(slot, run);
                    }
                    else
                    {
                        PlotRuns(new Dictionary<int, RunData>(_runsBySlot));
                    }
                }
            }

            /* ----------------------------------------------------------------
             * 2. Show / hide all scatters belonging to this slot
             * -------------------------------------------------------------- */
            foreach (var scatter in _scatters)
            {
                if (_scatterSlotMap.TryGetValue(scatter, out int scatterSlot) &&
                    scatterSlot == slot)
                {
                    string channel = scatter.Label;
                    bool channelOn = _channelVisibility.TryGetValue(channel, out bool vis) ? vis : true;
                    scatter.IsVisible = isVisibleNow && channelOn;
                }
            }

            /* ----------------------------------------------------------------
             * 3. Show / hide split lines *and* labels in a single helper
             * -------------------------------------------------------------- */
            SetSplitVisibility(slot, isVisibleNow);              // ← NEW

            // -----------------------------------------------------------------
            // 4. Rebuild split lines if the slot is being shown and no objects
            //    currently exist (e.g., they were cleared when the log unloaded)
            // -----------------------------------------------------------------
            RunData run2;
            bool hasRun = _runsBySlot.TryGetValue(slot, out run2);
            bool needRebuild = isVisibleNow
                             && (!_splitLinesBySlot.ContainsKey(slot)
                                 || _splitLinesBySlot[slot].Count == 0)
                             && hasRun
                             && run2.IsRaceBox
                             && run2.SplitTimes?.Count > 0;

            if (needRebuild)
            {
                Logger.Log($"➕ Rebuilding missing split lines for slot {slot} after re-show");
                AddRaceBoxSplitLines(slot, run2.SplitTimes, run2.SplitLabels);
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
            _plot.Refresh();
            _plot.Plot.Axes.AutoScale();
            
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

            SetSplitVisibility(slot, newState);

            _plot.Refresh();

            return newState; // <--- Make sure this is here and method ends with this return
        }




        //===============================================================================
        public void ResetEmptyPlot()
        {
            _plot.Plot.Clear();
            _plot.Plot.Axes.Rules.Clear();

            // ✅ Disable vertical and horizontal grid lines
            _plot.Plot.Grid.XAxisStyle.IsVisible = false;
            _plot.Plot.Grid.YAxisStyle.IsVisible = false;

            // ✅ Hide all known axes
            var allAxes = new List<IAxis>
    {
        _plot.Plot.Axes.Bottom,
        throttleAxis,
         _plot.Plot.Axes.Left,
        rpmAxis,
        voltageAxis,
        currentAxis,
        rippleAxis,
        powerAxis,
        escTempAxis,
        motorTempAxis,
        motorTimingAxis,
        accelAxis,
        raceBoxSpeedAxis,
        raceBoxGxAxis
    };

            foreach (var axis in allAxes)
            {
                if (axis == null) continue;

                bool isBottom = axis == _plot.Plot.Axes.Bottom;

                // ❌ Completely hide bottom axis in empty state
                axis.Label.IsVisible = false;
                axis.TickLabelStyle.IsVisible = false;
                axis.MajorTickStyle.Length = 0;
                axis.MinorTickStyle.Length = 0;
                axis.FrameLineStyle.Width = 0;

            }


            // ✅ Add center placeholder text
            var msg = _plot.Plot.Add.Text("Waiting for log...", 0, 0);
            msg.Alignment = Alignment.MiddleCenter;
            msg.FontSize = 18;
            msg.Color = ScottPlot.Colors.Gray;

            // ✅ Maintain layout (title space)
            PixelPadding padding = new(left: 40, right: 40, top: 10, bottom: 50);
            _plot.Plot.Layout.Fixed(padding);

            // ✅ Hide legend
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

        private void AddRaceBoxSplitLines(int slot, List<double> splitTimes, List<string> splitLabels, bool includeZero = false)
        {
            Logger.Log($"📍 [SplitLines] Begin → slot={slot}, count={splitTimes?.Count ?? 0}");
           
            EnsureSplitLabelAxis();

            RemovePreviousSplitLines(slot);
            RemovePreviousSplitLabels(slot);

            var (times, labels) = PrepareSplitData(splitTimes, splitLabels, includeZero);
            const double yLabel = 0.95; ;

            _splitLinesBySlot[slot] = DrawSplitLines(times);
            _splitLabelsBySlot[slot] = DrawSplitLabels(times, labels, yLabel);

            Logger.Log($"✅ [SplitLines] Completed → slot={slot}, lines={times.Count}, yLabel={yLabel:F2}");

            _plot.Refresh();

        }
        private (List<double> times, List<string> labels) PrepareSplitData(List<double> splitTimes, List<string> splitLabels, bool includeZero)
        {
            var times = new List<double>(splitTimes ?? new());
            var labels = new List<string>(splitLabels ?? new());

            if (includeZero && !times.Contains(0.0))
            {
                times.Insert(0, 0.0);
                labels.Insert(0, "Start");
                Logger.Log("➕ [SplitPrep] Inserted 0.00s → label='Start'");
            }

            if (times.Count != labels.Count)
                Logger.Log($"⚠️ [SplitPrep] Label mismatch: {labels.Count} labels vs {times.Count} times");

            return (times, labels);
        }
        private double GetSplitLabelYPosition(List<double> times)
        {
            double yLabel = 5; // <- locked value not linked to any axis
            Logger.Log($"🔒 [YLock] Using fixed yLabel: {yLabel:F2}");
            return yLabel;
        }

        private List<VerticalLine> DrawSplitLines(List<double> times)
        {
            var lines = new List<VerticalLine>();

            foreach (double t in times)
            {
                var vLine = _plot.Plot.Add.VerticalLine(t);
                vLine.LinePattern = LineStyleHelper.GetLinePattern(99);
                vLine.LineWidth = 2;
                vLine.Color = ScottPlot.Colors.DarkBlue.WithAlpha(200);
                lines.Add(vLine);

                Logger.Log($"📍 [DrawLine] t={t:F3}s");
            }

            Logger.Log($"✅ [DrawLine] Total vertical lines drawn: {lines.Count}");
            return lines;
        }
        /// <summary>
        /// Draw text labels for every RaceBox split line.
        /// All labels are fixed to the hidden _splitLabelAxis so they never
        /// move when the primary Y-axis pans or zooms.
        /// </summary>
        private List<Text> DrawSplitLabels(List<double> times,
                                   List<string> labels,
                                   double yLabel)
        {
            // Axis already prepared by caller, but guard anyway
            EnsureSplitLabelAxis();

            var result = new List<Text>();

            for (int i = 0; i < times.Count; i++)
            {
                string txt = i < labels.Count ? labels[i] : $"Split {i}";
                double t = times[i];

                var lbl = _plot.Plot.Add.Text(txt, t, yLabel);
                lbl.Axes.YAxis = _splitLabelAxis!;   // 🔒 pin to locked axis
                lbl.Alignment = Alignment.UpperCenter;

                // styling (unchanged)
                lbl.FontSize = 12;
                lbl.FontColor = ScottPlot.Colors.DarkBlue;
                lbl.BackgroundColor = ScottPlot.Colors.White.WithAlpha(180);
                lbl.BorderColor = ScottPlot.Colors.DarkBlue;
                lbl.BorderWidth = 1;
                lbl.OffsetY = -2;
                result.Add(lbl);
            }
            return result;
        }


        private void RemovePreviousSplitLines(int slot)
        {
            if (_splitLinesBySlot.ContainsKey(slot))
            {
                foreach (var line in _splitLinesBySlot[slot])
                    _plot.Plot.Remove(line);
                _splitLinesBySlot.Remove(slot);
                Logger.Log($"🧹 [Cleanup] Removed {slot} split lines");
            }
        }
                private void RemovePreviousSplitLabels(int slot)
        {
            if (_splitLabelsBySlot.ContainsKey(slot))
            {
                foreach (var lbl in _splitLabelsBySlot[slot])
                    _plot.Plot.Remove(lbl);
                _splitLabelsBySlot.Remove(slot);
                Logger.Log($"🧹 [Cleanup] Removed {slot} split labels");
            }
        }
        /// <summary>
        /// Show or hide split lines and their labels for the given slot
        /// </summary>
        private void SetSplitVisibility(int slot, bool visible)
        {
            if (_splitLinesBySlot.TryGetValue(slot, out var lines))
                foreach (var ln in lines)
                    ln.IsVisible = visible;

            if (_splitLabelsBySlot.TryGetValue(slot, out var lbls))
                foreach (var lbl in lbls)
                    lbl.IsVisible = visible;
        }

        /// <summary>
        /// Grabs the plot’s built-in right Y-axis, hides it,
        /// and locks its vertical range to 0–1 so split labels never move.
        /// Call this once *before* you add any label.
        /// </summary>
        private void EnsureSplitLabelAxis()
    {
        if (_splitLabelAxis is not null)
            return;                                   // already prepared

        _splitLabelAxis = _plot.Plot.Axes.Right;      // this axis always exists

        // ① lock its numeric range forever
        _plot.Plot.Axes.Rules.Add(new LockedVertical(_splitLabelAxis, 0.0, 1.0));

        // ② hide frame, ticks, and label (same helper you use for Castle axes)
        HideAxis(_splitLabelAxis);
    }
        // CastleOverlayV2/Plot/PlotManager.cs

        // Add to class-level fields
        private double _castleTimeShift = 0.10;

        // Call this to shift Castle log start time
        public void SetCastleTimeShift(double shiftSeconds)
        {
            _castleTimeShift = shiftSeconds;
            Logger.Log($"⏱️ Castle Time Shift set to {shiftSeconds:F3} seconds");
        }


    }
}

