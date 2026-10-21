using CastleOverlayV2.Models;
using CastleOverlayV2.Services;
using CastleOverlayV2.Utils;
using ScottPlot;
using ScottPlot.WinForms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace CastleOverlayV2.Plot
{
    /// <summary>Coordinates shared plot state and the chart panes that render it.</summary>
    public class PlotManager
    {
        private readonly FormsPlot _plot;
        private readonly Controls.ChartStack _chartStack;
        private readonly List<ChartPane> _panes = new();
        private readonly Dictionary<FormsPlot, ChartPane> _paneCache = new();
        private readonly ChannelPaneMap _channelPaneMap;

        private bool _alignmentMode;
        private bool _alignmentDragging;
        private double _lastAlignmentMouseX;

        private readonly Controls.CursorReadout _readout = new();
        private readonly HashSet<string> _readoutChannels = new(StringComparer.OrdinalIgnoreCase);

        private string _focusedChannel = "RPM";
        private const float FocusedLineWidth = 2.0f;
        private const float BoldLineWidth = 4.0f;

        private readonly Dictionary<string, ScottPlot.Color> _colorOverrides = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _boldChannels = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, (double Height, double Position)> _channelBands = new(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<int, bool> _runVisibility = new();
        private Dictionary<string, bool> _channelVisibility = new();
        private readonly Dictionary<int, RunData> _runsBySlot = new();
        public IReadOnlyDictionary<int, RunData> Runs => _runsBySlot;

        public event Action<Dictionary<string, double?[]>> CursorMoved;
        public event Action<double>? AlignmentDragged;
        public event Action<double>? TrimBeforeRequested;
        public event Action<double>? TrimAfterRequested;
        public event Action? TrimResetRequested;
        public event Action<IReadOnlyList<double>>? PaneHeightsChanged;

        private ContextMenuStrip? _trimMenu;
        private ToolStripMenuItem? _trimBeforeItem;
        private ToolStripMenuItem? _trimAfterItem;
        private ToolStripMenuItem? _trimResetItem;
        private bool _manualTrimAvailable;
        private double _lastRightClickPlotX;

        private bool _isFourPoleMode;
        private bool _voltageSmoothingEnabled;
        private int _voltageSmoothingWindow = 5;

        private enum ScaleProfile { Drag, SpeedRun }
        private ScaleProfile _activeProfile = ScaleProfile.Drag;

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

        private double _castleTimeShift;

        public PlotManager(FormsPlot plotControl)
        {
            _plot = plotControl ?? throw new ArgumentNullException(nameof(plotControl));
            _chartStack = new Controls.ChartStack(_plot);
            _channelPaneMap = new ChannelPaneMap(ChannelText.Order);
            _chartStack.PaneHeightsChanged += heights => PaneHeightsChanged?.Invoke(heights);

            RebuildPaneObjects();

            ReapplyAxisLocks();
            _plot.Refresh();

            BuildTrimMenu();
            _plot.Refresh();
        }

        public Control ChartHost => _chartStack;
        public int PaneCount => _channelPaneMap.PaneCount;
        public IReadOnlyList<double> PaneHeights => _chartStack.PaneHeights;
        public IReadOnlyDictionary<string, int> ChannelPanes => _channelPaneMap.GetAssignments();

        public void SetPaneCount(int paneCount)
        {
            if (paneCount == PaneCount) return;

            _channelPaneMap.SetPaneCount(paneCount);
            _chartStack.SetPaneCount(paneCount);
            RebuildPaneObjects();
            ReplotCurrentState();
        }

        public void SetChannelPane(string channelName, int paneNumber)
        {
            _channelPaneMap.Assign(channelName, paneNumber - 1);
            ReplotCurrentState();
        }

        public int MoveChannelToNextPane(string channelName)
        {
            int paneNumber = _channelPaneMap.MoveToNextPane(channelName) + 1;
            ReplotCurrentState();
            return paneNumber;
        }

        public void ApplyBySourcePanes()
        {
            _channelPaneMap.ApplyBySource();
            _chartStack.SetPaneCount(3);
            RebuildPaneObjects();
            ReplotCurrentState();
        }

        public void ApplyPaneState(
            int paneCount,
            IReadOnlyDictionary<string, int>? channelPanes,
            IEnumerable<double>? paneHeights)
        {
            paneCount = Math.Clamp(paneCount, 1, 3);
            _channelPaneMap.SetPaneCount(paneCount);
            _channelPaneMap.ApplyAssignments(channelPanes);
            _chartStack.SetPaneCount(paneCount);
            _chartStack.SetPaneHeights(paneHeights);
            RebuildPaneObjects();
            ReplotCurrentState();
        }

        public void SetPaneHeights(IEnumerable<double>? heights) => _chartStack.SetPaneHeights(heights);

        private void RebuildPaneObjects()
        {
            foreach (ChartPane pane in _panes)
            {
                pane.PlotControl.Plot.Axes.UnlinkAll();
                pane.PlotControl.MouseMove -= FormsPlot_MouseMove;
                pane.PlotControl.MouseLeave -= FormsPlot_MouseLeave;
                pane.PlotControl.MouseDown -= FormsPlot_MouseDown;
                pane.PlotControl.MouseUp -= FormsPlot_MouseUp;
            }

            _readout.Parent?.Controls.Remove(_readout);
            _panes.Clear();
            for (int paneIndex = 0; paneIndex < _chartStack.PaneCount; paneIndex++)
            {
                FormsPlot plotControl = _chartStack.GetPaneControl(paneIndex);
                if (!_paneCache.TryGetValue(plotControl, out ChartPane? pane))
                {
                    pane = new ChartPane(plotControl, _channelPaneMap, paneIndex);
                    _paneCache[plotControl] = pane;
                }
                AddPane(pane);
            }

            _panes[0].PlotControl.Controls.Add(_readout);
            _readout.BringToFront();
            BuildTrimMenu();
            RelinkTimeAxes();
        }

        private void ReplotCurrentState()
        {
            if (_runsBySlot.Count > 0)
                PlotRuns(new Dictionary<int, RunData>(_runsBySlot));
            else
                ResetEmptyPlot();
        }

        private void AddPane(ChartPane pane)
        {
            _panes.Add(pane);
            try { pane.PlotControl.Menu?.Clear(); } catch { }
            pane.PlotControl.MouseMove += FormsPlot_MouseMove;
            pane.PlotControl.MouseLeave += FormsPlot_MouseLeave;
            pane.PlotControl.MouseDown += FormsPlot_MouseDown;
            pane.PlotControl.MouseUp += FormsPlot_MouseUp;
        }

        private void RelinkTimeAxes()
        {
            // ScottPlot 5.0.55 keeps links in LinkedAxisRules (separate from the vertical
            // lock Rules collection). Recreate the complete ordered graph anyway because
            // panes can be removed/re-added, and UnlinkAll prevents duplicate rules after
            // every replot. Link rules copy limits directly and do not recursively fire.
            foreach (ChartPane pane in _panes)
                pane.PlotControl.Plot.Axes.UnlinkAll();

            for (int source = 0; source < _panes.Count; source++)
                for (int target = 0; target < _panes.Count; target++)
                    if (source != target)
                        _panes[source].PlotControl.Plot.Axes.Link(
                            _panes[target].PlotControl, x: true, y: false);
        }

        /// <summary>Which channels the cursor box shows (picked in the channel list).</summary>
        public void SetReadoutChannels(IEnumerable<string> channels)
        {
            _readoutChannels.Clear();
            foreach (string channel in channels) _readoutChannels.Add(channel);
            if (_readoutChannels.Count == 0) _readout.Visible = false;
        }

        /// <summary>Configure voltage smoothing without mutating source data.</summary>
        public void SetVoltageSmoothing(bool enabled, int window)
        {
            _voltageSmoothingEnabled = enabled;
            if (window < 3) window = 3;
            if (window > 15) window = 15;
            if (window % 2 == 0) window += 1;
            _voltageSmoothingWindow = window;

            if (_runsBySlot.Count > 0)
                PlotRuns(new Dictionary<int, RunData>(_runsBySlot));
        }

        public void SetCastleTimeShift(double shiftSeconds)
        {
            _castleTimeShift = shiftSeconds;
            Logger.Log($"⏱️ Castle Time Shift set to {shiftSeconds:F3} seconds");
        }

        public void SetSpeedMode(bool isSpeedRun)
        {
            _activeProfile = isSpeedRun ? ScaleProfile.SpeedRun : ScaleProfile.Drag;
            Logger.Log($"📐 Scale Profile → {(_activeProfile == ScaleProfile.SpeedRun ? "Speed-Run" : "Drag")}");
        }

        /// <summary>Set per-channel Y-scale for current profile (or explicitly for Drag/Speed-Run).</summary>
        public void SetChannelScale(string channel, double min, double max, bool? forSpeedRun = null)
        {
            if (string.IsNullOrWhiteSpace(channel)) return;

            var scales = (forSpeedRun ?? (_activeProfile == ScaleProfile.SpeedRun))
                ? _speedScales
                : _dragScales;

            scales[channel] = (min, max);
            Logger.Log($"📏 SetChannelScale[{(scales == _speedScales ? "Speed-Run" : "Drag")}]: {channel} = ({min}, {max})");
            ReapplyAxisLocks();
            RefreshPlot();
        }

        private void BuildTrimMenu()
        {
            foreach (ChartPane pane in _panes)
                try { pane.PlotControl.Menu?.Clear(); } catch { }

            _trimMenu?.Dispose();
            _trimMenu = new ContextMenuStrip();
            _trimBeforeItem = new ToolStripMenuItem("Remove data before here",
                null, (_, _) => TrimBeforeRequested?.Invoke(_lastRightClickPlotX));
            _trimAfterItem = new ToolStripMenuItem("Remove data after here",
                null, (_, _) => TrimAfterRequested?.Invoke(_lastRightClickPlotX));
            _trimResetItem = new ToolStripMenuItem("Reset trim",
                null, (_, _) => TrimResetRequested?.Invoke());

            _trimMenu.Items.Add(_trimBeforeItem);
            _trimMenu.Items.Add(_trimAfterItem);
            _trimMenu.Items.Add(new ToolStripSeparator());
            _trimMenu.Items.Add(_trimResetItem);
        }

        public void SetManualTrimAvailable(bool available)
        {
            _manualTrimAvailable = available;
        }

        public string FocusedChannel => _focusedChannel;

        public void SetFocusedChannel(string channelName)
        {
            if (_focusedChannel == channelName) return;
            _focusedChannel = channelName;

            foreach (ChartPane pane in _panes)
            {
                pane.UpdateAllTraceStyles(GetTraceColor, WidthFor);
                pane.ApplyFocusToAxes(_focusedChannel, GetTraceColor);
            }
            RefreshPlot();
        }

        private ScottPlot.Color GetTraceColor(string channelLabel) =>
            _colorOverrides.TryGetValue(channelLabel, out var color)
                ? color
                : ChannelColorMap.GetColor(channelLabel);

        private float WidthFor(string channelLabel) =>
            _boldChannels.Contains(channelLabel) ? BoldLineWidth : FocusedLineWidth;

        public void SetChannelColor(string channelName, System.Drawing.Color? color)
        {
            if (color is { } selected)
                _colorOverrides[channelName] = new ScottPlot.Color(selected.R, selected.G, selected.B);
            else
                _colorOverrides.Remove(channelName);

            ScottPlot.Color traceColor = GetTraceColor(channelName);
            foreach (ChartPane pane in _panes)
            {
                pane.UpdateTraceColor(channelName, traceColor);
                pane.ApplyFocusToAxes(_focusedChannel, GetTraceColor);
            }
            RefreshPlot();
        }

        public void SetChannelBold(string channelName, bool bold)
        {
            if (bold) _boldChannels.Add(channelName);
            else _boldChannels.Remove(channelName);

            foreach (ChartPane pane in _panes)
                pane.UpdateLineWidth(channelName, WidthFor(channelName));
            RefreshPlot();
        }

        public void SetChannelBand(string channelName, double height, double position)
        {
            height = Math.Clamp(height, 0.1, 1.0);
            position = Math.Clamp(position, 0.0, 1.0);
            if (height >= 1.0)
                _channelBands.Remove(channelName);
            else
                _channelBands[channelName] = (height, position);

            ReapplyAxisLocks();
            RefreshPlot();
        }

        public static (double Min, double Max) BandAxisRange(double min, double max, double height, double position) =>
            ChartPane.BandAxisRange(min, max, height, position);

        public void SetFourPoleMode(bool isFourPole)
        {
            _isFourPoleMode = isFourPole;
            Logger.Log($"⚙️ Four-Pole Mode: {(_isFourPoleMode ? "ON" : "OFF")}");
            ReapplyAxisLocks();
            RefreshPlot();
        }

        public bool GetRunVisibility(int slot) =>
            _runVisibility.TryGetValue(slot, out bool visible) ? visible : true;

        public void SetRunVisibility(int slot, bool isVisible)
        {
            _runVisibility[slot] = isVisible;
            Logger.Log($"SetRunVisibility: slot {slot} → {(isVisible ? "Visible" : "Hidden")}");
        }

        public bool ToggleRunVisibility(int slot)
        {
            bool current = _runVisibility.TryGetValue(slot, out bool visible) ? visible : true;
            bool next = !current;
            _runVisibility[slot] = next;

            foreach (ChartPane pane in _panes)
                pane.SetRunVisibility(slot, next, _channelVisibility);

            RefreshPlot();
            return next;
        }

        public void SetChannelVisibility(string channelName, bool isVisible)
        {
            _channelVisibility[channelName] = isVisible;
            foreach (ChartPane pane in _panes)
                pane.SetChannelVisibility(channelName, isVisible, _runVisibility);
            RefreshPlot();
        }

        public void SetInitialChannelVisibility(Dictionary<string, bool> visibilityMap)
        {
            if (Logger.IsEnabled)
            {
                Logger.Log("🟢 SetInitialChannelVisibility():");
                foreach (var pair in visibilityMap)
                    Logger.Log($"   • {pair.Key} = {(pair.Value ? "Visible" : "Hidden")}");
            }
            _channelVisibility = visibilityMap ?? new();
        }

        public void RefreshPlot()
        {
            foreach (ChartPane pane in _panes)
                pane.PlotControl.Refresh();
        }

        public void SetAlignmentMode(bool enabled)
        {
            _alignmentMode = enabled;
            _alignmentDragging = false;
            foreach (ChartPane pane in _panes)
            {
                pane.PlotControl.Cursor = Cursors.Default;
                pane.PlotControl.UserInputProcessor.Enable();
            }
        }

        public void FitToData()
        {
            foreach (ChartPane pane in _panes)
            {
                pane.PlotControl.Refresh();
                pane.PlotControl.Plot.Axes.AutoScale();
            }
        }

        public void SetRun(int slot, RunData run)
        {
            _runsBySlot[slot] = run;
            _runVisibility[slot] = run != null;

            if (run == null)
                foreach (ChartPane pane in _panes)
                    pane.RemoveSlot(slot);
        }

        public void LoadRun(RunData run)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            PlotRuns(new Dictionary<int, RunData> { [1] = run });
        }

        public void PlotRuns(Dictionary<int, RunData> runsBySlot)
        {
            if (runsBySlot == null || runsBySlot.Count == 0)
            {
                ResetEmptyPlot();
                return;
            }

            (double previousLeft, double previousRight) = _panes[0].GetXLimits();
            bool preserveAlignmentZoom =
                _alignmentMode &&
                _panes.Any(pane => pane.HasScatters) &&
                double.IsFinite(previousLeft) &&
                double.IsFinite(previousRight) &&
                previousRight > previousLeft;

            foreach (ChartPane pane in _panes)
            {
                pane.ClearForPlot();
                pane.SetupAllAxes(_focusedChannel, GetTraceColor);
                pane.EnsureSplitLabelAxis();
            }

            foreach (int slot in runsBySlot.Keys)
                if (!_runVisibility.ContainsKey(slot))
                    _runVisibility[slot] = true;

            foreach (var pair in runsBySlot)
            {
                int slot = pair.Key;
                RunData run = pair.Value;
                if (run == null || run.HasChannelData) continue;

                foreach (ChartPane pane in _panes)
                    pane.PlotCastleRun(slot, run, _castleTimeShift, _isFourPoleMode,
                        _voltageSmoothingEnabled, _voltageSmoothingWindow,
                        GetTraceColor, WidthFor, _channelVisibility, _runVisibility);
            }

            foreach (var pair in runsBySlot)
            {
                int slot = pair.Key;
                RunData run = pair.Value;
                if (run == null || !run.HasChannelData) continue;

                foreach (ChartPane pane in _panes)
                {
                    if (run.IsMiniLogger)
                        pane.PlotMiniLoggerRun(slot, run, GetTraceColor, WidthFor,
                            _channelVisibility, _runVisibility);
                    else
                        pane.PlotRaceBoxRun(slot, run, GetTraceColor, WidthFor,
                            _channelVisibility, _runVisibility);

                    if (run.SplitTimes?.Count > 0)
                        pane.AddRaceBoxSplitLines(slot, run.SplitTimes, run.SplitLabels, includeZero: true);

                    bool visible = _runVisibility.TryGetValue(slot, out bool runVisible) ? runVisible : true;
                    pane.SetRunVisibility(slot, visible, _channelVisibility);
                }
            }

            double minX;
            double maxX;
            if (preserveAlignmentZoom)
            {
                minX = previousLeft;
                maxX = previousRight;
            }
            else
            {
                (minX, maxX) = _panes[0].GetTimeRange(runsBySlot, _castleTimeShift);
                minX -= 0.10;
                maxX += 0.10;
            }

            foreach (ChartPane pane in _panes)
            {
                pane.ApplyTimeRangeAndTicks(runsBySlot, _castleTimeShift, minX, maxX);
                pane.ReapplyAxisLocks(ActiveScales, _isFourPoleMode, _channelBands);
            }
            RelinkTimeAxes();
            RefreshPlot();
        }

        public void SetupAllAxes()
        {
            foreach (ChartPane pane in _panes)
                pane.SetupAllAxes(_focusedChannel, GetTraceColor);
        }

        private IReadOnlyDictionary<string, (double Min, double Max)> ActiveScales =>
            _activeProfile == ScaleProfile.SpeedRun ? _speedScales : _dragScales;

        private void ReapplyAxisLocks()
        {
            foreach (ChartPane pane in _panes)
                pane.ReapplyAxisLocks(ActiveScales, _isFourPoleMode, _channelBands);
        }

        private void UpdateReadout(int pixelX, int pixelY, double time, Dictionary<string, double?[]> values)
        {
            var lines = new List<Controls.CursorReadout.Line>();
            IEnumerable<string> orderedChannels = ChannelText.Order.Concat(
                values.Keys.Where(channel => !ChannelText.Order.Contains(channel, StringComparer.OrdinalIgnoreCase)));
            foreach (string channel in orderedChannels)
            {
                if (!_readoutChannels.Contains(channel)) continue;
                if (!values.TryGetValue(channel, out var channelValues) || channelValues.All(value => !value.HasValue))
                    continue;

                ScottPlot.Color color = GetTraceColor(channel);
                lines.Add(new Controls.CursorReadout.Line(
                    ChannelText.DisplayName(channel),
                    System.Drawing.Color.FromArgb(color.R, color.G, color.B),
                    channelValues.Select(value => value.HasValue ? ChannelText.Format(channel, value.Value) : "–").ToArray()));
            }
            _readout.ShowAt(pixelX, pixelY, time, lines);
        }

        private void FormsPlot_MouseLeave(object? sender, EventArgs e)
        {
            if (ReferenceEquals(_readout.Parent, sender))
                _readout.Visible = false;
        }

        private void FormsPlot_MouseMove(object? sender, MouseEventArgs e)
        {
            FormsPlot source = sender as FormsPlot ?? _plot;
            if (_alignmentMode && _alignmentDragging)
            {
                double currentX = source.Plot.GetCoordinates(new Pixel(e.X, e.Y)).X;
                double deltaMs = (currentX - _lastAlignmentMouseX) * 1000.0;
                _lastAlignmentMouseX = currentX;
                if (Math.Abs(deltaMs) > 0.001)
                    AlignmentDragged?.Invoke(deltaMs);
                return;
            }

            ChartPane sourcePane = _panes.FirstOrDefault(pane => ReferenceEquals(pane.PlotControl, source)) ?? _panes[0];
            if (!sourcePane.HasCursor) return;

            var mouseCoordinates = source.Plot.GetCoordinates(new Pixel(e.X, e.Y));
            foreach (ChartPane pane in _panes)
                pane.SetCursorX(mouseCoordinates.X);

            var valuesAtCursor = new Dictionary<string, double?[]>();
            foreach (ChartPane pane in _panes)
            {
                foreach (var pair in pane.GetChannelToScatters())
                {
                    if (!valuesAtCursor.TryGetValue(pair.Key, out var channelValues))
                    {
                        channelValues = new double?[3];
                        valuesAtCursor[pair.Key] = channelValues;
                    }

                    foreach (var scatter in pair.Value)
                    {
                        if (!scatter.IsVisible) continue;
                        if (!pane.TryGetScatterSlot(scatter, out int slot)) continue;
                        int runIndex = (slot - 1) % 3;

                        var points = scatter.Data.GetScatterPoints();
                        if (points == null || points.Count == 0) continue;

                        int index = -1;
                        double minimumDistance = double.MaxValue;
                        for (int pointIndex = 0; pointIndex < points.Count; pointIndex++)
                        {
                            double distance = Math.Abs(points[pointIndex].X - mouseCoordinates.X);
                            if (distance < minimumDistance)
                            {
                                minimumDistance = distance;
                                index = pointIndex;
                            }
                        }

                        if (pane.TryGetPlottedValues(scatter, out double[] plottedValues) &&
                            index >= 0 && index < plottedValues.Length)
                        {
                            // RPM is already halved in the plotted array in four-pole mode.
                            channelValues[runIndex] = plottedValues[index];
                        }
                    }
                }
            }

            CursorMoved?.Invoke(valuesAtCursor);
            if (!ReferenceEquals(_readout.Parent, source))
            {
                _readout.Parent?.Controls.Remove(_readout);
                source.Controls.Add(_readout);
                _readout.BringToFront();
            }
            UpdateReadout(e.X, e.Y, mouseCoordinates.X, valuesAtCursor);
            RefreshPlot();
        }

        private void FormsPlot_MouseDown(object? sender, MouseEventArgs e)
        {
            if (!_alignmentMode || e.Button != MouseButtons.Left) return;
            if ((Control.ModifierKeys & Keys.Control) != Keys.Control) return;

            FormsPlot source = sender as FormsPlot ?? _plot;
            _alignmentDragging = true;
            _lastAlignmentMouseX = source.Plot.GetCoordinates(new Pixel(e.X, e.Y)).X;
            source.Capture = true;
        }

        private void FormsPlot_MouseUp(object? sender, MouseEventArgs e)
        {
            FormsPlot source = sender as FormsPlot ?? _plot;
            if (e.Button == MouseButtons.Right)
            {
                ShowTrimMenu(source, e.Location);
                return;
            }

            if (!_alignmentDragging || e.Button != MouseButtons.Left) return;
            _alignmentDragging = false;
            source.Capture = false;
        }

        private void ShowTrimMenu(FormsPlot source, System.Drawing.Point location)
        {
            if (_trimMenu == null) return;

            _lastRightClickPlotX = source.Plot.GetCoordinates(new Pixel(location.X, location.Y)).X;
            if (_trimBeforeItem != null) _trimBeforeItem.Enabled = _manualTrimAvailable;
            if (_trimAfterItem != null) _trimAfterItem.Enabled = _manualTrimAvailable;
            if (_trimResetItem != null) _trimResetItem.Enabled = _manualTrimAvailable;
            _trimMenu.Show(source, location);
        }

        public void ResetEmptyPlot()
        {
            foreach (ChartPane pane in _panes)
                pane.ResetEmptyPlot(ActiveScales, _isFourPoleMode, _channelBands);
            RelinkTimeAxes();
        }

        public void LogVisibilityStates()
        {
            if (!Logger.IsEnabled) return;

            Logger.Log("🔍 Current Run Visibility States:");
            foreach (var pair in _runVisibility)
                Logger.Log($"   Run Slot {pair.Key}: {(pair.Value ? "Visible" : "Hidden")}");

            Logger.Log("🔍 Current Channel Visibility States:");
            foreach (var pair in _channelVisibility)
                Logger.Log($"   Channel '{pair.Key}': {(pair.Value ? "Visible" : "Hidden")}");
        }

        public double GetSampleStepSeconds(int slot)
        {
            if (!_runsBySlot.TryGetValue(slot, out var run) || run == null)
                return 0.01;

            IEnumerable<double> times;
            if (run.IsRaceBox)
            {
                if (!run.Data.TryGetValue("RaceBox Speed", out var points) || points.Count < 2)
                    return 0.01;
                times = points.Select(point => point.Time);
            }
            else
            {
                if (run.DataPoints == null || run.DataPoints.Count < 2)
                    return 0.01;
                times = run.DataPoints.Select(point => point.Time);
            }

            double[] differences = times.Zip(times.Skip(1), (first, second) => second - first)
                .Where(difference => difference > 0)
                .OrderBy(difference => difference)
                .ToArray();

            return differences.Length == 0 ? 0.01 : differences[differences.Length / 2];
        }
    }
}
