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

using ModelPoint = CastleOverlayV2.Models.DataPoint;

namespace CastleOverlayV2.Plot
{
    /// <summary>Owns the ScottPlot state and rendering work for one chart pane.</summary>
    internal sealed class ChartPane
    {
        private readonly ChannelPaneMap _channelPaneMap;
        private readonly int _paneIndex;
        private readonly List<Scatter> _scatters = new();
        private readonly Dictionary<Scatter, double[]> _rawYMap = new();
        private readonly Dictionary<Scatter, int> _scatterSlotMap = new();
        private Dictionary<string, List<Scatter>>? _channelToScattersCache;

        private readonly Dictionary<int, List<VerticalLine>> _splitLinesBySlot = new();
        private readonly Dictionary<int, List<Text>> _splitLabelsBySlot = new();
        private readonly Dictionary<int, List<VerticalSpan>> _tcSpansBySlot = new();
        private IYAxis? _splitLabelAxis;

        private IYAxis? _throttleAxis;
        private IYAxis? _rpmAxis;
        private IYAxis? _voltageAxis;
        private IYAxis? _currentAxis;
        private IYAxis? _rippleAxis;
        private IYAxis? _powerAxis;
        private IYAxis? _escTempAxis;
        private IYAxis? _motorTempAxis;
        private IYAxis? _motorTimingAxis;
        private IYAxis? _accelAxis;
        private IYAxis? _raceBoxSpeedAxis;
        private IYAxis? _raceBoxGxAxis;
        private IYAxis? _raceBoxDistanceAxis;
        private IYAxis? _wheelRevsAxis;
        private IYAxis? _slipAxis;
        private IYAxis? _tcStateAxis;
        private double _miniLoggerMphPerRevPerSec =
            MiniLoggerLoader.MphPerRevPerSec(MiniLoggerLoader.DefaultTyreDiameterMm);

        private VerticalLine? _cursor;

        private static readonly ScottPlot.Color ThemePlotBg = new(0x0E, 0x12, 0x18);
        private static readonly ScottPlot.Color ThemeWindow = new(0x13, 0x17, 0x1E);
        private static readonly ScottPlot.Color ThemeText = new(0xE6, 0xE9, 0xEF);
        private static readonly ScottPlot.Color ThemeTextDim = new(0x9A, 0xA3, 0xB2);
        private static readonly ScottPlot.Color ThemeGrid = new(0xFF, 0xFF, 0xFF, 13);
        private static readonly ScottPlot.Color ThemeSplit = new(0x7C, 0x5A, 0x2E);

        public ChartPane(FormsPlot plotControl, ChannelPaneMap channelPaneMap, int paneIndex)
        {
            PlotControl = plotControl ?? throw new ArgumentNullException(nameof(plotControl));
            _channelPaneMap = channelPaneMap ?? throw new ArgumentNullException(nameof(channelPaneMap));
            _paneIndex = paneIndex;

            PlotControl.Plot.Clear();
            PlotControl.Plot.FigureBackground.Color = ThemeWindow;
            PlotControl.Plot.DataBackground.Color = ThemePlotBg;
            PlotControl.Plot.Title(null);
            PlotControl.Plot.Legend.IsVisible = false;

            var x = PlotControl.Plot.Axes.Bottom;
            x.Label.Text = "Time (s)";
            x.Label.FontSize = 12;
            ApplyDarkAxisStyle(x, ThemeTextDim);

            PlotControl.Plot.Grid.XAxisStyle.IsVisible = true;
            PlotControl.Plot.Grid.YAxisStyle.IsVisible = false;
            PlotControl.Plot.Grid.MajorLineColor = ThemeGrid;
            PlotControl.Plot.Grid.MinorLineColor = ThemeGrid;
            PlotControl.Plot.Grid.MajorLineWidth = 1;
            PlotControl.Plot.Grid.MinorLineWidth = 1;
        }

        private const float SidePadding = 24;

        public FormsPlot PlotControl { get; }
        public bool HasScatters => _scatters.Count > 0;
        public bool HasCursor => _cursor is not null;

        public void SetMiniLoggerTyreDiameter(double diameterMm) =>
            _miniLoggerMphPerRevPerSec = MiniLoggerLoader.MphPerRevPerSec(diameterMm);

        // The map is deliberately consulted at draw time. Unknown/future channels safely
        // default to pane 1 instead of disappearing because they were absent from a name list.
        public bool DrawsChannel(string channelName) => _channelPaneMap.GetPaneIndex(channelName) == _paneIndex;

        public (double Left, double Right) GetXLimits()
        {
            var limits = PlotControl.Plot.Axes.GetLimits();
            return (limits.Left, limits.Right);
        }

        public void ClearForPlot()
        {
            PlotControl.Plot.Clear();
            _scatters.Clear();
            _rawYMap.Clear();
            _scatterSlotMap.Clear();
            _channelToScattersCache = null;
            PlotControl.Plot.Axes.Rules.Clear();
            _splitLabelAxis = null;
            _tcSpansBySlot.Clear();

            _cursor = PlotControl.Plot.Add.VerticalLine(0);
            _cursor.LinePattern = LinePattern.Dashed;
            _cursor.Color = ScottPlot.Colors.Red;

            var xAxis = PlotControl.Plot.Axes.Bottom;
            xAxis.IsVisible = true;
            xAxis.Label.IsVisible = true;
            xAxis.TickLabelStyle.IsVisible = true;
            xAxis.MajorTickStyle.Length = 5;
            xAxis.MinorTickStyle.Length = 3;
            xAxis.FrameLineStyle.Width = 1;
        }

        public void SetupAllAxes(string focusedChannel, Func<string, ScottPlot.Color> getTraceColor)
        {
            if (_throttleAxis is null)
            {
                _throttleAxis = PlotControl.Plot.Axes.Left;
                _throttleAxis.Label.Text = "Throttle (%)";
                HideAxis(_throttleAxis);
            }

            if (_rpmAxis is null)
            {
                _rpmAxis = PlotControl.Plot.Axes.AddRightAxis();
                _rpmAxis.Label.Text = "RPM";
                HideAxis(_rpmAxis);
            }

            if (_voltageAxis is null)
            {
                _voltageAxis = PlotControl.Plot.Axes.AddLeftAxis();
                _voltageAxis.Label.Text = "Voltage (V)";
                HideAxis(_voltageAxis);
            }

            if (_currentAxis is null)
            {
                _currentAxis = PlotControl.Plot.Axes.AddRightAxis();
                _currentAxis.Label.Text = "Current (A)";
                HideAxis(_currentAxis);
            }

            if (_rippleAxis is null)
            {
                _rippleAxis = PlotControl.Plot.Axes.AddRightAxis();
                _rippleAxis.Label.Text = "Ripple (V)";
                HideAxis(_rippleAxis);
            }

            if (_powerAxis is null)
            {
                _powerAxis = PlotControl.Plot.Axes.AddLeftAxis();
                _powerAxis.Label.Text = "Power Out (W)";
                HideAxis(_powerAxis);
            }

            if (_escTempAxis is null)
            {
                _escTempAxis = PlotControl.Plot.Axes.AddRightAxis();
                _escTempAxis.Label.Text = "ESC Temp (°C)";
                HideAxis(_escTempAxis);
            }

            if (_motorTempAxis is null)
            {
                _motorTempAxis = PlotControl.Plot.Axes.AddRightAxis();
                _motorTempAxis.Label.Text = "Motor Temp (°C)";
                HideAxis(_motorTempAxis);
            }

            if (_motorTimingAxis is null)
            {
                _motorTimingAxis = PlotControl.Plot.Axes.AddRightAxis();
                _motorTimingAxis.Label.Text = "Motor Timing (deg)";
                HideAxis(_motorTimingAxis);
            }

            if (_accelAxis is null)
            {
                _accelAxis = PlotControl.Plot.Axes.AddRightAxis();
                _accelAxis.Label.Text = "Acceleration (g)";
                HideAxis(_accelAxis);
            }

            if (_raceBoxSpeedAxis is null)
            {
                _raceBoxSpeedAxis = PlotControl.Plot.Axes.AddRightAxis();
                _raceBoxSpeedAxis.Label.Text = "Speed (mph)";
                HideAxis(_raceBoxSpeedAxis);
            }

            if (_raceBoxGxAxis is null)
            {
                _raceBoxGxAxis = PlotControl.Plot.Axes.AddRightAxis();
                _raceBoxGxAxis.Label.Text = "G-Force X (g)";
                HideAxis(_raceBoxGxAxis);
            }

            if (_raceBoxDistanceAxis is null)
            {
                _raceBoxDistanceAxis = PlotControl.Plot.Axes.AddRightAxis();
                _raceBoxDistanceAxis.Label.Text = "Distance (ft)";
                HideAxis(_raceBoxDistanceAxis);
            }

            if (_wheelRevsAxis is null)
            {
                _wheelRevsAxis = PlotControl.Plot.Axes.AddRightAxis();
                _wheelRevsAxis.Label.Text = "Wheel (rev/s)";
                HideAxis(_wheelRevsAxis);
            }

            if (_slipAxis is null)
            {
                _slipAxis = PlotControl.Plot.Axes.AddRightAxis();
                _slipAxis.Label.Text = "Slip (%)";
                HideAxis(_slipAxis);
            }

            if (_tcStateAxis is null)
            {
                _tcStateAxis = PlotControl.Plot.Axes.AddRightAxis();
                _tcStateAxis.Label.Text = "TC State";
                HideAxis(_tcStateAxis);
            }

            ApplyFocusToAxes(focusedChannel, getTraceColor);
        }

        public void EnsureSplitLabelAxis()
        {
            if (_splitLabelAxis is not null)
                return;

            _splitLabelAxis = PlotControl.Plot.Axes.Right;
            HideAxis(_splitLabelAxis);
        }

        public void PlotCastleRun(
            int slot,
            RunData run,
            double castleTimeShift,
            bool isFourPoleMode,
            bool voltageSmoothingEnabled,
            int voltageSmoothingWindow,
            Func<string, ScottPlot.Color> getTraceColor,
            Func<string, float> widthFor,
            IReadOnlyDictionary<string, bool> channelVisibility,
            IReadOnlyDictionary<int, bool> runVisibility)
        {
            if (run.HasChannelData || run.DataPoints == null || run.DataPoints.Count == 0)
                return;

            double shiftSec = (run.TimeShiftMs / 1000.0) + castleTimeShift;
            double[] xs = run.DataPoints.Select(dp => dp.Time + shiftSec).ToArray();

            foreach (var (channelLabel, rawYs) in GetCastleChannels(run))
            {
                if (!DrawsChannel(channelLabel)) continue;

                double[] ysToPlot = rawYs;
                if (isFourPoleMode && channelLabel == "RPM")
                    ysToPlot = rawYs.Select(v => v * 0.5).ToArray();
                if (voltageSmoothingEnabled && channelLabel == "Voltage")
                    ysToPlot = MovingAverage(ysToPlot, voltageSmoothingWindow);

                AddScatter(slot, channelLabel, xs, ysToPlot, getTraceColor, widthFor,
                    channelVisibility, runVisibility);
            }
        }

        public void PlotRaceBoxRun(
            int slot,
            RunData run,
            Func<string, ScottPlot.Color> getTraceColor,
            Func<string, float> widthFor,
            IReadOnlyDictionary<string, bool> channelVisibility,
            IReadOnlyDictionary<int, bool> runVisibility)
        {
            if (!run.IsRaceBox) return;

            foreach (string channel in run.Data.Keys.Where(channel =>
                         channel.StartsWith("RaceBox ", StringComparison.OrdinalIgnoreCase)))
            {
                if (!DrawsChannel(channel)) continue;
                if (!run.Data.TryGetValue(channel, out var points) || points.Count == 0) continue;

                var typedPoints = points.OfType<ModelPoint>().ToList();
                if (typedPoints.Count < 2) continue;

                double shiftSec = run.TimeShiftMs / 1000.0;
                double[] xs = typedPoints.Select(p => p.Time + shiftSec).ToArray();
                double[] ys = typedPoints.Select(p => p.Y).ToArray();
                AddScatter(slot, channel, xs, ys, getTraceColor, widthFor,
                    channelVisibility, runVisibility);
            }
        }

        public void PlotMiniLoggerRun(
            int slot,
            RunData run,
            Func<string, ScottPlot.Color> getTraceColor,
            Func<string, float> widthFor,
            IReadOnlyDictionary<string, bool> channelVisibility,
            IReadOnlyDictionary<int, bool> runVisibility)
        {
            if (!run.IsMiniLogger) return;

            double shiftSec = run.TimeShiftMs / 1000.0;
            bool drawsLoggerData = run.Data.Keys.Any(DrawsChannel);
            foreach (string channel in run.Data.Keys)
            {
                if (!DrawsChannel(channel)) continue;
                if (!run.Data.TryGetValue(channel, out var points) || points.Count < 2) continue;

                double[] xs = points.Select(p => p.Time + shiftSec).ToArray();
                double[] ys = points.Select(p => p.Y).ToArray();
                AddScatter(slot, channel, xs, ys, getTraceColor, widthFor,
                    channelVisibility, runVisibility);
            }

            if (drawsLoggerData)
                AddTcSpans(slot, run, shiftSec, runVisibility);
        }

        private void AddTcSpans(
            int slot,
            RunData run,
            double shiftSec,
            IReadOnlyDictionary<int, bool> runVisibility)
        {
            if (!run.MiniLoggerSamples.Any(sample => sample.TcState.HasValue)) return;

            bool visible = !runVisibility.TryGetValue(slot, out bool runOn) || runOn;
            var spans = new List<VerticalSpan>();
            AddRanges(sample => sample.TcState == 2, new ScottPlot.Color(0x55, 0xB8, 0x73, 30));
            AddRanges(sample => sample.TcState == 2 && sample.ThrottleOutMs.HasValue &&
                                sample.ThrottleOutMs.Value < sample.ThrottleMs,
                new ScottPlot.Color(0xFF, 0x65, 0x57, 75));
            _tcSpansBySlot[slot] = spans;

            void AddRanges(Func<MiniLoggerSample, bool> predicate, ScottPlot.Color color)
            {
                int start = -1;
                for (int i = 0; i <= run.MiniLoggerSamples.Count; i++)
                {
                    bool inRange = i < run.MiniLoggerSamples.Count && predicate(run.MiniLoggerSamples[i]);
                    if (inRange && start < 0)
                    {
                        start = i;
                        continue;
                    }
                    if (inRange || start < 0) continue;

                    int end = i - 1;
                    double left = run.MiniLoggerSamples[start].LogsPageTimeSeconds + shiftSec;
                    double right = i < run.MiniLoggerSamples.Count
                        ? run.MiniLoggerSamples[i].LogsPageTimeSeconds + shiftSec
                        : run.MiniLoggerSamples[end].LogsPageTimeSeconds + shiftSec;
                    if (right > left)
                    {
                        var span = PlotControl.Plot.Add.VerticalSpan(left, right, color);
                        span.LineWidth = 0;
                        span.EnableAutoscale = false;
                        span.IsVisible = visible;
                        spans.Add(span);
                    }
                    start = -1;
                }
            }
        }

        private void AddScatter(
            int slot,
            string channel,
            double[] xs,
            double[] ys,
            Func<string, ScottPlot.Color> getTraceColor,
            Func<string, float> widthFor,
            IReadOnlyDictionary<string, bool> channelVisibility,
            IReadOnlyDictionary<int, bool> runVisibility)
        {
            var scatter = PlotControl.Plot.Add.Scatter(xs, ys);
            scatter.LegendText = channel;
            scatter.Color = getTraceColor(channel);
            scatter.LinePattern = LineStyleHelper.GetLinePattern(slot - 1);
            scatter.LineWidth = widthFor(channel);
            scatter.MarkerSize = 0;
            scatter.Axes.XAxis = PlotControl.Plot.Axes.Bottom;
            scatter.Axes.YAxis = AxisForPlot(channel);

            bool channelOn = channelVisibility.TryGetValue(channel, out bool visible) ? visible : true;
            bool runOn = runVisibility.TryGetValue(slot, out bool runVisible) ? runVisible : true;
            scatter.IsVisible = channelOn && runOn;

            _scatters.Add(scatter);
            _rawYMap[scatter] = ys;
            _scatterSlotMap[scatter] = slot;
            _channelToScattersCache = null;
        }

        private IYAxis AxisForPlot(string channelLabel) => channelLabel switch
        {
            "RPM" => _rpmAxis!,
            "Throttle %" => _throttleAxis!,
            "Voltage" => _voltageAxis!,
            "Current" => _currentAxis!,
            "Ripple" => _rippleAxis!,
            "PowerOut" => _powerAxis!,
            "ESC Temp" => _escTempAxis!,
            "MotorTemp" => _motorTempAxis!,
            "MotorTiming" => _motorTimingAxis!,
            "Acceleration" => _accelAxis!,
            "RaceBox Speed" => _raceBoxSpeedAxis!,
            "RaceBox Distance" => _raceBoxDistanceAxis!,
            "RaceBox G-Force X" => _raceBoxGxAxis!,
            MiniLoggerLoader.ThrottleChannel => _throttleAxis!,
            MiniLoggerLoader.ThrottleOutChannel => _throttleAxis!,
            MiniLoggerLoader.TcStateChannel => _tcStateAxis!,
            MiniLoggerLoader.RpmChannel => _rpmAxis!,
            MiniLoggerLoader.WheelSpeedChannel => _raceBoxSpeedAxis!,
            MiniLoggerLoader.RearSpeedChannel => _raceBoxSpeedAxis!,
            MiniLoggerLoader.SlipChannel => _slipAxis!,
            MiniLoggerLoader.WheelRevsChannel => _wheelRevsAxis!,
            _ => _throttleAxis!,
        };

        // Side scales are never shown (Stewart: "i dont want any scale"); values come from the
        // cursor box and the channel list. The axes still exist for their locks and line sizes.
        public void ApplyFocusToAxes(string focusedChannel, Func<string, ScottPlot.Color> getTraceColor)
        {
            foreach (IAxis? axis in KnownAxes())
                if (axis is not null) HideAxis(axis);
        }

        public void UpdateTraceColor(string channelName, ScottPlot.Color color)
        {
            foreach (var scatter in _scatters)
                if (string.Equals(scatter.LegendText, channelName, StringComparison.OrdinalIgnoreCase))
                    scatter.Color = color;
        }

        public void UpdateLineWidth(string channelName, float width)
        {
            foreach (var scatter in _scatters)
                if (string.Equals(scatter.LegendText, channelName, StringComparison.OrdinalIgnoreCase))
                    scatter.LineWidth = width;
        }

        public void UpdateAllTraceStyles(Func<string, ScottPlot.Color> getTraceColor, Func<string, float> widthFor)
        {
            foreach (var scatter in _scatters)
            {
                scatter.Color = getTraceColor(scatter.LegendText);
                scatter.LineWidth = widthFor(scatter.LegendText);
            }
        }

        public void SetRunVisibility(int slot, bool isVisible, IReadOnlyDictionary<string, bool> channelVisibility)
        {
            foreach (var scatter in _scatters)
            {
                if (!_scatterSlotMap.TryGetValue(scatter, out int scatterSlot) || scatterSlot != slot)
                    continue;

                bool channelOn = channelVisibility.TryGetValue(scatter.LegendText, out bool visible) ? visible : true;
                scatter.IsVisible = isVisible && channelOn;
            }

            SetSplitVisibility(slot, isVisible);
            SetTcSpanVisibility(slot, isVisible);
        }

        public void SetChannelVisibility(string channelName, bool isVisible, IReadOnlyDictionary<int, bool> runVisibility)
        {
            foreach (var scatter in _scatters)
            {
                if (scatter.LegendText != channelName || !_scatterSlotMap.TryGetValue(scatter, out int slot))
                    continue;

                bool runOn = runVisibility.TryGetValue(slot, out bool visible) ? visible : true;
                scatter.IsVisible = runOn && isVisible;
            }
        }

        public void RemoveSlot(int slot)
        {
            var toRemove = _scatterSlotMap
                .Where(pair => pair.Value == slot)
                .Select(pair => pair.Key)
                .ToList();

            foreach (var scatter in toRemove)
            {
                PlotControl.Plot.Remove(scatter);
                _scatters.Remove(scatter);
                _scatterSlotMap.Remove(scatter);
                _rawYMap.Remove(scatter);
            }

            if (toRemove.Count > 0)
                _channelToScattersCache = null;

            RemovePreviousSplitLines(slot);
            RemovePreviousSplitLabels(slot);
            RemoveTcSpans(slot);
        }

        private void SetTcSpanVisibility(int slot, bool visible)
        {
            if (_tcSpansBySlot.TryGetValue(slot, out var spans))
                foreach (var span in spans) span.IsVisible = visible;
        }

        private void RemoveTcSpans(int slot)
        {
            if (!_tcSpansBySlot.TryGetValue(slot, out var spans)) return;
            foreach (var span in spans) PlotControl.Plot.Remove(span);
            _tcSpansBySlot.Remove(slot);
        }

        public void AddRaceBoxSplitLines(int slot, List<double>? splitTimes, List<string>? splitLabels, bool includeZero)
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

            var labelBg = new ScottPlot.Color(0x1B, 0x21, 0x2B);
            var lines = new List<VerticalLine>();
            foreach (double time in times)
            {
                var line = PlotControl.Plot.Add.VerticalLine(time);
                line.LinePattern = LineStyleHelper.GetLinePattern(99);
                line.LineWidth = 1.5f;
                line.Color = ThemeSplit;
                lines.Add(line);
            }
            _splitLinesBySlot[slot] = lines;

            var plotLabels = new List<Text>();
            for (int i = 0; i < times.Count; i++)
            {
                string text = i < labels.Count && !string.IsNullOrWhiteSpace(labels[i]) ? labels[i] : $"Split {i}";
                var label = PlotControl.Plot.Add.Text(text, times[i], 0.95);
                label.Axes.YAxis = _splitLabelAxis!;
                label.Alignment = Alignment.UpperCenter;
                label.LabelFontSize = 11;
                label.LabelFontColor = ThemeText;
                label.LabelBackgroundColor = labelBg;
                label.LabelBorderColor = ThemeSplit;
                label.LabelBorderWidth = 1;
                label.OffsetY = -2;
                plotLabels.Add(label);
            }
            _splitLabelsBySlot[slot] = plotLabels;

            PlotControl.Refresh();
        }

        public void SetSplitVisibility(int slot, bool visible)
        {
            if (_splitLinesBySlot.TryGetValue(slot, out var lines))
                foreach (var line in lines) line.IsVisible = visible;

            if (_splitLabelsBySlot.TryGetValue(slot, out var labels))
                foreach (var label in labels) label.IsVisible = visible;
        }

        private void RemovePreviousSplitLines(int slot)
        {
            if (!_splitLinesBySlot.TryGetValue(slot, out var lines)) return;
            foreach (var line in lines) PlotControl.Plot.Remove(line);
            _splitLinesBySlot.Remove(slot);
        }

        private void RemovePreviousSplitLabels(int slot)
        {
            if (!_splitLabelsBySlot.TryGetValue(slot, out var labels)) return;
            foreach (var label in labels) PlotControl.Plot.Remove(label);
            _splitLabelsBySlot.Remove(slot);
        }

        public IReadOnlyDictionary<string, List<Scatter>> GetChannelToScatters()
        {
            if (_channelToScattersCache is not null)
                return _channelToScattersCache;

            var cache = new Dictionary<string, List<Scatter>>();
            foreach (var scatter in _scatters)
            {
                if (!cache.TryGetValue(scatter.LegendText, out var list))
                {
                    list = new List<Scatter>();
                    cache[scatter.LegendText] = list;
                }
                list.Add(scatter);
            }
            _channelToScattersCache = cache;
            return cache;
        }

        public bool TryGetScatterSlot(Scatter scatter, out int slot) => _scatterSlotMap.TryGetValue(scatter, out slot);
        public bool TryGetPlottedValues(Scatter scatter, out double[] values) => _rawYMap.TryGetValue(scatter, out values!);

        public void SetCursorX(double x)
        {
            if (_cursor is not null)
                _cursor.X = x;
        }

        public void ApplyTimeRangeAndTicks(Dictionary<int, RunData> runsBySlot, double castleTimeShift, double minX, double maxX)
        {
            ApplyManualTimeTicks(runsBySlot, castleTimeShift);
            // Every pane uses identical margins so linked time coordinates line up vertically.
            // No side scales are drawn, so the sides only leave room for the end time labels.
            PixelPadding padding = new(left: SidePadding, right: SidePadding, top: 0, bottom: 50);
            PlotControl.Plot.Layout.Fixed(padding);
            PlotControl.Plot.Legend.IsVisible = false;

            var limits = PlotControl.Plot.Axes.GetLimits();
            PlotControl.Plot.Axes.SetLimits(minX, maxX, limits.Bottom, limits.Top);
        }

        public (double MinX, double MaxX) GetTimeRange(Dictionary<int, RunData> runsBySlot, double castleTimeShift)
        {
            double minX = double.PositiveInfinity;
            double maxX = double.NegativeInfinity;

            foreach (var pair in runsBySlot)
            {
                RunData run = pair.Value;
                if (run == null) continue;

                double shiftSec = (run.TimeShiftMs / 1000.0) + (run.HasChannelData ? 0.0 : castleTimeShift);
                if (!run.IsRaceBox)
                {
                    if (run.DataPoints == null || run.DataPoints.Count == 0) continue;
                    minX = Math.Min(minX, run.DataPoints.First().Time + shiftSec);
                    maxX = Math.Max(maxX, run.DataPoints.Last().Time + shiftSec);
                }
                else if (run.Data != null && run.Data.TryGetValue("RaceBox Speed", out var points) && points.Count > 0)
                {
                    minX = Math.Min(minX, points.First().Time + shiftSec);
                    maxX = Math.Max(maxX, points.Last().Time + shiftSec);
                }
            }

            return !double.IsFinite(minX) || !double.IsFinite(maxX) || minX >= maxX
                ? (-1.0, 10.0)
                : (minX, maxX);
        }

        private void ApplyManualTimeTicks(Dictionary<int, RunData> runsBySlot, double castleTimeShift)
        {
            (double minX, double maxX) = GetTimeRange(runsBySlot, castleTimeShift);
            minX -= 0.10;
            maxX += 0.10;

            if (maxX - minX > 6.0)
            {
                PlotControl.Plot.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericAutomatic();
                return;
            }

            var manual = new ScottPlot.TickGenerators.NumericManual();
            for (double pos = Math.Ceiling(minX / 0.25) * 0.25; pos <= maxX; pos += 0.25)
                manual.AddMajor(pos, pos.ToString("0.00"));
            for (double pos = Math.Ceiling(minX / 0.05) * 0.05; pos <= maxX; pos += 0.05)
                manual.AddMinor(pos);

            PlotControl.Plot.Axes.Bottom.TickGenerator = manual;
            PlotControl.Plot.Grid.XAxisStyle.IsVisible = true;
            PlotControl.Plot.Grid.YAxisStyle.IsVisible = false;
            PlotControl.Plot.Grid.XAxisStyle.MajorLineStyle.Color = ScottPlot.Colors.Black.WithAlpha(40);
            PlotControl.Plot.Grid.XAxisStyle.MajorLineStyle.Width = 1;
            PlotControl.Plot.Grid.XAxisStyle.MinorLineStyle.Color = ScottPlot.Colors.Gray.WithAlpha(25);
            PlotControl.Plot.Grid.XAxisStyle.MinorLineStyle.Width = 0.5f;
        }

        public void ReapplyAxisLocks(
            IReadOnlyDictionary<string, (double Min, double Max)> scales,
            bool isFourPoleMode,
            IReadOnlyDictionary<string, (double Height, double Position)> channelBands)
        {
            var knownAxes = new HashSet<IAxis>(KnownAxes().Append(_splitLabelAxis).Where(axis => axis is not null)!);
            var toRemove = new List<IAxisRule>();
            foreach (var rule in PlotControl.Plot.Axes.Rules)
                if (rule is LockedVertical locked && knownAxes.Contains(locked.YAxis))
                    toRemove.Add(rule);
            foreach (var rule in toRemove)
                PlotControl.Plot.Axes.Rules.Remove(rule);

            AddLock(_raceBoxSpeedAxis, 0, 110, channelBands);
            AddLock(_raceBoxGxAxis, -5, 7, channelBands);
            AddLock(_raceBoxDistanceAxis, 0, 1000, channelBands);
            AddLock(_wheelRevsAxis, 0, 110 / _miniLoggerMphPerRevPerSec, channelBands);
            AddLock(_slipAxis, -20, 100, channelBands);
            AddLock(_tcStateAxis, -0.5, 4.5, channelBands);
            if (_splitLabelAxis is not null)
                PlotControl.Plot.Axes.Rules.Add(new LockedVertical(_splitLabelAxis, 0.0, 1.0));

            ApplyAxisLocksForActiveProfile(scales, isFourPoleMode, channelBands);
        }

        private void ApplyAxisLocksForActiveProfile(
            IReadOnlyDictionary<string, (double Min, double Max)> scales,
            bool isFourPoleMode,
            IReadOnlyDictionary<string, (double Height, double Position)> channelBands)
        {
            (double Min, double Max) Scale(string channel, (double Min, double Max) fallback) =>
                scales.TryGetValue(channel, out var range) ? range : fallback;

            var throttle = Scale("Throttle %", (-100, 120));
            AddLock(_throttleAxis, throttle.Min, throttle.Max, channelBands);

            var rpm = Scale("RPM", (0.0, isFourPoleMode ? 100_000.0 : 200_000.0));
            AddLock(_rpmAxis, rpm.Min, rpm.Max, channelBands);

            var voltage = Scale("Voltage", (6.0, 9.0));
            AddLock(_voltageAxis, voltage.Min, voltage.Max, channelBands);

            var current = Scale("Current", (0.0, 800.0));
            AddLock(_currentAxis, current.Min, current.Max, channelBands);

            var ripple = Scale("Ripple", (0.0, 5.0));
            AddLock(_rippleAxis, ripple.Min, ripple.Max, channelBands);

            var power = Scale("PowerOut", (0.0, 120.0));
            AddLock(_powerAxis, power.Min, power.Max, channelBands);

            var escTemp = Scale("ESC Temp", (20.0, 120.0));
            AddLock(_escTempAxis, escTemp.Min, escTemp.Max, channelBands);

            var motorTemp = Scale("MotorTemp", (20.0, 120.0));
            AddLock(_motorTempAxis, motorTemp.Min, motorTemp.Max, channelBands);

            var motorTiming = Scale("MotorTiming", (0.0, 120.0));
            AddLock(_motorTimingAxis, motorTiming.Min, motorTiming.Max, channelBands);

            var acceleration = Scale("Acceleration", (-5.0, 7.0));
            AddLock(_accelAxis, acceleration.Min, acceleration.Max, channelBands);
        }

        private void AddLock(
            IYAxis? axis,
            double min,
            double max,
            IReadOnlyDictionary<string, (double Height, double Position)> channelBands)
        {
            if (axis is null) return;

            foreach (var (channel, band) in channelBands)
            {
                if (!ReferenceEquals(AxisFor(channel), axis)) continue;

                var range = BandAxisRange(min, max, band.Height, band.Position);
                PlotControl.Plot.Axes.Rules.Add(new LockedVertical(axis, range.Min, range.Max));
                return;
            }

            PlotControl.Plot.Axes.Rules.Add(new LockedVertical(axis, min, max));
        }

        internal static (double Min, double Max) BandAxisRange(double min, double max, double height, double position)
        {
            double bottom = position * (1.0 - height);
            double axisSpan = (max - min) / height;
            double axisMin = min - bottom * axisSpan;
            return (axisMin, axisMin + axisSpan);
        }

        public void ResetEmptyPlot(
            IReadOnlyDictionary<string, (double Min, double Max)> scales,
            bool isFourPoleMode,
            IReadOnlyDictionary<string, (double Height, double Position)> channelBands)
        {
            PlotControl.Plot.Clear();
            PlotControl.Plot.Axes.Rules.Clear();
            PlotControl.Plot.Grid.XAxisStyle.IsVisible = false;
            PlotControl.Plot.Grid.YAxisStyle.IsVisible = false;

            var axes = new List<IAxis?> { PlotControl.Plot.Axes.Bottom, PlotControl.Plot.Axes.Left };
            axes.AddRange(KnownAxes());
            foreach (IAxis axis in axes.Where(axis => axis is not null)!)
            {
                axis.IsVisible = false;
                axis.Label.IsVisible = false;
                axis.TickLabelStyle.IsVisible = false;
                axis.MajorTickStyle.Length = 0;
                axis.MinorTickStyle.Length = 0;
                axis.FrameLineStyle.Width = 0;
            }

            var message = PlotControl.Plot.Add.Text("Waiting for log...", 0, 0);
            message.Alignment = Alignment.MiddleCenter;
            message.LabelFontSize = 18;
            message.LabelFontColor = ThemeTextDim;

            PixelPadding padding = new(left: SidePadding, right: SidePadding, top: 10, bottom: 50);
            PlotControl.Plot.Layout.Fixed(padding);
            PlotControl.Plot.Legend.IsVisible = false;
            ReapplyAxisLocks(scales, isFourPoleMode, channelBands);
            PlotControl.Refresh();
        }

        private IAxis? AxisFor(string channelLabel) => channelLabel switch
        {
            "RPM" => _rpmAxis,
            "Throttle %" => _throttleAxis,
            "Voltage" => _voltageAxis,
            "Current" => _currentAxis,
            "Ripple" => _rippleAxis,
            "PowerOut" => _powerAxis,
            "ESC Temp" => _escTempAxis,
            "MotorTemp" => _motorTempAxis,
            "MotorTiming" => _motorTimingAxis,
            "Acceleration" => _accelAxis,
            "RaceBox Speed" => _raceBoxSpeedAxis,
            "RaceBox Distance" => _raceBoxDistanceAxis,
            "RaceBox G-Force X" => _raceBoxGxAxis,
            MiniLoggerLoader.ThrottleChannel => _throttleAxis,
            MiniLoggerLoader.ThrottleOutChannel => _throttleAxis,
            MiniLoggerLoader.TcStateChannel => _tcStateAxis,
            MiniLoggerLoader.RpmChannel => _rpmAxis,
            MiniLoggerLoader.WheelSpeedChannel => _raceBoxSpeedAxis,
            MiniLoggerLoader.RearSpeedChannel => _raceBoxSpeedAxis,
            MiniLoggerLoader.SlipChannel => _slipAxis,
            MiniLoggerLoader.WheelRevsChannel => _wheelRevsAxis,
            _ => null,
        };

        private IEnumerable<IAxis?> KnownAxes()
        {
            yield return _throttleAxis;
            yield return _rpmAxis;
            yield return _voltageAxis;
            yield return _currentAxis;
            yield return _rippleAxis;
            yield return _powerAxis;
            yield return _escTempAxis;
            yield return _motorTempAxis;
            yield return _motorTimingAxis;
            yield return _accelAxis;
            yield return _raceBoxSpeedAxis;
            yield return _raceBoxGxAxis;
            yield return _raceBoxDistanceAxis;
            yield return _wheelRevsAxis;
            yield return _slipAxis;
            yield return _tcStateAxis;
        }

        private static IEnumerable<(string Label, double[] Values)> GetCastleChannels(RunData run)
        {
            double[] Values(Func<ModelPoint, double> selector) => run.DataPoints.Select(selector).ToArray();

            yield return ("RPM", Values(point => point.Speed));
            yield return ("Throttle %", Values(point => point.ThrottlePercent));
            yield return ("Voltage", Values(point => point.Voltage));
            yield return ("Current", Values(point => point.Current));
            yield return ("Ripple", Values(point => point.Ripple));
            yield return ("PowerOut", Values(point => point.PowerOut));
            yield return ("ESC Temp", Values(point => point.Temperature));
            yield return ("MotorTemp", Values(point => point.MotorTemp));
            yield return ("MotorTiming", Values(point => point.MotorTiming));
            yield return ("Acceleration", Values(point => point.Acceleration));
        }

        private static double[] MovingAverage(double[] source, int window)
        {
            if (source.Length == 0 || window <= 1) return source;

            int half = window / 2;
            var result = new double[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                int first = Math.Max(0, i - half);
                int last = Math.Min(source.Length - 1, i + half);
                double sum = 0;
                for (int j = first; j <= last; j++) sum += source[j];
                result[i] = sum / (last - first + 1);
            }
            return result;
        }

        private static void ApplyDarkAxisStyle(IAxis axis, ScottPlot.Color color)
        {
            axis.Label.ForeColor = color;
            axis.TickLabelStyle.ForeColor = color;
            axis.MajorTickStyle.Color = color;
            axis.MinorTickStyle.Color = color;
            axis.FrameLineStyle.Color = color;
        }

        private static void HideAxis(IAxis axis)
        {
            // An axis is also a layout panel in ScottPlot 5. Keeping an undecorated panel
            // visible still reserves its outward-stacked position and can push a later
            // focused axis beyond the fixed plot padding. Panel visibility does not remove
            // the axis, so its plottables and LockedVertical rules continue to use it.
            axis.IsVisible = false;
            axis.Label.IsVisible = false;
            axis.TickLabelStyle.IsVisible = false;
            axis.MajorTickStyle.Length = 0;
            axis.MinorTickStyle.Length = 0;
            axis.FrameLineStyle.Width = 0;
        }
    }
}
