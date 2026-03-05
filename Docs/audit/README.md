# DragOverlay — Audit Index
**Last updated:** 2026-03-05
**Branch audited:** `claude/gracious-blackburn` (based on v1.12)

All audits were conducted by Claude Sonnet 4.6 in a single session on 2026-03-05.
Every source file was read in full before any findings were written.

---

## File Index

| File | What it covers | Findings |
|------|---------------|----------|
| [01-full-audit-2026-03-05.md](#01--full-code-audit) | Every source file — patterns, bugs, edge cases, performance, maintainability | 5 critical, 17 medium, 12 low |
| [02-architecture-2026-03-05.md](#02--architecture-analysis) | Structural risks, God Object analysis, growth breakpoints | 7 structural risks |
| [03-plot-code-2026-03-05.md](#03--plotmanager-deep-dive) | PlotManager.cs line-by-line — ScottPlot v5 anti-patterns, inefficiencies | 17 findings |
| [04-error-handling-2026-03-05.md](#04--unhandled-exception-map) | Every throw site traced — what crashes the app and under what conditions | 11 findings |
| [05-csv-loading-2026-03-05.md](#05--csv-loading-failure-map) | CsvLoader + RaceBoxLoader — behaviour under malformed, missing, empty input | 20 findings |
| [06-fix-plan-2026-03-05.md](#06--prioritised-fix-plan) | Consolidated fix list — all findings ranked by priority with effort and session plan | 30 fix items |

---

## 01 — Full Code Audit

**File:** `01-full-audit-2026-03-05.md`
**Scope:** All 15 source files read in full — MainForm, PlotManager, CsvLoader, RaceBoxLoader, ConfigService, Logger, ChannelToggleBar, ColorMap, LineStyleHelper, RunData, DataPoint, Config, RaceBoxData, RaceBoxPoint, ThrottlePercent.

### Top 3 critical findings

**C1 — RaceBox crash on empty telemetry (`RaceBoxLoader.cs:151`)**
`DetectRaceBoxLaunchIndex` returns 0 as a fallback when no launch is detected. If `points` is empty (all rows failed the run-index filter), `points[0]` throws `ArgumentOutOfRangeException`. No guard exists before the access.

**C3 — Y-axis switch expression has no default arm (`PlotManager.cs:339`)**
The `channelLabel switch` expression that maps channel names to Y-axis objects has no `_ =>` arm. Any unrecognised channel name throws `SwitchExpressionException` at runtime. Propagates unhandled through every caller: channel toggle, delete, RPM mode, shift buttons.

**C4 — `LoadRaceBox1` and `LoadRaceBox3` crash on empty or null points (`MainForm.cs`)**
Both handlers call `points.First()` and `points.Last()` with no null/empty guard. If `LoadTelemetry` returns null (invalid file) or an empty list (telemetry header out of bounds), these throw `InvalidOperationException` / `NullReferenceException`. No try/catch wraps these handlers.

---

## 02 — Architecture Analysis

**File:** `02-architecture-2026-03-05.md`
**Scope:** Structural risks, God Object identification, break-point analysis, recommendations by effort tier.

### Top 3 structural risks

**Risk 1 — MainForm is a God Object (1,300 lines)**
Every feature touches MainForm. Adding a 4th Castle slot requires changes to ~15 locations: 6+ event handlers, `PlotAllRuns`, `DeleteRun`, `IsAnyRunLoaded`, `OnRpmModeChanged`, `ApplyRunTypeUI`, `SetShiftButtonsEnabled`, the Designer, and multiple `_plotManager` calls. The six named fields (`run1`–`run6`) make this unavoidable without a refactor.

**Risk 2 — Run slot system is hard-coded**
The mapping between UI labels ("RaceBox 2"), MainForm variables (`run5`), and PlotManager slots (5) is implicit and exists only in the pattern of field names and magic numbers. `LineStyleHelper.GetLinePattern(slot - 1)` is fragile: slot 4 → index 3 → Solid, which happens to match the intended pattern but would silently produce wrong line styles if slot numbering changed.

**Risk 3 — PlotManager mixes three unrelated concerns**
PlotManager handles (A) ScottPlot wrapping, (B) data transformation (RPM scaling, time shifting), and (C) visibility state management. A bug introduced in one concern can corrupt another. Adding a new channel costs 9 locations. A channel registry (single source of truth) would cut this to 1.

---

## 03 — PlotManager Deep Dive

**File:** `03-plot-code-2026-03-05.md`
**Scope:** All 999 lines of `PlotManager.cs` — ScottPlot v5 usage correctness, performance on the hot path (hover), structural issues.

### Top 3 critical findings

**PM1 — `FitToData()` renders before autoscaling — does nothing (`PlotManager.cs:227`)**
`_plot.Refresh()` is called before `_plot.Plot.Axes.AutoScale()`. ScottPlot renders with the old limits, then the limits change without a re-render. The "Fit to Data" action produces no visible result.

**PM2/PM3 — O(n log n) sort + O(n) index scan on every mouse pixel (`PlotManager.cs:800`)**
`FormsPlot_MouseMove` finds the nearest data point using `OrderBy(...).First()` — a full sort of all points on every scatter for every pixel of mouse movement. With 6 runs loaded: ~18 scatters × 500 points × log(500) ≈ 81,000 comparisons per pixel. A second O(n) linear scan then finds the same point's index. Both passes should be a single O(n) scan.

**PM5 — Near-zero Y filter creates gaps in G-Force trace (`PlotManager.cs:669`)**
`Where(p => Math.Abs(p.Y) > 0.01)` silently removes data points where G-Force is at or near zero. G-Force legitimately crosses zero during a drag pass. The filter creates an artificial gap in the G-Force line at every zero-crossing. The filter is harmless for speed (always positive during a run) but harmful for G-Force.

---

## 04 — Unhandled Exception Map

**File:** `04-error-handling-2026-03-05.md`
**Scope:** Every throw site in the codebase traced through all callers to determine whether an exception would be caught. Ranked by crash likelihood.

### Top 3 critical findings

**Finding 1/2 — `LoadRaceBox1Button_Click` and `LoadRaceBox3Button_Click` have no try/catch at all**
Any exception from `LoadHeaderOnly`, `LoadTelemetry`, `points.First()`, or `points.Last()` propagates as an unhandled exception in an `async void` handler. WinForms dispatches this to the application's unhandled exception handler — typically a crash dialog followed by termination. The Castle load handlers correctly wrap the same operations in try/catch. This asymmetry is the root cause.

**Finding 4 — `Program.Main` has no top-level try/catch**
`new ConfigService()` calls `Directory.CreateDirectory(appDataPath)`, which throws `UnauthorizedAccessException` or `IOException` if `%AppData%\Roaming\DragOverlay` is not writable (read-only profile, network home directory, etc.). The app crashes before any window opens. The user sees a bare CLR error dialog with a stack trace.

**Finding 6 — PlotManager switch expression (`SwitchExpressionException`) propagates to all callers**
The Y-axis switch has no default arm (cross-reference: C3 from audit 01). The exception propagates through `PlotAllRuns` → `OnChannelVisibilityChanged`, `DeleteRun`, `OnRpmModeChanged`, and all shift handlers. None of those callers have try/catch. A single unrecognised channel name crashes the app during any of these common operations.

---

## 05 — CSV Loading Failure Map

**File:** `05-csv-loading-2026-03-05.md`
**Scope:** `CsvLoader.cs` and `RaceBoxLoader.cs` — what happens to every execution path when the input is empty, truncated, missing columns, or from a non-English locale.

### Top 3 critical findings

**CL3 — `CsvConfiguration(CultureInfo.InvariantCulture)` does not protect `double.TryParse`**
The `CsvConfiguration` culture setting only affects CsvHelper's internal typed reads. Since `GetField<string>` is used everywhere and values are parsed manually with `double.TryParse`, the ambient OS locale is used for every numeric parse. On a German/French locale, `"8.4"` fails to parse (`.` is the thousands separator). All Castle channels return 0.0 silently. The `CsvConfiguration` line gives a false sense of locale safety.

**CL2 — Missing "Power-Out" column produces a silent empty result**
If the "Power-Out" column is missing or renamed, `csv.GetField<string>("Power-Out")` returns null (due to `MissingFieldFound = null`). `powerOut` stays 0.0. The in-loop launch filter (`powerOut >= 5.0`) never triggers. Every row is skipped. The method returns an empty `RunData` with no `MessageBox`, no log warning visible to the user, and no indication that anything went wrong. The filename appears in the UI button, implying a successful load.

**RT1/RT2 — `LoadTelemetry` returns null or empty list; `LoadRaceBox1/3` crash on both**
`LoadTelemetry` returns `null` for an invalid file signature (after showing a MessageBox) and returns an empty `List<RaceBoxPoint>` for a missing telemetry section. Both `LoadRaceBox1Button_Click` and `LoadRaceBox3Button_Click` then call `points.First()` without any guard — crashing the app. `LoadRaceBox2Button_Click` handles both cases correctly with a null/count guard, revealing the asymmetry.

---

## 06 — Prioritised Fix Plan

**File:** `06-fix-plan-2026-03-05.md`
**Scope:** All 55 findings from audits 01–05 consolidated and ranked into 30 actionable fix items across 6 priority tiers. Includes effort estimates and a recommended 4-session execution plan.

### Top 3 highest-priority actions

**Fix #3 — Add default arm to PlotManager switch (2 minutes)**
`_ => throttleAxis` as the final arm of the Y-axis switch expression. Closes the `SwitchExpressionException` crash path that affects channel toggle, delete, RPM mode, and all shift operations.

**Fix #1/#2 — Add try/catch to all three RaceBox load handlers (~30 minutes total)**
Apply the same try/catch pattern already used by the Castle load handlers. All three RaceBox handlers (`LoadRaceBox1/2/3Button_Click`) need the same wrapper. This single change closes every CRITICAL unhandled exception path in the RaceBox loading flow.

**Fixes #6–9 — CultureInfo batch across 5 parse sites (~20 minutes total)**
Add `CultureInfo.InvariantCulture` to: `DateTime.Parse` in `RaceBoxLoader` (×2), `double.TryParse` in `CsvLoader.GetDouble()` and metadata section, `double.TryParse` for speed/G-Force in `LoadTelemetry`, `double.TryParse` for split times in `LoadHeaderOnly`. Eliminates every locale-related silent failure with one focused pass.

### Recommended session order

| Session | Items | What it achieves | Est. time |
|---------|-------|-----------------|-----------|
| **A** | #1–11 (P1+P2) | Every known crash path closed; locale failures fixed | ~75 min |
| **B** | #12–17 (P3) | Silent failures become visible errors; RPM mode fixed; ToolTip leak fixed | ~2 hr |
| **C** | #18–22 (P4) | Hover responsive with 6 loaded runs | ~2 hr |
| **D** | #29–30 (P6) | Structural: shared LoadRaceBoxSlot; exceptions out of loaders | ~2 days |

---

## Quick-reference: findings by severity

### Crash bugs (fix before any release)
| ID | File | Description |
|----|------|-------------|
| C3 / PM6 / 04§6 | `PlotManager.cs:339` | Switch expression no default arm — `SwitchExpressionException` |
| C4 / 04§1–2 | `MainForm.cs` | `LoadRaceBox1/3` no try/catch — crash on any exception |
| C1 / RT1 / 04§3 | `MainForm.cs` | `LoadRaceBox2` unprotected LoadHeaderOnly/LoadTelemetry block |
| 04§4 | `Program.cs` | No top-level try/catch — crash before window opens |
| 04§5 | `ConfigService.cs:79` | `File.WriteAllText` uncaught — crash on every channel toggle |

### Silent wrong output (fix to prevent user confusion)
| ID | File | Description |
|----|------|-------------|
| CL3 / M2 | `CsvLoader.cs` | All Castle channels = 0 on non-EN locale |
| RT4 | `RaceBoxLoader.cs` | Speed and G-Force = 0 on non-EN locale; nothing plots |
| RT3 / C2 | `RaceBoxLoader.cs:119` | `DateTime.Parse` crashes on some locales |
| CL2 | `CsvLoader.cs` | Missing "Power-Out" column → silent empty load |
| M9 | `MainForm.cs` | RPM mode change wipes RaceBox overlays off chart |
| PM5 | `PlotManager.cs:669` | G-Force gaps at every zero-crossing |
| PM1 | `PlotManager.cs:227` | FitToData does nothing (Refresh/AutoScale order) |

---

*Index compiled by Claude Sonnet 4.6 — 2026-03-05*
