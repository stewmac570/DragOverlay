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
        private bool _alignmentMode;
        private bool _alignmentDragging;
        private double _lastAlignmentMouseX;

        // Cursor
        private VerticalLine _cursor;

        // Lines and lookups
        private readonly List<Scatter> _scatters = new();
        private readonly Dictionary<Scatter, double[]> _rawYMap = new();
        private readonly Dictionary<Scatter, int> _scatterSlotMap = new();

        // Lazy cache of channel → scatters, rebuilt on demand after _scatters mutates.
        private Dictionary<string, List<Scatter>>? _channelToScattersCache;

        // Focus model: one channel is "focused" (full opacity, normal line width, owns the
        // visible left Y axis); every other visible channel renders as a dim context trace.
        // Initial value is RPM; SetFocusedChannel(...) flips it on a card click (Phase 3).
        private string _focusedChannel = "RPM";

        // Line widths per focus state (spec §6.3).
        private const float FocusedLineWidth = 2.0f;
        private const float ContextLineWidth = 1.3f;

        // Dark theme tokens (from Docs/DragOverlay_UI_Spec.md §5).
        private static readonly ScottPlot.Color ThemePlotBg = new(0x0E, 0x12, 0x18);   // surface.plot
        private static readonly ScottPlot.Color ThemeWindow = new(0x13, 0x17, 0x1E);   // surface.window
        private static readonly ScottPlot.Color ThemeText = new(0xE6, 0xE9, 0xEF);     // text.primary
        private static readonly ScottPlot.Color ThemeTextDim = new(0x9A, 0xA3, 0xB2);  // text.secondary
        private static readonly ScottPlot.Color ThemeGrid = new(0xFF, 0xFF, 0xFF, 13); // border.grid (~0.05 alpha)
        private static readonly ScottPlot.Color ThemeSplit = new(0x7C, 0x5A, 0x2E);    // split markers (66 / 132 ft)
        private const byte ContextAlpha = 71; // 0.28 × 255

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
        private IYAxis raceBoxDistanceAxis;

        // Events
        public event Action<Dictionary<string, double?[]>> CursorMoved;
        public event Action<double>? AlignmentDragged;

        private bool _isFourPoleMode = false;

        // ---- Scale Profiles (Drag vs Speed-Run) ----
        private enum ScaleProfile { Drag, SpeedRun }
        private ScaleProfile _activeProfile = ScaleProfile.Drag;

        // Per-profile per-channel scales for CASTLE channels ONLY.
        private readonly Dictionary<string, (double Min, double Max)> _dragScales = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Throttle %"] = (-100, 120),
            ["Voltage"] = (6.0, 9.0),
            ["Current"] = (0.0, 800.0),
            ["Ripple"] = (0.0, 5.0),
            ["PowerOut"] = (0.0, 120.0),
            ["ESC Temp"] = (20.0, 120.0),
            ["MotorTemp"] = (20.0, 120.0),
            ["MotorTiming"] = (0.0, 120.0),
            ["Acceleration"] = (-5.0, 7.0),
        };

        private readonly Dictionary<string, (double Min, double Max)> _speedScales = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Throttle %"] = (-10, 110),
            ["Voltage"] = (6.0, 9.0),
            ["Current"] = (0.0, 800.0),
            ["Ripple"] = (0.0, 5.0),
            ["PowerOut"] = (0.0, 120.0),
            ["ESC Temp"] = (20.0, 120.0),
            ["MotorTemp"] = (20.0, 120.0),
            ["MotorTiming"] = (0.0, 120.0),
            ["Acceleration"] = (-5.0, 7.0),
        };

        // Extra Castle offset (seconds) added to each Castle run
        private double _castleTimeShift = 0.0;
        public void SetCastleTimeShift(double shiftSeconds)
        {
            _castleTimeShift = shiftSeconds;
            Logger.Log($"⏱️ Castle Time Shift set to {shiftSeconds:F3} seconds");
        }

        // Public API to flip profiles
        public void SetSpeedMode(bool isSpeedRun)
        {
            _activeProfile = isSpeedRun ? ScaleProfile.SpeedRun : ScaleProfile.Drag;
            Logger.Log($"📐 Scale Profile → {(_activeProfile == ScaleProfile.SpeedRun ? "Speed-Run" : "Drag")}");
            //ReapplyAxisLocks();
            //_plot.Refresh();
        }

        /// <summary>Set per-channel Y-scale for current profile (or explicitly for Drag/Speed-Run).</summary>
        public void SetChannelScale(string channel, double min, double max, bool? forSpeedRun = null)
        {
            if (string.IsNullOrWhiteSpace(channel)) return;

            var dict = (forSpeedRun ?? (_activeProfile == ScaleProfile.SpeedRun))
                ? _speedScales
                : _dragScales;

            dict[channel] = (min, max);
            Logger.Log($"📏 SetChannelScale[{(dict == _speedScales ? "Speed-Run" : "Drag")}]: {channel} = ({min}, {max})");
            ReapplyAxisLocks();
            _plot.Refresh();
        }

        public PlotManager(FormsPlot plotControl)
        {
            _plot = plotControl ?? throw new ArgumentNullException(nameof(plotControl));
            _plot.Plot.Clear();

            // Dark theme — figure + data backgrounds
            _plot.Plot.FigureBackground.Color = ThemeWindow;
            _plot.Plot.DataBackground.Color = ThemePlotBg;

            // Title & legend
            _plot.Plot.Title(null);
            _plot.Plot.Legend.IsVisible = false;

            // 🔒 make Y-locks stick straight away
            ReapplyAxisLocks();

            _plot.Refresh();

            // X axis label (dark theme: secondary text colour)
            var x = _plot.Plot.Axes.Bottom;
            x.Label.Text = "Time (s)";
            x.Label.FontSize = 12;
            ApplyDarkAxisStyle(x, ThemeTextDim);

            // Grid style (vertical only, low-alpha for dark theme)
            _plot.Plot.Grid.XAxisStyle.IsVisible = true;
            _plot.Plot.Grid.YAxisStyle.IsVisible = false;
            _plot.Plot.Grid.MajorLineColor = ThemeGrid;
            _plot.Plot.Grid.MinorLineColor = ThemeGrid;
            _plot.Plot.Grid.MajorLineWidth = 1;
            _plot.Plot.Grid.MinorLineWidth = 1;

            _plot.MouseMove += FormsPlot_MouseMove;
            _plot.MouseDown += FormsPlot_MouseDown;
            _plot.MouseUp += FormsPlot_MouseUp;
            _plot.Refresh();
        }

        /// <summary>
        /// Set tick + label + frame colours on an axis to a single dark-theme tint.
        /// </summary>
        private static void ApplyDarkAxisStyle(IAxis axis, ScottPlot.Color color)
        {
            axis.Label.ForeColor = color;
            axis.TickLabelStyle.ForeColor = color;
            axis.MajorTickStyle.Color = color;
            axis.MinorTickStyle.Color = color;
            axis.FrameLineStyle.Color = color;
        }

        public string FocusedChannel => _focusedChannel;

        /// <summary>
        /// Change the focused channel. Re-tints every existing scatter (full opacity if it
        /// matches the new focus, dimmed otherwise), re-binds the single visible Y axis to
        /// the new channel, and triggers a refresh. No replot needed.
        /// </summary>
        public void SetFocusedChannel(string channelName)
        {
            if (_focusedChannel == channelName) return;
            _focusedChannel = channelName;

            foreach (var s in _scatters)
            {
                s.Color = GetTraceColor(s.Label);
                s.LineWidth = WidthFor(s.Label);
            }

            ApplyFocusToAxes();
            _plot.Refresh();
        }

        /// <summary>
        /// Channel hue, dimmed to <see cref="ContextAlpha"/> when this channel is not the focused one.
        /// </summary>
        private ScottPlot.Color GetTraceColor(string channelLabel)
        {
            var baseColor = ChannelColorMap.GetColor(channelLabel);
            return channelLabel == _focusedChannel
                ? baseColor
                : baseColor.WithAlpha(ContextAlpha);
        }

        private float WidthFor(string channelLabel) =>
            channelLabel == _focusedChannel ? FocusedLineWidth : ContextLineWidth;

        // ---------------- Public API ----------------

        public void SetFourPoleMode(bool isFourPole)
        {
            _isFourPoleMode = isFourPole;
            Logger.Log($"⚙️ Four-Pole Mode: {(_isFourPoleMode ? "ON" : "OFF")}");
            ReapplyAxisLocks();
            _plot.Refresh();
        }

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
            if (Logger.IsEnabled)
            {
                Logger.Log("🟢 SetInitialChannelVisibility():");
                foreach (var kvp in visibilityMap)
                    Logger.Log($"   • {kvp.Key} = {(kvp.Value ? "Visible" : "Hidden")}");
            }
            _channelVisibility = visibilityMap ?? new();
        }

        public void RefreshPlot() => _plot.Refresh();

        public void SetAlignmentMode(bool enabled)
        {
            _alignmentMode = enabled;
            _alignmentDragging = false;
            _plot.Cursor = Cursors.Default;
            _plot.UserInputProcessor.Enable();
        }

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
                if (toRemove.Count > 0)
                    _channelToScattersCache = null;

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

            var previousLimits = _plot.Plot.Axes.GetLimits();
            bool preserveAlignmentZoom =
                _alignmentMode &&
                _scatters.Count > 0 &&
                double.IsFinite(previousLimits.Left) &&
                double.IsFinite(previousLimits.Right) &&
                previousLimits.Right > previousLimits.Left;

            // Clear
            _plot.Plot.Clear();
            _scatters.Clear();
            _rawYMap.Clear();
            _scatterSlotMap.Clear();
            _channelToScattersCache = null;
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
                    s.Color = GetTraceColor(channelLabel);
                    s.LinePattern = LineStyleHelper.GetLinePattern(slot - 1);
                    s.LineWidth = WidthFor(channelLabel);
                    s.MarkerSize = 0; // line-only — markers are noise (spec §6.3)
                    s.Axes.XAxis = xAxis;
                    s.Axes.YAxis = channelLabel switch
                    {
                        "RPM" => rpmAxis,
                        "Throttle %" => throttleAxis,
                        "Voltage" => voltageAxis,
                        "Current" => currentAxis,
                        "Ripple" => rippleAxis,
                        "PowerOut" => powerAxis,
                        "ESC Temp" => escTempAxis,
                        "MotorTemp" => motorTempAxis,
                        "MotorTiming" => motorTimingAxis,
                        "Acceleration" => accelAxis,
                        _ => throttleAxis,
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

            double minX;
            double maxX;
            if (preserveAlignmentZoom)
            {
                minX = previousLimits.Left;
                maxX = previousLimits.Right;
            }
            else
            {
                // --- Set initial X range from the data without touching Y ---
                (minX, maxX) = GetTimeRange(runsBySlot);
                minX -= 0.10;
                maxX += 0.10;
            }

            // preserve current Y limits exactly as-is
            var lim = _plot.Plot.Axes.GetLimits();
            _plot.Plot.Axes.SetLimits(minX, maxX, lim.Bottom, lim.Top);

            // Now (and only now) apply the Y locks to the real axes
            ReapplyAxisLocks();

            _plot.Refresh();


        }

        // ---------------- Axes ----------------

        // Made public because MainForm calls this
        // Made public because MainForm calls this
        public void SetupAllAxes()
        {
            // 1) Create axes ONCE and reuse thereafter
            if (throttleAxis is null)
            {
                throttleAxis = _plot.Plot.Axes.Left;
                throttleAxis.Label.Text = "Throttle (%)";
                HideAxis(throttleAxis);
            }

            if (rpmAxis is null)
            {
                rpmAxis = _plot.Plot.Axes.AddRightAxis();
                rpmAxis.Label.Text = "RPM";
                HideAxis(rpmAxis);
            }

            if (voltageAxis is null)
            {
                voltageAxis = _plot.Plot.Axes.AddLeftAxis();
                voltageAxis.Label.Text = "Voltage (V)";
                HideAxis(voltageAxis);
            }

            if (currentAxis is null)
            {
                currentAxis = _plot.Plot.Axes.AddRightAxis();
                currentAxis.Label.Text = "Current (A)";
                HideAxis(currentAxis);
            }

            if (rippleAxis is null)
            {
                rippleAxis = _plot.Plot.Axes.AddRightAxis();
                rippleAxis.Label.Text = "Ripple (V)";
                HideAxis(rippleAxis);
            }

            if (powerAxis is null)
            {
                powerAxis = _plot.Plot.Axes.AddLeftAxis();
                powerAxis.Label.Text = "Power Out (W)";
                HideAxis(powerAxis);
            }

            if (escTempAxis is null)
            {
                escTempAxis = _plot.Plot.Axes.AddRightAxis();
                escTempAxis.Label.Text = "ESC Temp (°C)";
                HideAxis(escTempAxis);
            }

            if (motorTempAxis is null)
            {
                motorTempAxis = _plot.Plot.Axes.AddRightAxis();
                motorTempAxis.Label.Text = "Motor Temp (°C)";
                HideAxis(motorTempAxis);
            }

            if (motorTimingAxis is null)
            {
                motorTimingAxis = _plot.Plot.Axes.AddRightAxis();
                motorTimingAxis.Label.Text = "Motor Timing (deg)";
                HideAxis(motorTimingAxis);
            }

            if (accelAxis is null)
            {
                accelAxis = _plot.Plot.Axes.AddRightAxis();
                accelAxis.Label.Text = "Acceleration (g)";
                HideAxis(accelAxis);
            }

            // RaceBox axes (also create once)
            if (raceBoxSpeedAxis is null)
            {
                raceBoxSpeedAxis = _plot.Plot.Axes.AddRightAxis();
                raceBoxSpeedAxis.Label.Text = "Speed (mph)";
                HideAxis(raceBoxSpeedAxis);
            }

            if (raceBoxGxAxis is null)
            {
                raceBoxGxAxis = _plot.Plot.Axes.AddRightAxis();
                raceBoxGxAxis.Label.Text = "G-Force X (g)";
                HideAxis(raceBoxGxAxis);
            }

            if (raceBoxDistanceAxis is null)
            {
                raceBoxDistanceAxis = _plot.Plot.Axes.AddRightAxis();
                raceBoxDistanceAxis.Label.Text = "Distance (ft)";
                HideAxis(raceBoxDistanceAxis);
            }

            // Show the focused channel's axis with its hue; keep the rest hidden.
            ApplyFocusToAxes();

            // NOTE: Do NOT call AddLeftAxis/AddRightAxis again after this point.
            // Lock add/remove lives in ReapplyAxisLocks (single source of truth).
        }

        /// <summary>
        /// Make the focused channel's Y axis visible in its hue; every other Castle/RaceBox
        /// Y axis stays hidden. Called after axis creation and on every replot.
        /// </summary>
        private void ApplyFocusToAxes()
        {
            // Hide all known axes first.
            foreach (var axis in new IAxis?[] {
                throttleAxis, rpmAxis, voltageAxis, currentAxis, rippleAxis, powerAxis,
                escTempAxis, motorTempAxis, motorTimingAxis, accelAxis,
                raceBoxSpeedAxis, raceBoxGxAxis, raceBoxDistanceAxis })
            {
                if (axis is not null) HideAxis(axis);
            }

            // Show the focused channel's axis with its hue.
            var focusAxis = AxisFor(_focusedChannel);
            if (focusAxis is not null)
            {
                var hue = ChannelColorMap.GetColor(_focusedChannel);
                focusAxis.Label.IsVisible = true;
                focusAxis.TickLabelStyle.IsVisible = true;
                focusAxis.MajorTickStyle.Length = 5;
                focusAxis.MinorTickStyle.Length = 3;
                focusAxis.FrameLineStyle.Width = 1;
                ApplyDarkAxisStyle(focusAxis, hue);
            }
        }

        private IAxis? AxisFor(string channelLabel) => channelLabel switch
        {
            "RPM" => rpmAxis,
            "Throttle %" => throttleAxis,
            "Voltage" => voltageAxis,
            "Current" => currentAxis,
            "Ripple" => rippleAxis,
            "PowerOut" => powerAxis,
            "ESC Temp" => escTempAxis,
            "MotorTemp" => motorTempAxis,
            "MotorTiming" => motorTimingAxis,
            "Acceleration" => accelAxis,
            "RaceBox Speed" => raceBoxSpeedAxis,
            "RaceBox Distance" => raceBoxDistanceAxis,
            "RaceBox G-Force X" => raceBoxGxAxis,
            _ => null
        };


        private void ReapplyAxisLocks()
        {
            // Single source of truth for LockedVertical rules. Wipes locks for every
            // known axis, then re-adds RaceBox (fixed), split-label (fixed), and
            // Castle (via active profile).
            var knownAxes = new HashSet<IAxis>(new[]
            {
                throttleAxis, rpmAxis, voltageAxis, currentAxis, rippleAxis, powerAxis,
                escTempAxis, motorTempAxis, motorTimingAxis, accelAxis,
                raceBoxSpeedAxis, raceBoxGxAxis, raceBoxDistanceAxis, _splitLabelAxis
            }.Where(a => a is not null)!);

            var toRemove = new List<IAxisRule>();
            foreach (var rule in _plot.Plot.Axes.Rules)
                if (rule is LockedVertical lv && knownAxes.Contains(lv.YAxis))
                    toRemove.Add(rule);
            foreach (var r in toRemove)
                _plot.Plot.Axes.Rules.Remove(r);

            if (raceBoxSpeedAxis is not null)
                _plot.Plot.Axes.Rules.Add(new LockedVertical(raceBoxSpeedAxis, 0, 110));
            if (raceBoxGxAxis is not null)
                _plot.Plot.Axes.Rules.Add(new LockedVertical(raceBoxGxAxis, -5, 7));
            if (raceBoxDistanceAxis is not null)
                _plot.Plot.Axes.Rules.Add(new LockedVertical(raceBoxDistanceAxis, 0, 1000));
            if (_splitLabelAxis is not null)
                _plot.Plot.Axes.Rules.Add(new LockedVertical(_splitLabelAxis, 0.0, 1.0));

            ApplyAxisLocksForActiveProfile();
        }

        private void ApplyAxisLocksForActiveProfile()
        {
            // Caller (ReapplyAxisLocks) has already cleared Castle locks. Just add.
            var dict = _activeProfile == ScaleProfile.SpeedRun ? _speedScales : _dragScales;

            bool TryGetScale(string key, out (double Min, double Max) range)
            {
                if (dict.TryGetValue(key, out range))
                    return true;
                range = default;
                return false;
            }

            // Throttle
            if (!TryGetScale("Throttle %", out var thr)) thr = (-100, 120);
            _plot.Plot.Axes.Rules.Add(new LockedVertical(throttleAxis, thr.Min, thr.Max));

            // RPM (allow override; else pole-mode default)
            if (TryGetScale("RPM", out var rpm))
            {
                _plot.Plot.Axes.Rules.Add(new LockedVertical(rpmAxis, rpm.Min, rpm.Max));
            }
            else
            {
                double rpmMax = _isFourPoleMode ? 100_000.0 : 200_000.0;
                _plot.Plot.Axes.Rules.Add(new LockedVertical(rpmAxis, 0.0, rpmMax));
            }

            // Voltage
            if (!TryGetScale("Voltage", out var vlt)) vlt = (6.0, 9.0);
            _plot.Plot.Axes.Rules.Add(new LockedVertical(voltageAxis, vlt.Min, vlt.Max));

            // Current
            if (!TryGetScale("Current", out var cur)) cur = (0.0, 800.0);
            _plot.Plot.Axes.Rules.Add(new LockedVertical(currentAxis, cur.Min, cur.Max));

            // Ripple
            if (!TryGetScale("Ripple", out var rip)) rip = (0.0, 5.0);
            _plot.Plot.Axes.Rules.Add(new LockedVertical(rippleAxis, rip.Min, rip.Max));

            // PowerOut
            if (!TryGetScale("PowerOut", out var pow)) pow = (0.0, 120.0);
            _plot.Plot.Axes.Rules.Add(new LockedVertical(powerAxis, pow.Min, pow.Max));

            // ESC Temp
            if (!TryGetScale("ESC Temp", out var et)) et = (20.0, 120.0);
            _plot.Plot.Axes.Rules.Add(new LockedVertical(escTempAxis, et.Min, et.Max));

            // Motor Temp
            if (!TryGetScale("MotorTemp", out var mt)) mt = (20.0, 120.0);
            _plot.Plot.Axes.Rules.Add(new LockedVertical(motorTempAxis, mt.Min, mt.Max));

            // Motor Timing
            if (!TryGetScale("MotorTiming", out var mtg)) mtg = (0.0, 120.0);
            _plot.Plot.Axes.Rules.Add(new LockedVertical(motorTimingAxis, mtg.Min, mtg.Max));

            // Acceleration
            if (!TryGetScale("Acceleration", out var acc)) acc = (-5.0, 7.0);
            _plot.Plot.Axes.Rules.Add(new LockedVertical(accelAxis, acc.Min, acc.Max));
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
            HideAxis(_splitLabelAxis);
            // Lock (0..1) is added by ReapplyAxisLocks now (single source of truth).
        }

        // ---------------- RaceBox ----------------

        private void PlotRaceBoxRun(int slot, RunData run)
        {
            if (run == null || !run.IsRaceBox) return;

            var channels = new[] { "RaceBox Speed", "RaceBox Distance", "RaceBox G-Force X" };
            foreach (var ch in channels)
            {
                if (!run.Data.TryGetValue(ch, out var pts) || pts.Count == 0)
                    continue;

                var rbTyped = pts.OfType<ModelPoint>().ToList();
                if (rbTyped.Count < 2) continue;

                // Fix #47: plot all samples, including legitimate near-zero G-Force values.
                double shiftSec = run.TimeShiftMs / 1000.0;
                double[] xs = rbTyped.Select(p => p.Time + shiftSec).ToArray();
                double[] ys = rbTyped.Select(p => p.Y).ToArray();

                var s = _plot.Plot.Add.Scatter(xs, ys);
                s.Label = ch;
                s.Color = GetTraceColor(ch);
                s.LinePattern = LineStyleHelper.GetLinePattern(slot - 1);
                s.LineWidth = WidthFor(ch);
                s.MarkerSize = 0; // line-only — markers are noise (spec §6.3)
                s.Axes.XAxis = _plot.Plot.Axes.Bottom;
                s.Axes.YAxis = ch switch
                {
                    "RaceBox Speed" => raceBoxSpeedAxis,
                    "RaceBox Distance" => raceBoxDistanceAxis,
                    _ => raceBoxGxAxis,
                };

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

            // Dark-theme split markers (spec §5: 66/132 ft = #7C5A2E, finish = #9A3D2E).
            // For Phase 1 use the 66/132 colour for all splits; the dedicated finish hue can
            // come back once the label data flags which split is the finish.
            var splitColor = ThemeSplit;
            var labelBg = new ScottPlot.Color(0x1B, 0x21, 0x2B);   // surface.bar
            var labelFg = ThemeText;

            var lines = new List<VerticalLine>();
            foreach (double t in times)
            {
                var v = _plot.Plot.Add.VerticalLine(t);
                v.LinePattern = LineStyleHelper.GetLinePattern(99);
                v.LineWidth = 1.5f;
                v.Color = splitColor;
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
                lbl.FontSize = 11;
                lbl.FontColor = labelFg;
                lbl.BackgroundColor = labelBg;
                lbl.BorderColor = splitColor;
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

        // Channel-name → scatters lookup used by MouseMove. Rebuilt lazily after any
        // mutation of _scatters (PlotRuns reset, SetRun slot-removal).
        private Dictionary<string, List<Scatter>> GetChannelToScatters()
        {
            if (_channelToScattersCache is not null)
                return _channelToScattersCache;

            var cache = new Dictionary<string, List<Scatter>>();
            foreach (var s in _scatters)
            {
                if (!cache.TryGetValue(s.Label, out var list))
                {
                    list = new List<Scatter>();
                    cache[s.Label] = list;
                }
                list.Add(s);
            }
            _channelToScattersCache = cache;
            return cache;
        }

        private void FormsPlot_MouseMove(object sender, MouseEventArgs e)
        {
            if (_alignmentMode && _alignmentDragging)
            {
                double currentX = _plot.Plot.GetCoordinates(new Pixel(e.X, e.Y)).X;
                double deltaMs = (currentX - _lastAlignmentMouseX) * 1000.0;
                _lastAlignmentMouseX = currentX;
                if (Math.Abs(deltaMs) > 0.001)
                    AlignmentDragged?.Invoke(deltaMs);
                return;
            }

            if (_cursor == null) return;

            var mouseCoord = _plot.Plot.GetCoordinates(new Pixel(e.X, e.Y));
            _cursor.X = mouseCoord.X;

            var valuesAtCursor = new Dictionary<string, double?[]>();

            var channels = GetChannelToScatters();

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

                    int index = -1;
                    double minDist = double.MaxValue;
                    for (int j = 0; j < pts.Count; j++)
                    {
                        double dist = Math.Abs(pts[j].X - mouseCoord.X);
                        if (dist < minDist)
                        {
                            minDist = dist;
                            index = j;
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

        private void FormsPlot_MouseDown(object? sender, MouseEventArgs e)
        {
            if (!_alignmentMode || e.Button != MouseButtons.Left) return;
            if ((Control.ModifierKeys & Keys.Control) != Keys.Control) return;

            _alignmentDragging = true;
            _lastAlignmentMouseX = _plot.Plot.GetCoordinates(new Pixel(e.X, e.Y)).X;
            _plot.Capture = true;
        }

        private void FormsPlot_MouseUp(object? sender, MouseEventArgs e)
        {
            if (!_alignmentDragging || e.Button != MouseButtons.Left) return;
            _alignmentDragging = false;
            _plot.Capture = false;
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
                raceBoxSpeedAxis, raceBoxGxAxis, raceBoxDistanceAxis
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
            msg.Color = ThemeTextDim;

            PixelPadding padding = new(left: 40, right: 40, top: 10, bottom: 50);
            _plot.Plot.Layout.Fixed(padding);

            _plot.Plot.Legend.IsVisible = false;

            // 🔒 make Y-locks stick straight away
            ReapplyAxisLocks();

            _plot.Refresh();

        }

        public void LogVisibilityStates()
        {
            if (!Logger.IsEnabled) return;

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
