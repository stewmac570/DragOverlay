// File: src/CastleOverlayV2/Plot/PlotManager.cs
using CastleOverlayV2.Models;
using CastleOverlayV2.Services;
using CastleOverlayV2.Utils;
using ScottPlot;
using ScottPlot.AxisPanels;
using ScottPlot.AxisRules;
using ScottPlot.Plottables;
using ScottPlot.WinForms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

// Disambiguate ScottPlot.DataPoint vs our model type
using ModelPoint = CastleOverlayV2.Models.DataPoint;

namespace CastleOverlayV2.Plot
{
    /// <summary>
    /// Manages the ScottPlot chart for displaying multiple runs (Castle + RaceBox).
    /// Preserves all existing features and fixes compile issues.
    /// </summary>
    public class PlotManager
    {
        private readonly FormsPlot _plot;

        // Cursor
        private VerticalLine _cursor;

        // Lines and lookups
        private readonly List<Scatter> _scatters = new();
        private readonly Dictionary<Scatter, double[]> _rawYMap = new();
        private readonly Dictionary<Scatter, int> _scatterSlotMap = new();

        // Visibility & storage
        private readonly Dictionary<int, bool> _runVisibility = new();
        private Dictionary<string, bool> _channelVisibility = new();
        private readonly Dictionary<int, RunData> _runsBySlot = new();
        public IReadOnlyDictionary<int, RunData> Runs => _runsBySlot;

        // RaceBox split lines & labels
        private readonly Dictionary<int, List<VerticalLine>> _splitLinesBySlot = new();
        private readonly Dictionary<int, List<Text>> _splitLabelsBySlot = new();
        private IYAxis? _splitLabelAxis;

        // Axes
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
        private IYAxis raceBoxSpeedAxis;
        private IYAxis raceBoxGxAxis;

        // Events
        public event Action<Dictionary<string, double?[]>> CursorMoved;

        private bool _isFourPoleMode = false;

        // Extra Castle offset (seconds) added to each Castle run
        private double _castleTimeShift = 0.0;
        public void SetCastleTimeShift(double shiftSeconds)
        {
            _castleTimeShift = shiftSeconds;
            Logger.Log($"⏱️ Castle Time Shift set to {shiftSeconds:F3} seconds");
        }

        public PlotManager(FormsPlot plotControl)
        {
            _plot = plotControl ?? throw new ArgumentNullException(nameof(plotControl));
            _plot.Plot.Clear();

            // Title & legend
            _plot.Plot.Title(null);
            _plot.Plot.Legend.IsVisible = false;

            // X axis label (ticks are configured on each plot so grid matches range)
            var x = _plot.Plot.Axes.Bottom;
            x.Label.Text = "Time (s)";
            x.Label.FontSize = 12;

            // Grid style (vertical only)
            _plot.Plot.Grid.XAxisStyle.IsVisible = true;
            _plot.Plot.Grid.YAxisStyle.IsVisible = false;
            _plot.Plot.Grid.MajorLineColor = ScottPlot.Colors.Grey.WithAlpha(75);
            _plot.Plot.Grid.MinorLineColor = ScottPlot.Colors.Grey.WithAlpha(25);
            _plot.Plot.Grid.MajorLineWidth = 2;
            _plot.Plot.Grid.MinorLineWidth = 1;

            _plot.MouseMove += FormsPlot_MouseMove;
            _plot.Refresh();
        }

        // ---------------- Public API ----------------

        public void SetFourPoleMode(bool isFourPole) => _isFourPoleMode = isFourPole;

        public bool GetRunVisibility(int slot) =>
            _runVisibility.TryGetValue(slot, out bool vis) ? vis : true;

        public void SetRunVisibility(int slot, bool isVisible)
        {
            _runVisibility[slot] = isVisible;
            Logger.Log($"SetRunVisibility: slot {slot} → {(isVisible ? "Visible" : "Hidden")}");
        }

        public bool ToggleRunVisibility(int slot)
        {
            bool curr = _runVisibility.TryGetValue(slot, out bool v) ? v : true;
            bool ns = !curr;
            _runVisibility[slot] = ns;

            foreach (var s in _scatters)
            {
                if (_scatterSlotMap.TryGetValue(s, out int ss) && ss == slot)
                {
                    string ch = s.Label;
                    bool chOn = _channelVisibility.TryGetValue(ch, out bool vis) ? vis : true;
                    s.IsVisible = ns && chOn;
                }
            }

            SetSplitVisibility(slot, ns);
            _plot.Refresh();
            return ns;
        }

        public void SetChannelVisibility(string channelName, bool isVisible)
        {
            _channelVisibility[channelName] = isVisible;

            foreach (var s in _scatters)
            {
                if (s.Label == channelName && _scatterSlotMap.TryGetValue(s, out int slot))
                {
                    bool runVisible = _runVisibility.TryGetValue(slot, out bool rv) ? rv : true;
                    s.IsVisible = runVisible && isVisible;
                }
            }

            _plot.Refresh();
        }

        public void SetInitialChannelVisibility(Dictionary<string, bool> visibilityMap)
        {
            Logger.Log("🟢 SetInitialChannelVisibility():");
            foreach (var kvp in visibilityMap)
                Logger.Log($"   • {kvp.Key} = {(kvp.Value ? "Visible" : "Hidden")}");
            _channelVisibility = visibilityMap ?? new();
        }

        public void RefreshPlot() => _plot.Refresh();

        public void FitToData()
        {
            _plot.Refresh();
            _plot.Plot.Axes.AutoScale();
        }

        public void SetRun(int slot, RunData run)
        {
            _runsBySlot[slot] = run;
            _runVisibility[slot] = run != null;

            if (run == null)
            {
                // Remove plottables for this slot
                var toRemove = _scatterSlotMap
                    .Where(kvp => kvp.Value == slot)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var s in toRemove)
                {
                    _plot.Plot.Remove(s);
                    _scatters.Remove(s);
                    _scatterSlotMap.Remove(s);
                    _rawYMap.Remove(s);
                }

                if (_splitLinesBySlot.TryGetValue(slot, out var lines))
                {
                    foreach (var ln in lines) _plot.Plot.Remove(ln);
                    _splitLinesBySlot.Remove(slot);
                }

                if (_splitLabelsBySlot.TryGetValue(slot, out var lbls))
                {
                    foreach (var lbl in lbls) _plot.Plot.Remove(lbl);
                    _splitLabelsBySlot.Remove(slot);
                }
            }
        }

        public void LoadRun(RunData run)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            PlotRuns(new Dictionary<int, RunData> { [1] = run });
        }

        // ---------------- Core Plot ----------------

        public void PlotRuns(Dictionary<int, RunData> runsBySlot)
        {
            if (runsBySlot == null || runsBySlot.Count == 0)
            {
                ResetEmptyPlot();
                return;
            }

            // Clear
            _plot.Plot.Clear();
            _scatters.Clear();
            _rawYMap.Clear();
            _scatterSlotMap.Clear();
            _plot.Plot.Axes.Rules.Clear();
            _splitLabelAxis = null;

            // Cursor
            _cursor = _plot.Plot.Add.VerticalLine(0);
            _cursor.LinePattern = LinePattern.Dashed;
            _cursor.Color = ScottPlot.Colors.Red;

            // Keep bottom axis visuals visible
            var xAxis = _plot.Plot.Axes.Bottom;
            xAxis.Label.IsVisible = true;
            xAxis.TickLabelStyle.IsVisible = true;
            xAxis.MajorTickStyle.Length = 5;
            xAxis.MinorTickStyle.Length = 3;
            xAxis.FrameLineStyle.Width = 1;

            // Axes and split-label axis
            SetupAllAxes();
            EnsureSplitLabelAxis();

            // Ensure run visibility default
            foreach (var slot in runsBySlot.Keys)
                if (!_runVisibility.ContainsKey(slot))
                    _runVisibility[slot] = true;

            // Plot Castle
            foreach (var kvp in runsBySlot)
            {
                int slot = kvp.Key;
                RunData run = kvp.Value;
                if (run == null) continue;
                if (run.IsRaceBox) continue;
                if (run.DataPoints == null || run.DataPoints.Count == 0) continue;

                double shiftSec = (run.TimeShiftMs / 1000.0) + _castleTimeShift;

                double[] xs = run.DataPoints.Select(dp => dp.Time + shiftSec).ToArray();

                foreach (var (channelLabel, rawYs, _) in GetChannelsWithRaw(run))
                {
                    double[] ysToPlot = rawYs;
                    if (_isFourPoleMode && channelLabel == "RPM")
                        ysToPlot = rawYs.Select(v => v * 0.5).ToArray();

                    var s = _plot.Plot.Add.Scatter(xs, ysToPlot);
                    s.Label = channelLabel;
                    s.Color = ChannelColorMap.GetColor(channelLabel);
                    s.LinePattern = LineStyleHelper.GetLinePattern(slot - 1);
                    s.LineWidth = (float)LineStyleHelper.GetLineWidth(slot - 1);
                    s.Axes.XAxis = xAxis;
                    s.Axes.YAxis = channelLabel switch
                    {
                        "RPM" => rpmAxis,
                        "Throttle %" => throttleAxis,   // <— NEW
                        "Voltage" => voltageAxis,
                        "Current" => currentAxis,
                        "Ripple" => rippleAxis,
                        "PowerOut" => powerAxis,
                        "ESC Temp" => escTempAxis,
                        "MotorTemp" => motorTempAxis,
                        "MotorTiming" => motorTimingAxis,
                        "Acceleration" => accelAxis,
                        //_ => throttleAxis
                    };


                    bool chOn = _channelVisibility.TryGetValue(channelLabel, out var v) ? v : true;
                    bool runOn = _runVisibility.TryGetValue(slot, out var rv) ? rv : true;
                    s.IsVisible = chOn && runOn;

                    _scatters.Add(s);
                    _rawYMap[s] = rawYs;
                    _scatterSlotMap[s] = slot;
                }
            }

            // Plot RaceBox
            foreach (var kvp in runsBySlot)
            {
                int slot = kvp.Key;
                RunData run = kvp.Value;
                if (run == null || !run.IsRaceBox) continue;

                PlotRaceBoxRun(slot, run);

                if (run.SplitTimes?.Count > 0)
                    AddRaceBoxSplitLines(slot, run.SplitTimes, run.SplitLabels, includeZero: true);

                bool isVisible = _runVisibility.TryGetValue(slot, out var vis) ? vis : true;
                SetSplitVisibility(slot, isVisible);

                foreach (var s in _scatters)
                {
                    if (_scatterSlotMap.TryGetValue(s, out int sSlot) && sSlot == slot)
                    {
                        string label = s.Label;
                        bool chOn = _channelVisibility.TryGetValue(label, out var cv) ? cv : true;
                        s.IsVisible = isVisible && chOn;
                    }
                }
            }

            // Re-apply manual time ticks covering the plotted range
            ApplyManualTimeTicks(runsBySlot);

            // Layout padding
            PixelPadding padding = new(left: 40, right: 40, top: 0, bottom: 50);
            _plot.Plot.Layout.Fixed(padding);

            _plot.Plot.Legend.IsVisible = false;
            _plot.Refresh();
        }

        // ---------------- Axes ----------------

        private void SetupAllAxes()
        {
            _plot.Plot.Axes.Rules.Clear();

            // Throttle as PERCENT
            throttleAxis = _plot.Plot.Axes.Left;
            throttleAxis.Label.Text = "Throttle (%)";
            // show the whole range; keep hidden by default like before
            _plot.Plot.Axes.Rules.Add(new LockedVertical(throttleAxis, -100, 120));
            HideAxis(throttleAxis);


            rpmAxis = _plot.Plot.Axes.AddRightAxis();
            rpmAxis.Label.Text = "RPM";
            double rpmMax = _isFourPoleMode ? 100000.0 : 200000.0;
            _plot.Plot.Axes.Rules.Add(new LockedVertical(rpmAxis, 0, rpmMax));
            HideAxis(rpmAxis);

            voltageAxis = _plot.Plot.Axes.AddLeftAxis();
            voltageAxis.Label.Text = "Voltage (V)";
            _plot.Plot.Axes.Rules.Add(new LockedVertical(voltageAxis, 6.0, 9.0));
            HideAxis(voltageAxis);

            currentAxis = _plot.Plot.Axes.AddRightAxis();
            currentAxis.Label.Text = "Current (A)";
            _plot.Plot.Axes.Rules.Add(new LockedVertical(currentAxis, 0, 800));
            HideAxis(currentAxis);

            rippleAxis = _plot.Plot.Axes.AddRightAxis();
            rippleAxis.Label.Text = "Ripple (V)";
            _plot.Plot.Axes.Rules.Add(new LockedVertical(rippleAxis, 0, 5.0));
            HideAxis(rippleAxis);

            powerAxis = _plot.Plot.Axes.AddLeftAxis();
            powerAxis.Label.Text = "Power Out (W)";
            _plot.Plot.Axes.Rules.Add(new LockedVertical(powerAxis, 0, 120));
            HideAxis(powerAxis);

            escTempAxis = _plot.Plot.Axes.AddRightAxis();
            escTempAxis.Label.Text = "ESC Temp (°C)";
            _plot.Plot.Axes.Rules.Add(new LockedVertical(escTempAxis, 20, 120));
            HideAxis(escTempAxis);

            motorTempAxis = _plot.Plot.Axes.AddRightAxis();
            motorTempAxis.Label.Text = "Motor Temp (°C)";
            _plot.Plot.Axes.Rules.Add(new LockedVertical(motorTempAxis, 20, 120));
            HideAxis(motorTempAxis);

            motorTimingAxis = _plot.Plot.Axes.AddRightAxis();
            motorTimingAxis.Label.Text = "Motor Timing (deg)";
            _plot.Plot.Axes.Rules.Add(new LockedVertical(motorTimingAxis, 0, 120));
            HideAxis(motorTimingAxis);

            accelAxis = _plot.Plot.Axes.AddRightAxis();
            accelAxis.Label.Text = "Acceleration (g)";
            _plot.Plot.Axes.Rules.Add(new LockedVertical(accelAxis, -5, 7));
            HideAxis(accelAxis);

            raceBoxSpeedAxis = _plot.Plot.Axes.AddRightAxis();
            raceBoxSpeedAxis.Label.Text = "Speed (mph)";
            _plot.Plot.Axes.Rules.Add(new LockedVertical(raceBoxSpeedAxis, 0, 110));
            HideAxis(raceBoxSpeedAxis);

            raceBoxGxAxis = _plot.Plot.Axes.AddRightAxis();
            raceBoxGxAxis.Label.Text = "G-Force X (g)";
            _plot.Plot.Axes.Rules.Add(new LockedVertical(raceBoxGxAxis, -5, 7));
            HideAxis(raceBoxGxAxis);
        }

        private static void HideAxis(IAxis axis)
        {
            axis.Label.IsVisible = false;
            axis.TickLabelStyle.IsVisible = false;
            axis.MajorTickStyle.Length = 0;
            axis.MinorTickStyle.Length = 0;
            axis.FrameLineStyle.Width = 0;
        }

        private void EnsureSplitLabelAxis()
        {
            if (_splitLabelAxis is not null)
                return;

            _splitLabelAxis = _plot.Plot.Axes.Right; // always exists
            _plot.Plot.Axes.Rules.Add(new LockedVertical(_splitLabelAxis, 0.0, 1.0));
            HideAxis(_splitLabelAxis);
        }

        // ---------------- RaceBox ----------------

        private void PlotRaceBoxRun(int slot, RunData run)
        {
            if (run == null || !run.IsRaceBox) return;

            var channels = new[] { "RaceBox Speed", "RaceBox G-Force X" };
            foreach (var ch in channels)
            {
                if (!run.Data.TryGetValue(ch, out var pts) || pts.Count == 0)
                    continue;

                var rbTyped = pts.OfType<ModelPoint>().ToList();
                if (rbTyped.Count == 0) continue;

                var good = rbTyped.Where(p => Math.Abs(p.Y) > 0.01).ToList();
                if (good.Count < 2) continue;

                double shiftSec = run.TimeShiftMs / 1000.0;
                double[] xs = good.Select(p => p.Time + shiftSec).ToArray();
                double[] ys = good.Select(p => p.Y).ToArray();

                var s = _plot.Plot.Add.Scatter(xs, ys);
                s.Label = ch;
                s.Color = ChannelColorMap.GetColor(ch);
                s.LinePattern = LineStyleHelper.GetLinePattern(slot - 1);
                s.LineWidth = (float)LineStyleHelper.GetLineWidth(slot - 1);
                s.Axes.XAxis = _plot.Plot.Axes.Bottom;
                s.Axes.YAxis = ch == "RaceBox Speed" ? raceBoxSpeedAxis : raceBoxGxAxis;

                bool chOn = _channelVisibility.TryGetValue(ch, out var vis) ? vis : true;
                bool runOn = _runVisibility.TryGetValue(slot, out var rvis) ? rvis : true;
                s.IsVisible = chOn && runOn;

                _scatters.Add(s);
                _rawYMap[s] = ys;
                _scatterSlotMap[s] = slot;
            }
        }

        private void AddRaceBoxSplitLines(int slot, List<double>? splitTimes, List<string>? splitLabels, bool includeZero = false)
        {
            if (splitTimes == null || splitTimes.Count == 0)
                return;

            EnsureSplitLabelAxis();
            RemovePreviousSplitLines(slot);
            RemovePreviousSplitLabels(slot);

            var times = new List<double>(splitTimes);
            var labels = new List<string>(splitLabels ?? Enumerable.Repeat(string.Empty, times.Count).ToList());

            if (includeZero && !times.Contains(0.0))
            {
                times.Insert(0, 0.0);
                labels.Insert(0, "Start");
            }

            var lines = new List<VerticalLine>();
            foreach (double t in times)
            {
                var v = _plot.Plot.Add.VerticalLine(t);
                v.LinePattern = LineStyleHelper.GetLinePattern(99);
                v.LineWidth = 2;
                v.Color = ScottPlot.Colors.DarkBlue.WithAlpha(200);
                lines.Add(v);
            }
            _splitLinesBySlot[slot] = lines;

            var lbls = new List<Text>();
            for (int i = 0; i < times.Count; i++)
            {
                string txt = i < labels.Count && !string.IsNullOrWhiteSpace(labels[i]) ? labels[i] : $"Split {i}";
                var lbl = _plot.Plot.Add.Text(txt, times[i], 0.95);
                lbl.Axes.YAxis = _splitLabelAxis!;
                lbl.Alignment = Alignment.UpperCenter;
                lbl.FontSize = 12;
                lbl.FontColor = ScottPlot.Colors.DarkBlue;
                lbl.BackgroundColor = ScottPlot.Colors.White.WithAlpha(180);
                lbl.BorderColor = ScottPlot.Colors.DarkBlue;
                lbl.BorderWidth = 1;
                lbl.OffsetY = -2;
                lbls.Add(lbl);
            }
            _splitLabelsBySlot[slot] = lbls;

            _plot.Refresh();
        }

        private void RemovePreviousSplitLines(int slot)
        {
            if (_splitLinesBySlot.TryGetValue(slot, out var lines))
            {
                foreach (var ln in lines) _plot.Plot.Remove(ln);
                _splitLinesBySlot.Remove(slot);
            }
        }

        private void RemovePreviousSplitLabels(int slot)
        {
            if (_splitLabelsBySlot.TryGetValue(slot, out var lbls))
            {
                foreach (var lbl in lbls) _plot.Plot.Remove(lbl);
                _splitLabelsBySlot.Remove(slot);
            }
        }

        private void SetSplitVisibility(int slot, bool visible)
        {
            if (_splitLinesBySlot.TryGetValue(slot, out var lines))
                foreach (var ln in lines) ln.IsVisible = visible;

            if (_splitLabelsBySlot.TryGetValue(slot, out var lbls))
                foreach (var lbl in lbls) lbl.IsVisible = visible;
        }

        // ---------------- Hover ----------------

        private void FormsPlot_MouseMove(object sender, MouseEventArgs e)
        {
            if (_cursor == null) return;

            var mouseCoord = _plot.Plot.GetCoordinates(new Pixel(e.X, e.Y));
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

                for (int i = 0; i < scatters.Count && i < 3; i++)
                {
                    var s = scatters[i];
                    if (!s.IsVisible) continue;

                    var pts = s.Data.GetScatterPoints();
                    if (pts == null || pts.Count == 0) continue;

                    var nearest = pts.OrderBy(p => Math.Abs(p.X - mouseCoord.X)).First();
                    int index = -1;
                    for (int j = 0; j < pts.Count; j++)
                    {
                        if (Math.Abs(pts[j].X - nearest.X) < 1e-12)
                        {
                            index = j;
                            break;
                        }
                    }

                    if (_rawYMap.TryGetValue(s, out var rawYs) && index >= 0 && index < rawYs.Length)
                    {
                        double value = rawYs[index];
                        channelValues[i] = (channelName == "RPM" && _isFourPoleMode) ? value * 0.5 : value;
                    }
                }

                valuesAtCursor[channelName] = channelValues;
            }

            CursorMoved?.Invoke(valuesAtCursor);
            _plot.Refresh();
        }

        // ---------------- Helpers ----------------

        private IEnumerable<(string Label, double[] RawYs, double[] ScaledYs)> GetChannelsWithRaw(RunData run)
        {
            double[] GetRaw(Func<ModelPoint, double> sel) =>
                run.DataPoints.Select(sel).ToArray();

            yield return ("RPM", GetRaw(dp => dp.Speed), GetRaw(dp => dp.Speed));
            yield return ("Throttle %", GetRaw(dp => dp.ThrottlePercent), GetRaw(dp => dp.ThrottlePercent));
            yield return ("Voltage", GetRaw(dp => dp.Voltage), GetRaw(dp => dp.Voltage));
            yield return ("Current", GetRaw(dp => dp.Current), GetRaw(dp => dp.Current));
            yield return ("Ripple", GetRaw(dp => dp.Ripple), GetRaw(dp => dp.Ripple));
            yield return ("PowerOut", GetRaw(dp => dp.PowerOut), GetRaw(dp => dp.PowerOut));
            yield return ("ESC Temp", GetRaw(dp => dp.Temperature), GetRaw(dp => dp.Temperature));
            yield return ("MotorTemp", GetRaw(dp => dp.MotorTemp), GetRaw(dp => dp.MotorTemp));
            yield return ("MotorTiming", GetRaw(dp => dp.MotorTiming), GetRaw(dp => dp.MotorTiming));
            yield return ("Acceleration", GetRaw(dp => dp.Acceleration), GetRaw(dp => dp.Acceleration));
        }

        public void ResetEmptyPlot()
        {
            _plot.Plot.Clear();
            _plot.Plot.Axes.Rules.Clear();

            _plot.Plot.Grid.XAxisStyle.IsVisible = false;
            _plot.Plot.Grid.YAxisStyle.IsVisible = false;

            var allAxes = new List<IAxis>
            {
                _plot.Plot.Axes.Bottom,
                _plot.Plot.Axes.Left,
                throttleAxis, rpmAxis, voltageAxis, currentAxis, rippleAxis, powerAxis,
                escTempAxis, motorTempAxis, motorTimingAxis, accelAxis,
                raceBoxSpeedAxis, raceBoxGxAxis
            }.Where(a => a != null).ToList();

            foreach (var axis in allAxes)
            {
                axis.Label.IsVisible = false;
                axis.TickLabelStyle.IsVisible = false;
                axis.MajorTickStyle.Length = 0;
                axis.MinorTickStyle.Length = 0;
                axis.FrameLineStyle.Width = 0;
            }

            var msg = _plot.Plot.Add.Text("Waiting for log...", 0, 0);
            msg.Alignment = Alignment.MiddleCenter;
            msg.FontSize = 18;
            msg.Color = ScottPlot.Colors.Gray;

            PixelPadding padding = new(left: 40, right: 40, top: 10, bottom: 50);
            _plot.Plot.Layout.Fixed(padding);

            _plot.Plot.Legend.IsVisible = false;
            _plot.Refresh();
        }

        public void LogVisibilityStates()
        {
            Logger.Log("🔍 Current Run Visibility States:");
            foreach (var kvp in _runVisibility)
                Logger.Log($"   Run Slot {kvp.Key}: {(kvp.Value ? "Visible" : "Hidden")}");

            Logger.Log("🔍 Current Channel Visibility States:");
            foreach (var kvp in _channelVisibility)
                Logger.Log($"   Channel '{kvp.Key}': {(kvp.Value ? "Visible" : "Hidden")}");
        }

        /// <summary>
        /// Re-apply dense manual time ticks: major 0.05s, minor 0.005s.
        /// Range spans plotted data with a small buffer.
        /// </summary>
        private void ApplyManualTimeTicks(Dictionary<int, RunData> runsBySlot)
        {
            (double minX, double maxX) = GetTimeRange(runsBySlot);
            minX -= 0.10;
            maxX += 0.10;

            var manual = new ScottPlot.TickGenerators.NumericManual();
            for (double pos = minX; pos <= maxX; pos += 0.05)
                manual.AddMajor(pos, pos.ToString("0.00"));
            for (double pos = minX + 0.005; pos <= maxX; pos += 0.005)
                manual.AddMinor(pos);

            _plot.Plot.Axes.Bottom.TickGenerator = manual;

            // Match original grid appearance for manual ticks
            _plot.Plot.Grid.XAxisStyle.IsVisible = true;
            _plot.Plot.Grid.YAxisStyle.IsVisible = false;
            _plot.Plot.Grid.XAxisStyle.MajorLineStyle.Color = ScottPlot.Colors.Black.WithAlpha(40);
            _plot.Plot.Grid.XAxisStyle.MajorLineStyle.Width = 1;
            _plot.Plot.Grid.XAxisStyle.MinorLineStyle.Color = ScottPlot.Colors.Gray.WithAlpha(25);
            _plot.Plot.Grid.XAxisStyle.MinorLineStyle.Width = 0.5f;
        }

        /// <summary>Compute overall min/max X across all runs, respecting per-run shift.</summary>
        private (double minX, double maxX) GetTimeRange(Dictionary<int, RunData> runsBySlot)
        {
            double minX = double.PositiveInfinity;
            double maxX = double.NegativeInfinity;

            foreach (var kvp in runsBySlot)
            {
                var run = kvp.Value;
                if (run == null) continue;

                double shiftSec = (run.TimeShiftMs / 1000.0) + (run.IsRaceBox ? 0.0 : _castleTimeShift);

                if (!run.IsRaceBox)
                {
                    if (run.DataPoints != null && run.DataPoints.Count > 0)
                    {
                        double localMin = run.DataPoints.First().Time + shiftSec;
                        double localMax = run.DataPoints.Last().Time + shiftSec;
                        if (localMin < minX) minX = localMin;
                        if (localMax > maxX) maxX = localMax;
                    }
                }
                else
                {
                    if (run.Data != null && run.Data.TryGetValue("RaceBox Speed", out var rb) && rb.Count > 0)
                    {
                        double localMin = rb.First().Time + shiftSec;
                        double localMax = rb.Last().Time + shiftSec;
                        if (localMin < minX) minX = localMin;
                        if (localMax > maxX) maxX = localMax;
                    }
                }
            }

            if (!double.IsFinite(minX) || !double.IsFinite(maxX) || minX >= maxX)
                return (-1.0, 10.0);

            return (minX, maxX);
        }

        /// <summary>
        /// Median sample step (seconds) for the given slot (Castle or RaceBox).
        /// Use this to nudge runs left/right exactly one point per key press.
        /// </summary>
        public double GetSampleStepSeconds(int slot)
        {
            if (!_runsBySlot.TryGetValue(slot, out var run) || run == null)
                return 0.01;

            IEnumerable<double> times;

            if (run.IsRaceBox)
            {
                if (!run.Data.TryGetValue("RaceBox Speed", out var rb) || rb.Count < 2)
                    return 0.01;
                times = rb.Select(p => p.Time);
            }
            else
            {
                if (run.DataPoints == null || run.DataPoints.Count < 2)
                    return 0.01;
                times = run.DataPoints.Select(p => p.Time);
            }

            var diffs = times.Zip(times.Skip(1), (a, b) => b - a)
                             .Where(d => d > 0)
                             .OrderBy(d => d)
                             .ToArray();

            return diffs.Length == 0 ? 0.01 : diffs[diffs.Length / 2];
        }
    }
}
