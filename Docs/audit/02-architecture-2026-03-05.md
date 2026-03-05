# DragOverlay — Architecture Analysis
**Date:** 2026-03-05
**Based on:** Full codebase read (all 15 source files) — see audit 01

---

## Current Architecture Map

```
MainForm.cs (~1300 lines)
│   ├── State: run1..run6 (6 named RunData fields)
│   ├── State: raceBox1..raceBox3
│   ├── State: _isSpeedRunMode, _isFourPoleMode
│   ├── All UI event handlers (Load, Toggle, Delete, Shift × 6 slots × 3 actions)
│   ├── RaceBox load orchestration (inline, tripled)
│   └── Mode management (RunType pill switch)
│
PlotManager.cs (~1000 lines)
│   ├── 13 axis fields (throttleAxis, rpmAxis, voltageAxis, ...)
│   ├── 5 dictionaries (_scatters, _rawYMap, _scatterSlotMap, _runVisibility, _channelVisibility)
│   ├── 2 scale profile dicts (_dragScales, _speedScales)
│   ├── Mouse/hover logic
│   └── Split line / label management
│
Services/
│   ├── CsvLoader — parses Castle CSV + calls MessageBox
│   ├── RaceBoxLoader — parses RaceBox CSV + calls MessageBox
│   ├── ConfigService — reads/writes config.json
│   ├── Logger — file logger
│   └── ThrottlePercent — throttle ms → % conversion
│
Models/
│   ├── RunData — dual-purpose: Castle (DataPoints) OR RaceBox (Data dict)
│   ├── DataPoint — Castle row + RaceBox Y shim field
│   ├── Config — flat bag of settings
│   ├── RaceBoxData — header metadata only
│   └── RaceBoxPoint — raw RaceBox telemetry row
│
Controls/
│   └── ChannelToggleBar — UI toggle panel
│
Utils/
│   ├── ChannelColorMap — channel name → ScottPlot.Color
│   └── LineStyleHelper — slot index → line pattern / width
```

---

## Layer Violations (architecture is not layered)

The code does not separate concerns into clean layers. The actual dependency graph looks like this:

```
UI (MainForm, ChannelToggleBar)
    ↓ calls directly
Services (CsvLoader, RaceBoxLoader)
    ↓ calls directly
UI (MessageBox.Show)       ← service layer reaches back into UI layer

PlotManager
    ↓ knows about
Models (RunData, DataPoint)
    ↓ contains
Business logic (RPM scaling, time shifting, hover snap)
```

**There is no clean separation between data parsing, business logic, and presentation.** Services display dialogs; PlotManager transforms data; MainForm orchestrates everything.

---

## Structural Risk 1 — MainForm is a God Object ⚠️ HIGH

`MainForm.cs` is responsible for:
- All event handler wiring (30+ handlers)
- All application state (`run1`–`run6`, `_isSpeedRunMode`, `_isFourPoleMode`, `_isFourPoleMode` duplicated from PlotManager)
- All UI state (button text, enabled/disabled, visibility)
- Business logic orchestration (calling Load, Plot, Config in the right order)
- Run type mode logic (drag vs speed-run switching)

**What this means in practice:**

Every feature touches MainForm. Adding a 4th Castle run slot currently requires changes to:
- 6+ event handlers (Load, Toggle, Delete, ShiftLeft, ShiftRight, ShiftReset)
- `PlotAllRuns()` — add `run7`
- `DeleteRun()` — add `case 7:` switch arm
- `IsAnyRunLoaded()` — add `|| run7 != null`
- `OnRpmModeChanged()` — add run7 to the local dict (currently only run1–3)
- `ApplyRunTypeUI()` — add button visibility lines
- `SetShiftButtonsEnabled()` — add switch arm
- Designer `.cs` and `.resx` — add new buttons
- `_plotManager.SetRun(7, run7)`, `SetRunVisibility(7, ...)` throughout

There is no way to add a slot without editing ~15 locations. This is a direct consequence of the six separate named fields and hand-coded switch/case blocks.

**Break point:** The next Castle slot addition.

---

## Structural Risk 2 — Run Slot System is Hard-Coded ⚠️ HIGH

```csharp
// MainForm.cs — the full slot registry
private RunData run1, run2, run3;   // Castle
private RunData run4, run5, run6;   // RaceBox
```

The mapping between UI concept ("RaceBox 2") and runtime slot is:

| UI label | MainForm var | PlotManager slot |
|----------|-------------|-----------------|
| Castle Run 1 | `run1` | 1 |
| Castle Run 2 | `run2` | 2 |
| Castle Run 3 | `run3` | 3 |
| RaceBox 1 | `run4` | 4 |
| RaceBox 2 | `run5` | 5 |
| RaceBox 3 | `run6` | 6 |

This mapping is **implicit** — it exists only in the pattern of field names and magic numbers. It is not documented, not enforced, and not consistent (the UI calls it "RaceBox 1" but the variable is `run4`, slot is `4`).

`PlotManager` accepts any `Dictionary<int, RunData>` and uses slot numbers to determine line style. The line style logic in `LineStyleHelper` maps slots 0/1/2 (Castle) and 3/4/5 (RaceBox) to patterns. But PlotManager receives slots 1–6, so `LineStyleHelper.GetLinePattern(slot - 1)` is called, which means slot 4 → index 3 → Solid (same as Castle Run 1). **This is fragile and would produce wrong line styles if the slot numbering changed.**

**Break point:** Any refactoring of slot numbers; adding a 4th Castle slot.

---

## Structural Risk 3 — PlotManager Accumulates Responsibilities ⚠️ HIGH

PlotManager mixes three separate concerns:

### Concern A: ScottPlot wrapper
Axis creation, scatter management, cursor, refresh, layout padding. This is pure presentation.

### Concern B: Data transformation
RPM 2P/4P scaling, time shifting (applying `TimeShiftMs` and `_castleTimeShift`). This is business logic. It currently runs at plot-time, meaning the data transformation is tightly coupled to when ScottPlot renders.

### Concern C: Visibility state management
`_channelVisibility`, `_runVisibility`, and the logic to sync them onto scatter `.IsVisible` flags. This is UI state. It currently lives in PlotManager but is also partially in MainForm.

**What this means:** Any change to data transformation or visibility rules requires opening PlotManager, which also contains the fragile axis/ScottPlot setup code. A bug introduced in one concern (e.g., a time shift calculation) can accidentally corrupt another (e.g., an axis rule).

Adding a new channel currently requires touching:
1. `GetChannelsWithRaw()` — yield the new channel
2. The Y-axis switch expression (C3 in audit) — map channel to axis
3. `SetupAllAxes()` — create and hide the new axis field
4. `ApplyAxisLocksForActiveProfile()` — add scale lock
5. `_dragScales` dict — add scale entry
6. `_speedScales` dict — add scale entry
7. `ChannelColorMap` — add color
8. `Config.ChannelVisibility` constructor — add default state
9. `ChannelToggleBar` channel list in `MainForm` — add to UI

**9 locations for 1 new channel.** This is O(n) in the number of channels.

**Break point:** Channels 11+; any channel requiring non-standard axis behavior.

---

## Structural Risk 4 — `RunData` is a Dual-Purpose Model ⚠️ MEDIUM

```csharp
public class RunData
{
    public List<DataPoint> DataPoints { get; set; }        // Castle: real data
    public Dictionary<string, List<DataPoint>> Data { get; set; }  // RaceBox: real data
    public Dictionary<string, List<RaceBoxPoint>> RaceBoxData { get; set; } // unused
    public bool IsRaceBox { get; set; }                    // discriminator
}
```

The model has two incompatible shapes held in the same class, distinguished by a boolean flag. Code that accesses `RunData` must always check `IsRaceBox` first, or it will silently get the wrong data:

- A Castle run accessed via `.Data` → empty dictionary
- A RaceBox run accessed via `.DataPoints` → list of time-only dummy entries (wrong)

The `DataPoint` model also has a `Y` shim field used only by RaceBox paths, adding to the confusion.

**What this means:** Every new piece of code that handles `RunData` must know the Castle/RaceBox distinction. There is no type safety to prevent mixing them up.

**Break point:** Any new processing step that doesn't carefully check `IsRaceBox`. Already caused at least one bug (PlotAllRuns logs `.DataPoints.Count` for RaceBox runs, which gives dummy-entry counts, not actual data counts).

---

## Structural Risk 5 — No Time Model ⚠️ MEDIUM

Three different time representations coexist:

| Source | Representation | Units |
|--------|---------------|-------|
| Castle CSV parsing | `rowIndex * 0.05` | Seconds (reconstructed) |
| Castle time shifting | `run.TimeShiftMs / 1000.0` | Milliseconds stored, seconds applied |
| RaceBox timestamps | `TimeSpan` (relative to first point) | .TotalSeconds for plotting |
| Global Castle offset | `_castleTimeShift` (in PlotManager) | Seconds |

The combined time calculation for a Castle point at plot-time is:
```
finalTime = dp.Time + (run.TimeShiftMs / 1000.0) + _castleTimeShift
```

For a RaceBox point:
```
finalTime = p.Time + (run.TimeShiftMs / 1000.0)
```

The `_castleTimeShift` is **NOT** applied to RaceBox runs. This is intentional but undocumented. If a user shifts Castle globally, RaceBox and Castle drift apart in a way that isn't immediately obvious.

**Break point:** Any feature that needs to correlate Castle and RaceBox time (e.g., "show me what the RaceBox speed was at this Castle RPM peak"). The time model is too fragmented to support this cleanly.

---

## Structural Risk 6 — Services Call UI Directly ⚠️ MEDIUM

```csharp
// CsvLoader.cs — inside a data parsing method
MessageBox.Show("No drag pass detected in this log.\nAuto-trim was skipped.",
    "DragOverlay", MessageBoxButtons.OK, MessageBoxIcon.Warning);

// RaceBoxLoader.cs — inside a parsing method
MessageBox.Show("This file is not a valid RaceBox export...",
    "Invalid File", MessageBoxButtons.OK, MessageBoxIcon.Warning);
```

Both loaders reach into the UI layer from what should be a pure data layer. This means:
- They cannot be unit tested (require a live UI thread)
- They cannot be used headlessly (e.g., batch processing, command-line export)
- They block the background thread they're called from (`Task.Run(() => loader.Load(...))`) — this is actually a cross-thread `MessageBox.Show` which works on WinForms but is technically incorrect since `MessageBox` should only be called from the UI thread

**Break point:** Any attempt to add automated testing, batch processing, or a non-WinForms front end.

---

## Structural Risk 7 — No Cancellation on Async Operations ⚠️ LOW-MEDIUM

All load operations are `async void` firing `Task.Run(() => loader.Load(...))` with no `CancellationToken`. If a user:
1. Clicks "Load Run 1" — starts loading a large file
2. Immediately clicks "Load Run 1" again before the first completes

Two concurrent tasks write to `run1`. The second task's result may arrive first (if it's a smaller file), and then the first overwrites it. The UI will show the second file's name but the first file's data.

Currently with Castle logs this race condition is extremely unlikely (files are small, loads are fast). But with large RaceBox files from long sessions, it is plausible.

**Break point:** Large RaceBox files; fast repeated clicking; slow disk I/O.

---

## What Would Break First if the Project Grew?

Ranked by probability and impact:

### 1. Adding a 4th Castle slot (HIGH probability, HIGH impact)
The 6-field system and all its switch/case duplications make this a major surgery. A slot refactor to `Dictionary<int, RunData>` on `MainForm` is the highest-value structural change possible.

### 2. Adding more channels (MEDIUM probability, HIGH effort)
The 9-location cost per channel is unsustainable. If channels 11–15 are ever needed (governor gain, motor timing 2, etc.), the setup code in `PlotManager` becomes unworkable. A channel registry (single source of truth: name, axis, color, scale, default visibility) would cut the per-channel cost from 9 locations to 1.

### 3. Any automated testing (HIGH value, currently impossible)
Because `CsvLoader` and `RaceBoxLoader` call `MessageBox.Show()`, they cannot be unit tested without a live UI. This means load logic regressions are only caught by manual testing. As the detection logic grows more complex (debounce, multi-trigger scoring), this is a mounting risk.

### 4. User wanting to select which RaceBox run to load (MEDIUM probability)
`LoadHeaderOnly` identifies the "first complete run" and `LoadTelemetry` always uses that index. There is no UI to pick a different run from the file. A user with multiple runs in one RaceBox export has no way to choose which one to overlay. The run selection UI would be straightforward but requires changes to the load flow.

### 5. Performance with 6 runs loaded (MEDIUM probability)
The O(n log n) sort-per-scatter in mouse hover (audit: C5, M5) will become noticeably laggy with 6 full runs loaded (~18 scatters × 600 points each). The current design has no render budget or throttle on the hover path.

---

## Positive Architecture Notes

Before recommendations: several things are done well.

- **`PlotManager` encapsulates ScottPlot completely.** `MainForm` never touches the ScottPlot API directly. This means a ScottPlot version upgrade only requires changes in `PlotManager`.
- **`ChannelColorMap` and `LineStyleHelper` are clean and isolated.** Adding color/style changes is easy and safe.
- **`ConfigService` is decoupled from everything.** It holds no dependencies on the plot or UI layer.
- **The `AxisRules` pattern for Y-locking is correct and stable.** It was discovered the hard way but is now a solid, consistent pattern.
- **`ThrottlePercent` is a pure function with no side effects.** Easy to test if needed.
- **`Logger.Init(bool)` makes logging opt-in.** Zero cost in production.

---

## Recommendations by Effort

### Quick (< 1 day): Reduce immediate risk
1. **Extract a `LoadRaceBoxSlot(int)` shared method** — eliminates the three near-identical RaceBox handlers and all three missing-null-check bugs at once (audit: C4, M16, M17).
2. **Add `_ => throttleAxis` to the Y-axis switch** — 2-minute fix for a crash (audit: C3).
3. **Move `MessageBox.Show` out of loaders** — have them throw typed exceptions (`InvalidCastleLogException`, `InvalidRaceBoxFileException`) and show the MessageBox in MainForm's catch block. Enables future testing.

### Medium (1–3 days): Structural improvement
4. **Replace `run1`–`run6` with `Dictionary<int, RunData> _runs`** — eliminates the slot duplication throughout MainForm. `PlotAllRuns()` becomes `_plotManager.PlotRuns(_runs)`. Adding/removing slots stops requiring form surgery.
5. **Introduce a `ChannelDefinition` record** — a single place that holds `(string Name, IYAxis Axis, ScottPlot.Color Color, double YMin, double YMax)`. Populate it at `PlotManager` construction. The Y-axis switch, `GetChannelsWithRaw`, `SetupAllAxes`, and the scale dicts all collapse into a single loop over this registry.
6. **Fix all `double.TryParse` and `DateTime.Parse` calls** to use `CultureInfo.InvariantCulture` — protects all international users (audit: C2, M2).

### Long-term (> 3 days): Architectural separation
7. **Split `RunData` into `CastleRunData : RunData` and `RaceBoxRunData : RunData`** — eliminates the boolean discriminator and the dual-property confusion. Each subtype carries only the data it needs.
8. **Extract a `RunSlotManager`** from MainForm — holds the `Dictionary<int, RunData>` and per-slot UI state (button text, enabled state, shift value). MainForm becomes an event router; RunSlotManager owns slot lifecycle.
9. **Add `CancellationToken` to async load paths** — prevents race conditions on fast re-load; enables "cancel loading" UI feedback.

---

## Architecture Health Summary

| Dimension | Status | Risk |
|-----------|--------|------|
| Separation of concerns | Poor — MainForm is God Object, services touch UI | High |
| Data model clarity | Poor — RunData is dual-purpose with boolean discriminator | Medium |
| Extensibility (new slots) | Poor — hard-coded 6-slot system | High |
| Extensibility (new channels) | Fair — 9 locations per channel, but pattern is consistent | Medium |
| ScottPlot isolation | Good — all ScottPlot in PlotManager | Low |
| Config isolation | Good — ConfigService is clean | Low |
| Testability | Poor — services call MessageBox, no interfaces | High |
| Async correctness | Fair — works, but no cancellation or race protection | Medium |
| Time model | Poor — 3 representations, implicit rules | Medium |
| Dead code volume | Moderate — ~150 lines of dead/unused code | Low |

**Overall:** The codebase is in a common "prototype that became production" state. The core patterns are sound (AxisRules, PlotManager isolation, ConfigService), but the growth path is blocked by the God Form and the hard-coded slot system. The good news is that the architectural debt is concentrated in two files (MainForm, PlotManager) and fixing those two files would resolve the majority of the risk.

---

*Architecture analysis by Claude Sonnet 4.6 — 2026-03-05*
