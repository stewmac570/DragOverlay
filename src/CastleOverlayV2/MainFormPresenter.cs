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
        private readonly ChannelToggleBar _toggleBar;

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

        public MainFormPresenter(MainForm view, ConfigService config, PlotManager plot, ChannelToggleBar toggleBar)
        {
            _view = view;
            _config = config;
            _plot = plot;
            _toggleBar = toggleBar;

            _isFourPoleMode = _config.Config.IsFourPoleMode;
            _plot.SetFourPoleMode(_isFourPoleMode);
            _plot.SetSpeedMode(_isSpeedRunMode);

            _toggleBar.ChannelVisibilityChanged += OnChannelVisibilityChanged;
            _toggleBar.RpmModeChanged += OnRpmModeChanged;
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
                var loaded = await Task.Run(() => loader.Load(path, trimForDrag: !_isSpeedRunMode));

                if (loaded != null && loaded.DataPoints.Count > 0)
                {
                    _runs[slot] = loaded;
                    Logger.Log($"Loaded Run {slot} - {Path.GetFileName(path)} - {loaded.DataPoints.Count} rows");

                    _plot.SetRun(slot, loaded);
                    _plot.SetRunVisibility(slot, true);

                    PlotAllRuns();
                    _plot.SetSpeedMode(_isSpeedRunMode);
                    _plot.SetupAllAxes();
                    _plot.RefreshPlot();

                    _view.SetSlotLoadedUI(slot, $"Run {slot}: {Path.GetFileName(path)}", _plot.GetRunVisibility(slot));
                }
                else
                {
                    _runs.Remove(slot);
                    Logger.Log($"Run {slot} load failed or empty data.");
                    _view.ShowError("Import Failed",
                        "This file could not be loaded.\n\nIt may not be a valid Castle log or it contains no data.");
                }
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
                var rbData = RaceBoxLoader.LoadHeaderOnly(path);
                if (rbData == null)
                {
                    Logger.Log($"[Presenter] RaceBox header load failed for slot {uiSlot}.");
                    return;
                }

                if (rbData.FirstCompleteRunIndex == null)
                {
                    _view.ShowInfo("Incomplete Run", "No complete run found in this RaceBox file.");
                    return;
                }

                var loader = new RaceBoxLoader();
                var points = await Task.Run(() => loader.LoadTelemetry(path, rbData.FirstCompleteRunIndex.Value));

                if (points == null || points.Count == 0)
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

                _view.SetSlotLoadedUI(plotSlot, $"RaceBox {uiSlot}: {TruncateFileName(path)}", true);
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
            var states = _toggleBar.GetChannelStates();
            if (!states.ContainsKey("RaceBox Speed"))
            {
                _toggleBar.AddChannel("RaceBox Speed", true);
                added = true;
            }
            if (!states.ContainsKey("RaceBox G-Force X"))
            {
                _toggleBar.AddChannel("RaceBox G-Force X", true);
                added = true;
            }
            if (added)
            {
                _toggleBar.PerformLayout();
                _toggleBar.Refresh();
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

        private void OnCursorMoved(Dictionary<string, double?[]> valuesAtCursor)
        {
            _toggleBar.UpdateMousePositionValues(valuesAtCursor);
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

            var visibilityMap = _toggleBar.GetChannelStates();

            if (Logger.IsEnabled)
            {
                Logger.Log("PlotAllRuns — Channel visibility map before applying:");
                foreach (var kvp in visibilityMap)
                    Logger.Log($"  Channel: {kvp.Key}, Visible: {kvp.Value}");
            }

            _plot.SetInitialChannelVisibility(visibilityMap);
            _plot.PlotRuns(new Dictionary<int, RunData>(_runs));
        }

        // ============================================================
        // Helpers
        // ============================================================
        private static string TruncateFileName(string filePath, int maxChars = 28)
        {
            string fileName = Path.GetFileName(filePath) ?? string.Empty;
            if (fileName.Length <= maxChars) return fileName;
            if (maxChars <= 3) return fileName.Substring(0, maxChars);
            return fileName.Substring(0, maxChars - 3) + "...";
        }
    }
}
