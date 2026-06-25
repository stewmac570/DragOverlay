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
        private readonly IMainView _view;
        private readonly ConfigService _config;
        private readonly PlotManager _plot;
        private readonly ChannelDrawer _drawer;
        private readonly TunePanel _tunePanel;

        // Active runs by slot. Castle = 1..3, RaceBox = 4..6.
        private readonly Dictionary<int, RunData> _runs = new();

        // RaceBox header metadata per UI slot (1..3).
        private RaceBoxData? _raceBox1, _raceBox2, _raceBox3;

        private bool _isFourPoleMode;
        private bool _isSpeedRunMode;
        private int? _armedSlot;
        private int? _selectedTuneSlot;

        // Cleanup tracker for the temp-extracted project (issue #87).
        private string? _activeProjectTempDir;

        // Modifier-key shift step sizes (ms).
        private const double SHIFT_FINE_MS = 1;     // Ctrl
        private const double SHIFT_COARSE_MS = 1000; // Shift
        private const double ALIGN_FINE_MS = 10;
        private const double ALIGN_COARSE_MS = 100;

        public bool IsSpeedRunMode => _isSpeedRunMode;
        public bool IsAnyRunLoaded => _runs.Count > 0;
        public bool IsAlignmentArmed => _armedSlot.HasValue;

        public MainFormPresenter(IMainView view, ConfigService config, PlotManager plot, ChannelDrawer drawer, TunePanel tunePanel)
        {
            _view = view;
            _config = config;
            _plot = plot;
            _drawer = drawer;
            _tunePanel = tunePanel;

            _isFourPoleMode = _config.Config.IsFourPoleMode;
            _plot.SetFourPoleMode(_isFourPoleMode);
            _plot.SetSpeedMode(_isSpeedRunMode);

            _drawer.ChannelVisibilityChanged += OnChannelVisibilityChanged;
            _drawer.RpmModeChanged += OnRpmModeChanged;
            _drawer.ChannelFocused += OnChannelFocused;
            _plot.CursorMoved += OnCursorMoved;
            _plot.AlignmentDragged += OnAlignmentDragged;
            _plot.TrimBeforeRequested += plotX => TrimArmedRun(plotX, before: true);
            _plot.TrimAfterRequested += plotX => TrimArmedRun(plotX, before: false);
            _plot.TrimResetRequested += ResetArmedRunTrim;
            _tunePanel.AttachTuneRequested += AttachTuneToRun;
            _tunePanel.RadioSettingsChanged += UpdateRadioSettings;
            _tunePanel.SelectedRunChanged += SelectTuneRun;
            _tunePanel.OpenProjectRequested += OnOpenProjectRequested;
            _tunePanel.SaveProjectRequested += OnSaveProjectRequested;

            RefreshTunePanelRuns();
            _tunePanel.SetSaveProjectEnabled(IsAnyRunLoaded);
        }

        private void OnOpenProjectRequested()
        {
            string? path = _view.PickProjectFileToOpen();
            if (path == null) return;
            var result = OpenProjectFrom(path);
            if (!result.Ok) ShowResultMessage(result);
            _tunePanel.SetSaveProjectEnabled(IsAnyRunLoaded);
        }

        private void OnSaveProjectRequested()
        {
            string? path = _view.PickProjectFileToSave();
            if (path == null) return;
            var result = SaveProjectTo(path);
            if (!result.Ok) ShowResultMessage(result);
        }

        // ============================================================
        // Castle load
        // ============================================================
        public async Task LoadCastleRunAsync(int slot)
        {
            Logger.Log($"LoadCastleRunAsync({slot}) started");

            string? path = _view.PickCastleCsvFile();
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

                loaded.SourcePath = path;
                _runs[slot] = loaded;
                _selectedTuneSlot = slot;
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
            RefreshTunePanelRuns();
        }

        // ============================================================
        // RaceBox load
        // ============================================================
        public async Task LoadRaceBoxRunAsync(int uiSlot)
        {
            int plotSlot = uiSlot + 3;
            Logger.Log($"LoadRaceBoxRunAsync(UI {uiSlot} → plot slot {plotSlot}) started");

            string? path = _view.PickRaceBoxCsvFile();
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
                    FileName = Path.GetFileName(path),
                    SourcePath = path
                };
                run.Data["RaceBox Speed"] = points
                    .Select(p => new DataPoint { Time = p.Time.TotalSeconds, Y = p.SpeedMph })
                    .ToList();
                run.Data["RaceBox G-Force X"] = points
                    .Select(p => new DataPoint { Time = p.Time.TotalSeconds, Y = p.GForceX })
                    .ToList();
                run.Data["RaceBox Distance"] = IntegrateDistanceFeet(points);
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
            RefreshTunePanelRuns();
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
            if (!states.ContainsKey("RaceBox Distance"))
            {
                _drawer.AddChannel("RaceBox Distance", true);
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

        /// <summary>
        /// Cumulative distance (feet) along the run, integrated from RaceBox speed at each sample.
        /// Rectangular integration is good enough for ~25 Hz data.
        /// </summary>
        private static List<DataPoint> IntegrateDistanceFeet(List<Models.RaceBoxPoint> points)
        {
            const double FeetPerMile = 5280.0;
            const double SecondsPerHour = 3600.0;
            const double MphToFeetPerSec = FeetPerMile / SecondsPerHour; // = 1.46667

            var result = new List<DataPoint>(points.Count);
            double distFt = 0;
            double prevSec = points.Count > 0 ? points[0].Time.TotalSeconds : 0;
            for (int i = 0; i < points.Count; i++)
            {
                double sec = points[i].Time.TotalSeconds;
                if (i > 0)
                {
                    double dt = sec - prevSec;
                    if (dt > 0)
                        distFt += Math.Max(0, points[i].SpeedMph) * MphToFeetPerSec * dt;
                }
                result.Add(new DataPoint { Time = sec, Y = distFt });
                prevSec = sec;
            }
            return result;
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
            if (_armedSlot == slot)
                DisarmAlignment();

            _runs.Remove(slot);
            _view.ResetSlotUI(slot);

            if (_selectedTuneSlot == slot)
                _selectedTuneSlot = null;

            if (_plot.GetRunVisibility(slot))
                _plot.ToggleRunVisibility(slot);

            _plot.PlotRuns(new Dictionary<int, RunData>(_runs));
            PushAllStatsToDrawer();
            _view.UpdateRunTypeLockState();
            RefreshTunePanelRuns();
        }

        public void ToggleAlignmentArm(int slot)
        {
            if (!_runs.TryGetValue(slot, out var run))
                return;

            if (_armedSlot == slot)
            {
                DisarmAlignment();
                return;
            }

            if (_armedSlot.HasValue)
                _view.SetSlotArmedUI(_armedSlot.Value, false);

            _armedSlot = slot;
            _view.SetSlotArmedUI(slot, true);
            _view.ShowAlignmentUI(run.FileName, run.TimeShiftMs);
            _plot.SetAlignmentMode(true);
            // Manual trim is Castle-only (RaceBox uses a different data shape and isn't auto-trimmed).
            _plot.SetManualTrimAvailable(!run.IsRaceBox);
        }

        public void DisarmAlignment()
        {
            if (_armedSlot.HasValue)
                _view.SetSlotArmedUI(_armedSlot.Value, false);

            _armedSlot = null;
            _view.HideAlignmentUI();
            _plot.SetAlignmentMode(false);
            _plot.SetManualTrimAvailable(false);
        }

        public void NudgeArmedRun(int direction, bool coarse)
        {
            if (!_armedSlot.HasValue) return;
            ApplyAlignmentDelta(
                _armedSlot.Value,
                direction * (coarse ? ALIGN_COARSE_MS : ALIGN_FINE_MS));
        }

        public void ResetArmedRun()
        {
            if (!_armedSlot.HasValue) return;
            ResetShift(_armedSlot.Value);
            UpdateAlignmentOffset();
        }

        public void AutoAlignArmedRun()
        {
            if (!_armedSlot.HasValue || !_runs.TryGetValue(_armedSlot.Value, out var run))
                return;

            double? offsetMs = AlignmentHelper.GetAutoOffsetMs(run);
            if (!offsetMs.HasValue)
            {
                _view.ShowInfo(
                    "Auto-align",
                    "No clear launch point was found. The existing offset was left unchanged.");
                return;
            }

            run.TimeShiftMs = offsetMs.Value;
            Logger.Log(
                $"Auto-align slot {_armedSlot.Value}: offset={run.TimeShiftMs:F1}ms");
            PlotAllRuns();
            UpdateAlignmentOffset();
        }

        /// <summary>
        /// Manually trim the armed Castle run, removing samples before (or after) the
        /// right-clicked time. Reversible — operates over the run's baseline and can be
        /// undone with <see cref="ResetArmedRunTrim"/>.
        /// </summary>
        private void TrimArmedRun(double plotX, bool before)
        {
            if (!_armedSlot.HasValue || !_runs.TryGetValue(_armedSlot.Value, out var run))
                return;
            if (run.IsRaceBox)
            {
                _view.ShowInfo("Trim", "Manual trim is only available for Castle log runs.");
                return;
            }

            run.CaptureTrimBaseline();

            // Plot X is in display seconds; convert to the run's own (re-zeroed) time.
            // The global Castle time shift is currently always 0, so only the per-run
            // alignment offset needs removing.
            double runTime = plotX - run.TimeShiftMs / 1000.0;

            if (before)
                run.TrimStartTime = runTime;
            else
                run.TrimEndTime = runTime;

            run.ApplyManualTrim();
            Logger.Log($"✂️ Manual trim slot {_armedSlot.Value}: {(before ? "before" : "after")} t={runTime:F3}s → {run.DataPoints.Count} pts");
            PlotAllRuns();
        }

        /// <summary>Clear any manual trim on the armed run and restore its full baseline.</summary>
        public void ResetArmedRunTrim()
        {
            if (!_armedSlot.HasValue || !_runs.TryGetValue(_armedSlot.Value, out var run))
                return;
            if (run.IsRaceBox || run.BaselineDataPoints == null)
                return;

            run.TrimStartTime = null;
            run.TrimEndTime = null;
            run.ApplyManualTrim();
            Logger.Log($"✂️ Manual trim reset slot {_armedSlot.Value} → {run.DataPoints.Count} pts");
            PlotAllRuns();
        }

        public void AttachTuneToRun(int? slot)
        {
            RunData? run = null;
            if (slot.HasValue &&
                (!_runs.TryGetValue(slot.Value, out run) || run.IsRaceBox))
            {
                _view.ShowInfo("Attach Tune", "Tune files can only be attached to Castle log runs.");
                RefreshTunePanelRuns();
                return;
            }

            string? path = _view.PickTuneFile();
            if (path == null)
                return;

            var result = new CastleTuneLoader().Load(path);
            if (!result.Ok)
            {
                ShowResultMessage(result);
                return;
            }

            // Track the original .dat path so the project saver can embed the bytes.
            result.Value!.SourcePath = path;

            if (slot.HasValue && run != null)
            {
                var previousRadio = run.Tune?.Radio.Clone();
                run.Tune = result.Value!;
                if (previousRadio != null)
                    run.Tune.Radio = previousRadio;

                _selectedTuneSlot = slot.Value;
                Logger.Log($"Attached tune to Run {slot.Value}: {Path.GetFileName(path)}");
                RefreshTunePanelRuns();
            }
            else
            {
                Logger.Log($"Previewed tune without a loaded run: {Path.GetFileName(path)}");
                _tunePanel.SetTune(null, result.Value);
            }
        }

        public void UpdateRadioSettings(int slot, RadioTuneSettings settings)
        {
            if (!_runs.TryGetValue(slot, out var run) || run.IsRaceBox)
                return;

            run.Tune ??= new TuneSettings();
            run.Tune.Radio = settings;
            _selectedTuneSlot = slot;
        }

        private void SelectTuneRun(int slot)
        {
            _selectedTuneSlot = slot;
            _tunePanel.SetTune(slot, _runs.TryGetValue(slot, out var run) ? run.Tune : null);
        }

        private void RefreshProjectButtonsState() =>
            _tunePanel.SetSaveProjectEnabled(IsAnyRunLoaded);

        private void RefreshTunePanelRuns()
        {
            var castleSlots = _runs
                .Where(kvp => kvp.Key <= 3 && !kvp.Value.IsRaceBox)
                .Select(kvp => kvp.Key)
                .OrderBy(slot => slot)
                .ToList();

            if (_selectedTuneSlot.HasValue && !castleSlots.Contains(_selectedTuneSlot.Value))
                _selectedTuneSlot = null;

            int? selected = _selectedTuneSlot ?? castleSlots.FirstOrDefault();
            if (selected == 0)
                selected = null;

            _tunePanel.SetAvailableRuns(castleSlots, selected);
            _tunePanel.SetTune(
                selected,
                selected.HasValue && _runs.TryGetValue(selected.Value, out var run) ? run.Tune : null);

            RefreshProjectButtonsState();
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

        private void OnAlignmentDragged(double deltaMs)
        {
            if (_armedSlot.HasValue)
                ApplyAlignmentDelta(_armedSlot.Value, deltaMs);
        }

        private void ApplyAlignmentDelta(int slot, double deltaMs)
        {
            if (!_runs.TryGetValue(slot, out var run)) return;
            run.TimeShiftMs += deltaMs;
            PlotAllRuns();
            UpdateAlignmentOffset();
        }

        private void UpdateAlignmentOffset()
        {
            if (_armedSlot.HasValue && _runs.TryGetValue(_armedSlot.Value, out var run))
                _view.SetAlignmentOffsetUI(run.TimeShiftMs);
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

            AutoScaleVoltage();
            PushAllStatsToDrawer();
        }

        /// <summary>
        /// Recompute the Voltage Y-axis range from the currently loaded Castle runs
        /// so the trace fits whatever pack the user is running (2S → 8S+). Skips
        /// non-positive readings (zero = no sample). No-op if no Castle data is loaded.
        /// </summary>
        private void AutoScaleVoltage()
        {
            double min = double.PositiveInfinity;
            double max = double.NegativeInfinity;
            foreach (var (_, run) in _runs)
            {
                if (run.IsRaceBox || run.DataPoints == null) continue;
                foreach (var dp in run.DataPoints)
                {
                    if (dp.Voltage <= 0) continue;
                    if (dp.Voltage < min) min = dp.Voltage;
                    if (dp.Voltage > max) max = dp.Voltage;
                }
            }
            if (!double.IsFinite(min) || !double.IsFinite(max) || max <= min) return;

            // Pad: at least 0.2V or 5% of range, whichever is larger. Clamp lower bound at 0.
            double range = max - min;
            double pad = Math.Max(0.2, range * 0.05);
            double lo = Math.Max(0, min - pad);
            double hi = max + pad;

            // Apply to both profiles so switching Drag ↔ Speed-Run preserves the fit.
            _plot.SetChannelScale("Voltage", lo, hi, forSpeedRun: false);
            _plot.SetChannelScale("Voltage", lo, hi, forSpeedRun: true);
        }

        private static readonly string[] _knownChannels = new[]
        {
            "RPM", "Throttle %", "Voltage", "Current", "Ripple", "PowerOut",
            "ESC Temp", "MotorTemp", "MotorTiming", "Acceleration",
            "RaceBox Speed", "RaceBox Distance", "RaceBox G-Force X",
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
        // Project save (issue #86)
        // ============================================================

        /// <summary>
        /// Snapshot the current analysis session into a <see cref="ProjectSnapshot"/>
        /// suitable for handing to <see cref="ProjectSaver"/>.
        /// Returns an error result if any loaded run is missing its on-disk source path
        /// (which would prevent embedding the original bytes — required by issue #86).
        /// </summary>
        public LoadResult<ProjectSnapshot> BuildProjectSnapshot()
        {
            if (_runs.Count == 0)
                return LoadResult<ProjectSnapshot>.Error("Save Project",
                    "Load at least one Castle or RaceBox log before saving a project.");

            var manifest = new ProjectManifest
            {
                AppBuild = _config.GetBuildNumber(),
                CreatedUtc = DateTimeOffset.UtcNow,
                RunMode = _isSpeedRunMode ? ProjectRunMode.Speed : ProjectRunMode.Drag,
                ChannelVisibility = new Dictionary<string, bool>(
                    _drawer.GetChannelStates(),
                    StringComparer.OrdinalIgnoreCase)
            };

            var files = new List<ProjectSnapshotFile>();

            foreach (var (plotSlot, run) in _runs.OrderBy(kv => kv.Key))
            {
                bool isRaceBox = plotSlot >= 4;
                int uiSlot = isRaceBox ? plotSlot - 3 : plotSlot;
                string sourceLabel = isRaceBox ? "racebox" : "castle";

                if (string.IsNullOrWhiteSpace(run.SourcePath))
                {
                    return LoadResult<ProjectSnapshot>.Error("Save Project",
                        $"Run {uiSlot} ({(isRaceBox ? "RaceBox" : "Castle")}) has no on-disk source path. " +
                        "Re-load the file before saving the project.");
                }

                string sourceArchivePath = $"logs/{sourceLabel}-{uiSlot}{Path.GetExtension(run.SourcePath)?.ToLowerInvariant()}";
                if (string.IsNullOrEmpty(Path.GetExtension(run.SourcePath)))
                    sourceArchivePath = $"logs/{sourceLabel}-{uiSlot}.csv";

                string? tuneArchivePath = null;
                if (!isRaceBox && run.Tune?.SourcePath is { } tunePath && File.Exists(tunePath))
                    tuneArchivePath = $"tunes/{sourceLabel}-{uiSlot}.dat";

                manifest.Runs.Add(new ProjectRunEntry
                {
                    SourceType = isRaceBox ? ProjectSourceType.RaceBox : ProjectSourceType.Castle,
                    UiSlot = uiSlot,
                    PlotSlot = plotSlot,
                    DisplayFileName = run.FileName ?? string.Empty,
                    SourcePath = sourceArchivePath,
                    IsVisible = _plot.GetRunVisibility(plotSlot),
                    TimeShiftMs = run.TimeShiftMs,
                    TrimStartTime = run.TrimStartTime,
                    TrimEndTime = run.TrimEndTime,
                    TunePath = tuneArchivePath,
                    RadioSettings = run.Tune?.Radio
                });

                files.Add(new ProjectSnapshotFile(run.SourcePath, sourceArchivePath));
                if (tuneArchivePath != null)
                    files.Add(new ProjectSnapshotFile(run.Tune!.SourcePath!, tuneArchivePath));
            }

            return LoadResult<ProjectSnapshot>.Success(new ProjectSnapshot
            {
                Manifest = manifest,
                Files = files
            });
        }

        /// <summary>
        /// Convenience: build the snapshot from current state and save to disk.
        /// The caller is responsible for picking <paramref name="destinationPath"/> (e.g. via
        /// <c>SaveFileDialog</c>) and showing any error returned here on the UI thread.
        /// </summary>
        public LoadResult<string> SaveProjectTo(string destinationPath)
        {
            var snapshot = BuildProjectSnapshot();
            if (!snapshot.Ok)
                return LoadResult<string>.Error(snapshot.Title ?? "Save Project",
                    snapshot.Message ?? "Could not build the project snapshot.");

            return new ProjectSaver().Save(destinationPath, snapshot.Value!);
        }

        // ============================================================
        // Project open (issue #87)
        // ============================================================

        /// <summary>
        /// Open a <c>.dragoverlay</c> package and atomically swap the current session for
        /// the restored one. Validation happens before any mutation — on failure the current
        /// session is untouched.
        /// </summary>
        public LoadResult<string> OpenProjectFrom(string packagePath)
        {
            var loadResult = new ProjectLoader(_config).Load(packagePath);
            if (!loadResult.Ok)
                return LoadResult<string>.Error(loadResult.Title ?? "Open Project",
                    loadResult.Message ?? "Could not open the project.");

            var restored = loadResult.Value!;

            // Disarm any in-progress alignment so we don't trip drag events during swap.
            if (_armedSlot.HasValue)
                DisarmAlignment();

            // Tear down the current session — clear plot scatters and chip state, but
            // do NOT call DeleteRun (which writes to config + nudges Castle delete UI flows).
            for (int slot = 1; slot <= 6; slot++)
            {
                if (_runs.ContainsKey(slot))
                {
                    _runs.Remove(slot);
                    _plot.SetRun(slot, null!);
                    _view.ResetSlotUI(slot);
                }
            }
            _selectedTuneSlot = null;

            // Apply manifest-level state.
            _isSpeedRunMode = restored.Manifest.RunMode == ProjectRunMode.Speed;
            _plot.SetSpeedMode(_isSpeedRunMode);
            _view.ApplyRunTypeUI(_isSpeedRunMode);

            if (restored.Manifest.ChannelVisibility is { Count: > 0 } chanVis)
                _drawer.ApplyChannelVisibility(chanVis);

            // Insert each restored run into its slot.
            foreach (var (plotSlot, run) in restored.RunsBySlot.OrderBy(kv => kv.Key))
            {
                _runs[plotSlot] = run;
                _plot.SetRun(plotSlot, run);

                bool isVisible = restored.Manifest.Runs
                    .FirstOrDefault(r => r.PlotSlot == plotSlot)?.IsVisible ?? true;
                _plot.SetRunVisibility(plotSlot, isVisible);

                _view.SetSlotLoadedUI(plotSlot, run.SourcePath ?? run.FileName ?? "", isVisible);
            }

            EnsureRaceBoxChannelsInToggleBar();
            PlotAllRuns();
            _plot.SetupAllAxes();
            _plot.RefreshPlot();

            // Pick the first Castle slot for the tune panel (or first run if no Castle).
            _selectedTuneSlot = restored.RunsBySlot.Keys.Where(s => s <= 3).OrderBy(s => s).FirstOrDefault();
            if (_selectedTuneSlot == 0) _selectedTuneSlot = null;
            RefreshTunePanelRuns();
            _view.UpdateRunTypeLockState();

            // Replace the previous extracted temp dir with the new one.
            ProjectLoader.TryDeleteTempDir(_activeProjectTempDir ?? "");
            _activeProjectTempDir = restored.TempDir;

            if (restored.Warnings.Count > 0)
            {
                _view.ShowInfo("Open Project",
                    "Project opened with warnings:\n\n" + string.Join("\n", restored.Warnings));
            }

            return LoadResult<string>.Success(packagePath);
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
