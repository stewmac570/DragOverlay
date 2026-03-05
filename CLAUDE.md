# DragOverlay — CLAUDE.md

## 🤖 Self-Learning Protocol (READ THIS FIRST)

Claude must follow this protocol every single session, no exceptions:

1. **Start of session** — Read this entire file before doing anything
2. **During session** — Note new discoveries, decisions, and patterns
3. **End of session** — Append findings to the relevant sections below
4. **Never delete history** — Only append. The log is permanent record.
5. **If something is unclear** — Say so explicitly. Never guess or assume.

> This file is a living document. It gets smarter every session. Treat it that way.

---

## Project Overview

**DragOverlay** (repo: `CastleOverlayV2`) is a Windows WinForms app for RC drag racing telemetry.
It overlays up to 3 Castle ESC `.csv` logs and optional RaceBox GPS logs on a shared ScottPlot chart,
aligned to a common launch point (t = 0), with Castle-style colors, hover cursor, axis locking, and channel toggles.

Castle Link 2 is the UX/behavior reference for plotting, hover, alignment, and colors.

---

## Current State (update every session)

| Field              | Value                        |
|--------------------|------------------------------|
| Last stable version | v1.12                       |
| Active branch       | claude/gracious-blackburn   |
| In progress         | Full code audit             |
| Known broken        | See audit — 5 critical issues identified (see Docs/audit/01-full-audit-2026-03-05.md) |
| Next priority       | Fix C1–C4 crash bugs; fix M9 RPM replot bug |
| Last session date   | 2026-03-05                  |

---

## Tech Stack

| Component        | Version / Detail                          |
|-----------------|-------------------------------------------|
| .NET            | 8.0 (WinForms)                            |
| ScottPlot       | **v5.0.8 — PINNED, do not upgrade**       |
| CsvHelper       | v33.1.0                                   |
| Newtonsoft.Json | Config persistence                        |
| Platform        | Windows only                              |

---

## Project Structure

```
src/CastleOverlayV2/
├── Program.cs              # Entry point
├── MainForm.cs             # Main form logic
├── MainForm.Designer.cs    # Auto-generated UI
├── Controls/               # Custom UI panels (toggle bar, etc.)
├── Models/                 # Data structures: RunData, DataPoint, Config, RaceBoxData
├── Services/               # Logic layer: CsvLoader, RaceBoxLoader, ConfigService, Logger, ThrottlePercent
├── Plot/                   # ScottPlot chart setup: PlotManager
├── Utils/                  # Helpers: ColorMap, LineStyleHelper
├── Resources/              # Icons, screenshots
Docs/                       # Feature specs, delivery plan, structure, dev log
config/                     # config.json (user prefs, not committed)
logs/                       # Test Castle CSV files
```

---

## Key Behaviors & Rules

### Launch Alignment
- t = 0 = launch point: `Throttle > 1.65ms` AND `PowerOut > 10W`
- Chart shows `-0.5s → +2.5s` around launch only
- All logs re-zeroed to this point

### Plot / Axes
- X-axis zoom only — Y-axis is locked via `AxisRules`
- Each channel has a hidden, dedicated Y-axis (no label noise)
- Zoom is X-only; never allow free Y zoom
- Castle-style colors: Run 1 = blue, Run 2 = red, Run 3 = green
- Line styles: Run 1 = solid, Run 2 = dashed, Run 3 = dotted

### Channels
- RPM, Throttle, Voltage, Current, Ripple, PowerOut, ESC Temp, Motor Timing, Acceleration, GovGain
- `"Temperature"` CSV column → mapped to `"ESC Temp"`
- RPM: toggle between 2P (raw) and 4P (halved) — persisted to config.json
- ThrottlePercent: derived channel from throttle pulse width

### RaceBox Integration
- One RaceBox slot per Castle slot (Run 1–3)
- RaceBox aligned to Castle launch time
- Plots: GPS Speed (mph), G-Force X, split times (6ft, 66ft, 132ft, etc.)
- Discipline labels shown at top of plot

### Config System
- Stored at: `%AppData%\Roaming\DragOverlay\config.json`
- Stores: channel visibility defaults, RPM mode, launch detection threshold, debug logging flag, build number
- Managed by `ConfigService.cs`

---

## Development Ground Rules

1. **Never guess ScottPlot or CsvHelper syntax** — verify against official v5 docs
2. **ScottPlot v5.0.8 is pinned** — do not upgrade mid-feature
3. **1 feature = 1 Git branch** — finish, test, commit, merge, then next
4. **Always deliver full files** — never partial broken snippets
5. **Match Castle Link 2 behavior** — hover, colors, alignment, axis layout
6. **No "maybe this works" code** — only verified, tested changes
7. **After EVERY session, update this file** — pitfalls, decisions, patterns discovered
8. **If unsure about anything ScottPlot-related, say so** — never guess the API
9. **Check Dev Log before starting** — avoid repeating past mistakes
10. **Check Verified Patterns before writing new code** — reuse what works

---

## Git Workflow

- Branch from `main` for each feature: `feature/<name>`
- PRs merge back to `main`
- Commit only stable, testable chunks
- Tag releases with version number (e.g., `v1.12`)

---

## Verified Patterns ✅ (tested and confirmed working — Claude appends here)

These are patterns confirmed to work. Use these before writing anything new.

- **Launch detection** — debounce logic implemented in v1.11. Use this pattern, don't reinvent it.
- **Axis locking** — `AxisRules` must be applied *after* plot setup, not before.
- **Re-plotting** — clear axes properly in `PlotManager` to avoid axis duplication.
- **Y-axis drift** — always apply `AxisRules` after every plot refresh, not just initial setup.
- **Y-axis switch expression** — `PlotManager.PlotRuns` has a C# switch expression for channel→axis mapping. Always add a `_ => throttleAxis` default arm, or a `SwitchExpressionException` will crash on any unknown channel name.
- **`SetSpeedMode` is NOT self-contained** — it only sets an enum; calling it alone does nothing visible. Must follow with `SetupAllAxes()` + `RefreshPlot()`, or better yet just call `PlotAllRuns()` which handles the whole pipeline.
- **`PlotAllRuns()` is the canonical replot call** — use it rather than assembling a partial run dict manually. `OnRpmModeChanged` is a known exception that only plots Castle runs (bugs: M9 in audit).
- **RaceBox slot mapping** — Castle slots 1/2/3 use `RunData` vars `run1/run2/run3`; RaceBox slots 1/2/3 use `run4/run5/run6` and plot manager slots 4/5/6. This is NOT consistent with the "RaceBox slot index" shown in the UI (which is 1/2/3). Always use plot manager slot = UI slot + 3 for RaceBox.

> Claude: When you confirm a new pattern works, add it here with a short description.

---

## Common Pitfalls ⚠️ (append only — never delete)

These are real bugs and traps encountered in this project. Read before coding.

- **ScottPlot v4 vs v5 API differences** — always confirm you're using v5 syntax. They are not compatible.
- **Axis duplication when re-plotting** — clear axes properly in `PlotManager` before re-drawing.
- **Y-axis drift when zoom is not locked** — always apply `AxisRules` after plot setup.
- **Launch detection firing too early** — debounce logic required (implemented in v1.11).
- **`double.TryParse` without CultureInfo** — if parsing Castle CSV field values, always pass `CultureInfo.InvariantCulture`. The CSV reader is set to invariant culture but `GetDouble()` in `CsvLoader` uses the ambient locale (audit: M2).
- **`DateTime.Parse` without CultureInfo in RaceBoxLoader** — RaceBox timestamps must use `CultureInfo.InvariantCulture` or they parse incorrectly on European-locale machines (audit: C2).
- **RaceBox `points` can be empty** — always null/count-check `points` before accessing `.First()` or `.Last()` after `LoadTelemetry`. Crashes in LoadRaceBox1 and LoadRaceBox3 confirmed (audit: C1, C4).
- **`SyncRunTypeUI` creates a new `ToolTip` object every call** — store the ToolTip as a field, never `new ToolTip()` in a method that is called repeatedly (audit: M8).
- **`OnRpmModeChanged` only replots Castle runs** — it builds its own run dict with only run1-3. RaceBox overlays disappear when RPM mode changes. Use `PlotAllRuns()` instead (audit: M9).
- **`RunData` is dual-purpose** — Castle runs use `.DataPoints`; RaceBox runs use `.Data` dictionary. Never access `.DataPoints` on a RaceBox `RunData` expecting real values — it contains time-only dummy entries (audit: L9).
- **`GovGain` is not implemented** — it appears in docs/README but has no axis, no color, no channel in `GetChannelsWithRaw`. Do not reference it in code (audit: L10).

> Claude: When you hit a new bug or unexpected behavior, add it here immediately.

---

## Dev Log 📓 (append only — never delete or edit past entries)

This is the permanent record of what happened each session. Claude appends an entry at the end of every session.

### Format
```
### YYYY-MM-DD
**Focus:** What was worked on
**Decisions:** Key choices made and why
**Completed:** What was finished
**Discovered:** New pitfalls or patterns found
**Next:** Recommended next step
```

---

### 2026-03-05 (entry 1)
**Focus:** CLAUDE.md setup and self-learning loop established
**Decisions:** Added self-learning protocol, verified patterns, dev log, and current state sections
**Completed:** CLAUDE.md v2 created
**Discovered:** Nothing new yet — first session with new format
**Next:** Run `/init` in Claude Code to confirm file is being read correctly, then begin next feature

---

### 2026-03-05 (entry 2)
**Focus:** Full codebase audit — all 15 source files read and analysed
**Decisions:** Audit saved to `Docs/audit/01-full-audit-2026-03-05.md`; CLAUDE.md updated with findings
**Completed:** 32 issues catalogued (5 critical, 17 medium, 10 low); quick-win list compiled
**Discovered:**
- 2 crash paths in RaceBox loading (empty `points` list, missing null checks in slots 1 + 3)
- `DateTime.Parse` without `CultureInfo.InvariantCulture` in `RaceBoxLoader` — locale bug
- Y-axis switch expression has no default arm — `SwitchExpressionException` on unknown channel
- `OnRpmModeChanged` only replots Castle runs — RaceBox overlays vanish when RPM mode changes
- `SyncRunTypeUI` leaks a `ToolTip` object on every call
- Logger file grows unbounded (startup truncation commented out)
- `RunData.RaceBoxData` dictionary is completely unused (dead field)
- `GovGain` appears in docs but is never implemented in code
- `InitializeEllipsisMenus` is defined but never called — entire menu system is dead code
- `LoadCsvButton_Click` is dead code that loads run1 with no chart update
- `_isFourPoleMode` duplicated in MainForm and PlotManager — must stay in sync manually
**Next:** Tackle quick wins first (C4, C3, M9, L5, L6, L7) — all under 5 min each. Then C1+C2 (crash bugs). Then M8 (ToolTip leak) and M16 (missing try/catch on async handlers).

---

### 2026-03-05 (entry 3)
**Focus:** Full architectural analysis
**Decisions:** Analysis saved to `Docs/audit/02-architecture-2026-03-05.md`
**Completed:** 7 structural risks identified and ranked; break points documented; recommendations by effort level
**Discovered:**
- The 6-named-field slot system (`run1`..`run6`) is the single highest-risk structural issue — adding a 4th Castle slot touches ~15 locations
- `RunData` dual-purpose model is a type-safety hole: accessing `.DataPoints` on a RaceBox run gives dummy data silently
- No time model: 3 representations coexist (index×0.05, TimeShiftMs, TimeSpan), with implicit undocumented rules
- Services (`CsvLoader`, `RaceBoxLoader`) call `MessageBox.Show()` directly — unit testing impossible, cross-thread risk
- Line style slot-index mapping (`LineStyleHelper.GetLinePattern(slot - 1)`) is fragile and undocumented
- No `CancellationToken` on async loads — rapid re-loads can write to the same run variable concurrently
**Next:** Fix crash bugs (C1–C4), then the `run1..run6` → `Dictionary<int, RunData>` refactor is the highest-value structural improvement.

---

> Claude: Add a new dated entry here at the end of every session. Never skip this step.
