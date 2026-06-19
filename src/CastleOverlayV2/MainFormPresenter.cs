// File: src/CastleOverlayV2/MainFormPresenter.cs
using CastleOverlayV2.Controls;
using CastleOverlayV2.Models;
using CastleOverlayV2.Plot;
using CastleOverlayV2.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CastleOverlayV2
{
    /// <summary>
    /// Owns run-slot state, file loading, plotting orchestration, and RunType mode.
    /// MainForm is the view: it forwards events here and exposes the UI setters this class calls.
    /// </summary>
    public class MainFormPresenter
    {
        private readonly MainForm _view;
        private readonly ConfigService _config;
        private readonly PlotManager _plot;
        private readonly ChannelDrawer _drawer;

        // Active runs by slot. Castle = 1..3, RaceBox = 4..6.
        private readonly Dictionary<int, RunData> _runs = new();

        // RaceBox header metadata per UI slot (1..3).
        private RaceBoxData? _raceBox1, _raceBox2, _raceBox3;

        private bool _isFourPoleMode;
        private bool _isSpeedRunMode;

        // Modifier-key shift step sizes (ms).
        private const double SHIFT_FINE_MS = 1;     // Ctrl
        private const double SHIFT_COARSE_MS = 1000; // Shift

        public bool IsSpeedRunMode => _isSpeedRunMode;
        public bool IsAnyRunLoaded => _runs.Count > 0;

        public MainFormPresenter(MainForm view, ConfigService config, PlotManager plot, ChannelDrawer drawer)
        {
            _view = view;
            _config = config;
            _plot = plot;
            _drawer = drawer;

            _isFourPoleMode = _config.Config.IsFourPoleMode;
            _plot.SetFourPoleMode(_isFourPoleMode);
            _plot.SetSpeedMode(_isSpeedRunMode);

            _drawer.ChannelVisibilityChanged += OnChannelVisibilityChanged;
            _drawer.RpmModeChanged += OnRpmModeChanged;
            _drawer.ChannelFocused += OnChannelFocused;
            _plot.CursorMoved += OnCursorMoved;
        }

        // ============================================================
        // Castle load
        // ============================================================
        public async Task LoadCastleRunAsync(int slot)
        {
            Logger.Log($"LoadCastleRunAsync({slot}) started");

            string? path = _view.PickCsvFile();
            if (path == null) return;

            try
            {
                var loader = new CsvLoader(_config);
                var result = await Task.Run(() => loader.Load(path, trimForDrag: !_isSpeedRunMode));

                if (!result.Ok)
                {
                    _runs.Remove(slot);
                    Logger.Log($"Run {slot} load failed: {result.Message}");
                    ShowResultMessage(result);
                    return;
                }

                var loaded = result.Value!;
                if (loaded.DataPoints.Count == 0)
                {
                    _runs.Remove(slot);
                    Logger.Log($"Run {slot} load returned empty data.");
                    _view.ShowError("Import Failed",
                        "This file could not be loaded.\n\nIt may not be a valid Castle log or it contains no data.");
                    return;
                }

                _runs[slot] = loaded;
                Logger.Log($"Loaded Run {slot} - {Path.GetFileName(path)} - {loaded.DataPoints.Count} rows");

                _plot.SetRun(slot, loaded);
                _plot.SetRunVisibility(slot, true);

                PlotAllRuns();
                _plot.SetSpeedMode(_isSpeedRunMode);
                _plot.SetupAllAxes();
                _plot.RefreshPlot();

                _view.SetSlotLoadedUI(slot, path, _plot.GetRunVisibility(slot));

                // Surface any non-fatal warning (e.g. "no drag pass detected").
                if (result.HasMessage) ShowResultMessage(result);
            }
            catch (Exception ex)
            {
                Logger.Log($"ERROR in Run{slot}: {ex.Message}");
                _view.ShowError("Error", "An error occurred while loading the file.\n\n" + ex.Message);
            }

            _view.UpdateRunTypeLockState();
        }

        // ============================================================
        // RaceBox load
        // ============================================================
        public async Task LoadRaceBoxRunAsync(int uiSlot)
        {
            int plotSlot = uiSlot + 3;
            Logger.Log($"LoadRaceBoxRunAsync(UI {uiSlot} → plot slot {plotSlot}) started");

            string? path = _view.PickCsvFile();
            if (path == null) return;

            try
            {
                var headerResult = RaceBoxLoader.LoadHeaderOnly(path);
                if (!headerResult.Ok)
                {
                    Logger.Log($"[Presenter] RaceBox header load failed for slot {uiSlot}: {headerResult.Message}");
                    ShowResultMessage(headerResult);
                    return;
                }

                var rbData = headerResult.Value!;

                if (rbData.FirstCompleteRunIndex == null)
                {
                    _view.ShowInfo("Incomplete Run", "No complete run found in this RaceBox file.");
                    return;
                }

                var loader = new RaceBoxLoader();
                var telemetryResult = await Task.Run(() => loader.LoadTelemetry(path, rbData.FirstCompleteRunIndex.Value));

                if (!telemetryResult.Ok)
                {
                    Logger.Log($"[Presenter] RaceBox telemetry load failed for slot {uiSlot}: {telemetryResult.Message}");
                    ShowResultMessage(telemetryResult);
                    return;
                }

                var points = telemetryResult.Value!;
                if (points.Count == 0)
                {
                    Logger.Log($"[Presenter] RaceBox telemetry empty for slot {uiSlot}.");
                    _view.ShowError("Telemetry Error",
                        "RaceBox telemetry is empty or could not be parsed.");
                    return;
                }

                switch (uiSlot)
                {
                    case 1: _raceBox1 = rbData; break;
                    case 2: _raceBox2 = rbData; break;
                    case 3: _raceBox3 = rbData; break;
                }

                var run = new RunData
                {
                    IsRaceBox = true,
                    SplitTimes = rbData.SplitTimes,
                    SplitLabels = rbData.SplitLabels,
                    FileName = Path.GetFileName(path)
                };
                run.Data["RaceBox Speed"] = points
                    .Select(p => new DataPoint { Time = p.Time.TotalSeconds, Y = p.SpeedMph })
                    .ToList();
                run.Data["RaceBox G-Force X"] = points
                    .Select(p => new DataPoint { Time = p.Time.TotalSeconds, Y = p.GForceX })
                    .ToList();
                // PlotManager skips runs with empty DataPoints — populate with time-only dummies.
                run.DataPoints = points
                    .Select(p => new DataPoint { Time = p.Time.TotalSeconds })
                    .ToList();

                _runs[plotSlot] = run;
                _plot.SetRun(plotSlot, run);
                _plot.SetRunVisibility(plotSlot, true);

                EnsureRaceBoxChannelsInToggleBar();
                PlotAllRuns();

                _view.SetSlotLoadedUI(plotSlot, path, true);
            }
            catch (Exception ex)
            {
                Logger.Log($"ERROR in RaceBox{uiSlot}: {ex.Message}");
                _view.ShowError("Error", "An error occurred while loading the file.\n\n" + ex.Message);
            }

            _view.UpdateRunTypeLockState();
        }

        private void EnsureRaceBoxChannelsInToggleBar()
        {
            bool added = false;
            var states = _drawer.GetChannelStates();
            if (!states.ContainsKey("RaceBox Speed"))
            {
                _drawer.AddChannel("RaceBox Speed", true);
                added = true;
            }
            if (!states.ContainsKey("RaceBox G-Force X"))
            {
                _drawer.AddChannel("RaceBox G-Force X", true);
                added = true;
            }
            if (added)
            {
                _drawer.PerformLayout();
                _drawer.Refresh();
            }
        }

        // ============================================================
        // Per-slot operations
        // ============================================================
        public void ToggleRun(int slot)
        {
            bool nowVisible = _plot.ToggleRunVisibility(slot);
            _view.SetSlotToggleText(slot, nowVisible);
        }

        public void DeleteRun(int slot)
        {
            _runs.Remove(slot);
            _view.ResetSlotUI(slot);

            if (_plot.GetRunVisibility(slot))
                _plot.ToggleRunVisibility(slot);

            _plot.PlotRuns(new Dictionary<int, RunData>(_runs));
            PushAllStatsToDrawer();
            _view.UpdateRunTypeLockState();
        }

        public void ApplyShiftDirection(int slot, int direction, Keys modifiers)
        {
            double delta = GetClickDeltaMs(slot, modifiers) * direction;
            if (!_runs.TryGetValue(slot, out var run)) return;
            run.TimeShiftMs += delta;
            Logger.Log($"⏱️ Run {slot} shift {(delta >= 0 ? "+" : "")}{delta} ms → total {run.TimeShiftMs} ms");
            PlotAllRuns();
        }

        public void ResetShift(int slot)
        {
            if (!_runs.TryGetValue(slot, out var run)) return;
            run.TimeShiftMs = 0;
            Logger.Log($"⏱️ Run {slot} shift reset → 0 ms");
            PlotAllRuns();
        }

        private double GetClickDeltaMs(int slot, Keys modifiers)
        {
            if ((modifiers & Keys.Control) == Keys.Control) return SHIFT_FINE_MS;
            if ((modifiers & Keys.Shift) == Keys.Shift) return SHIFT_COARSE_MS;

            double stepSec = _plot.GetSampleStepSeconds(slot);
            if (stepSec <= 0) stepSec = 0.01;
            return stepSec * 1000.0;
        }

        // ============================================================
        // Mode toggle
        // ============================================================
        public void ToggleSpeedRunMode()
        {
            _isSpeedRunMode = !_isSpeedRunMode;
            _view.ApplyRunTypeUI(_isSpeedRunMode);
            _view.UpdateRunTypeLockState();

            PlotAllRuns();
            _plot.SetSpeedMode(_isSpeedRunMode);
            _plot.SetupAllAxes();
            _plot.RefreshPlot();
        }

        // ============================================================
        // Event handlers (subscribed in ctor)
        // ============================================================
        private void OnChannelVisibilityChanged(string channelName, bool isVisible)
        {
            Logger.Log($"📎 Channel toggle: {channelName} → {(isVisible ? "Show" : "Hide")}");

            // SetChannelVisibility updates per-scatter IsVisible and refreshes; no full replot needed.
            _plot.SetChannelVisibility(channelName, isVisible);
            _config.SetChannelVisibility(channelName, isVisible);
        }

        private void OnRpmModeChanged(bool isFourPole)
        {
            _isFourPoleMode = isFourPole;
            _plot.SetFourPoleMode(_isFourPoleMode);

            // Replot all 6 slots — both Castle and RaceBox.
            PlotAllRuns();

            _config.SetRpmMode(isFourPole);
        }

        private void OnChannelFocused(string channelName)
        {
            // Mirror the focus state to the plot (which re-tints + re-binds the visible Y axis)
            // and back to the drawer (which highlights the focused card).
            _plot.SetFocusedChannel(channelName);
            _drawer.SetFocusedChannel(channelName);
        }

        private void OnCursorMoved(Dictionary<string, double?[]> valuesAtCursor)
        {
            _drawer.UpdateCursorValues(valuesAtCursor);
        }

        // ============================================================
        // Plotting
        // ============================================================
        private void PlotAllRuns()
        {
            if (Logger.IsEnabled)
            {
                Logger.Log("PlotAllRuns called with runs:");
                foreach (var (slot, run) in _runs)
                    Logger.Log($"  Run {slot}: {run.DataPoints.Count} points, shift={run.TimeShiftMs} ms");
            }

            var visibilityMap = _drawer.GetChannelStates();

            if (Logger.IsEnabled)
            {
                Logger.Log("PlotAllRuns — Channel visibility map before applying:");
                foreach (var kvp in visibilityMap)
                    Logger.Log($"  Channel: {kvp.Key}, Visible: {kvp.Value}");
            }

            _plot.SetInitialChannelVisibility(visibilityMap);
            _plot.PlotRuns(new Dictionary<int, RunData>(_runs));

            PushAllStatsToDrawer();
        }

        private static readonly string[] _knownChannels = new[]
        {
            "RPM", "Throttle %", "Voltage", "Current", "Ripple", "PowerOut",
            "ESC Temp", "MotorTemp", "MotorTiming", "Acceleration",
            "RaceBox Speed", "RaceBox G-Force X",
        };

        private void PushAllStatsToDrawer()
        {
            foreach (var ch in _knownChannels)
                _drawer.UpdateChannelStats(ch, GetChannelStats(ch));
        }

        // ============================================================
        // Channel stats (for the drawer's stat cards)
        // ============================================================
        public IReadOnlyList<ChannelRunStats> GetChannelStats(string channelLabel)
        {
            var list = new List<ChannelRunStats>();
            foreach (var (slot, run) in _runs)
            {
                string label = slot <= 3 ? $"Run {slot}" : $"RB {slot - 3}";
                if (!TryExtractChannelValues(run, channelLabel, out var values) || values.Count == 0)
                    continue;

                double max = values[0];
                double sum = 0;
                foreach (var v in values)
                {
                    if (v > max) max = v;
                    sum += v;
                }
                double avg = sum / values.Count;
                list.Add(new ChannelRunStats(slot, label, max, avg));
            }
            // Order by slot for a stable display.
            list.Sort((a, b) => a.Slot.CompareTo(b.Slot));
            return list;
        }

        private bool TryExtractChannelValues(RunData run, string channelLabel, out List<double> values)
        {
            values = new List<double>();
            if (run.IsRaceBox)
            {
                if (run.Data.TryGetValue(channelLabel, out var pts))
                {
                    foreach (var p in pts) values.Add(p.Y);
                    return true;
                }
                return false;
            }

            // Castle channel → DataPoint field
            if (run.DataPoints == null || run.DataPoints.Count == 0) return false;
            switch (channelLabel)
            {
                case "RPM":
                    foreach (var p in run.DataPoints)
                        values.Add(_isFourPoleMode ? p.Speed * 0.5 : p.Speed);
                    return true;
                case "Throttle %":
                    foreach (var p in run.DataPoints) values.Add(p.ThrottlePercent);
                    return true;
                case "Voltage":
                    foreach (var p in run.DataPoints) values.Add(p.Voltage);
                    return true;
                case "Current":
                    foreach (var p in run.DataPoints) values.Add(p.Current);
                    return true;
                case "Ripple":
                    foreach (var p in run.DataPoints) values.Add(p.Ripple);
                    return true;
                case "PowerOut":
                    foreach (var p in run.DataPoints) values.Add(p.PowerOut);
                    return true;
                case "ESC Temp":
                    foreach (var p in run.DataPoints) values.Add(p.Temperature);
                    return true;
                case "MotorTemp":
                    foreach (var p in run.DataPoints) values.Add(p.MotorTemp);
                    return true;
                case "MotorTiming":
                    foreach (var p in run.DataPoints) values.Add(p.MotorTiming);
                    return true;
                case "Acceleration":
                    foreach (var p in run.DataPoints) values.Add(p.Acceleration);
                    return true;
                default:
                    return false;
            }
        }

        // ============================================================
        // Helpers
        // ============================================================
        private void ShowResultMessage<T>(LoadResult<T> result)
        {
            if (!result.HasMessage) return;
            if (result.Severity == ResultSeverity.Info)
                _view.ShowInfo(result.Title!, result.Message!);
            else
                _view.ShowError(result.Title!, result.Message!);
        }

    }
}
