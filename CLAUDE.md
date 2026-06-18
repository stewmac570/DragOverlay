# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

**DragOverlay** (repo: `CastleOverlayV2`) is a Windows WinForms tool that overlays up to 3 Castle ESC `.csv` logs and optional RaceBox GPS logs on a single ScottPlot chart for RC drag-racing analysis. Castle Link 2 is the reference for hover, colors, alignment, and axis layout.

## Build / Run

The repo has no test project and no automated lint. Build, run, and publish from `src/CastleOverlayV2/`:

```powershell
dotnet build src/CastleOverlayV2/CastleOverlayV2.csproj -c Release
dotnet run   --project src/CastleOverlayV2/CastleOverlayV2.csproj
dotnet publish src/CastleOverlayV2/CastleOverlayV2.csproj -c Release -r win-x64 --self-contained false -o publish/win-x64
```

The Inno Setup installer script at `installer/installer.iss` packages `publish/win-x64/` — bump `MyAppVersion` and `Models/Config.cs::BuildNumber` together when releasing. Visual Studio 2022+ also builds via `src/CastleOverlayV2/CastleOverlayV2.sln`.

## Pinned dependencies

Treat these versions as constraints, not suggestions:

- **ScottPlot.WinForms 5.0.55** (project pins ScottPlot v5; ScottPlot v4 syntax is incompatible — never mix)
- **CsvHelper 33.1.0**
- **Newtonsoft.Json 13.0.3**
- **.NET 8 (net8.0-windows)**, WinForms, Windows-only

## Architecture

Data flows in one direction: **CSV → Loader → `RunData` → `PlotManager` → ScottPlot `FormsPlot`**. `MainForm` owns the slots and wires button clicks into the pipeline. `ConfigService` reads/writes user prefs to `%AppData%\Roaming\DragOverlay\config.json`.

### Slot model (read carefully — it's load-bearing)

`MainForm` holds **six** `RunData` fields, but they represent two parallel sets:

- Castle slots 1–3 → `run1`, `run2`, `run3` (fields on `MainForm`)
- RaceBox slots 1–3 → `run4`, `run5`, `run6` in `MainForm`; surfaced separately as `raceBox1/2/3` of type `RaceBoxData`
- In `PlotManager`, RaceBox uses **plot slot = UI slot + 3** (so UI RaceBox slot 1 is plot slot 4)
- `LineStyleHelper.GetLinePattern(slot - 1)` indexes off the plot slot — keep that mapping intact when touching either side

`RunData` is dual-purpose: Castle runs populate `.DataPoints` (flat sample list); RaceBox runs populate `.Data` (channel → list) and set `IsRaceBox = true`. Accessing `.DataPoints` on a RaceBox run returns time-only dummies, not real values.

### Plot pipeline

- `PlotManager.PlotAllRuns()` is the canonical replot. Prefer it to assembling a partial run dict by hand.
- `SetSpeedMode` only flips an enum — it has no visible effect on its own; follow with a replot.
- Y axes are per-channel and hidden; X-only zoom is enforced via `ScottPlot.AxisRules` and must be reapplied after any plot rebuild.
- The channel → Y-axis switch expression in `PlotManager` must keep its default arm (`_ => throttleAxis`) — without it, an unknown channel name throws `SwitchExpressionException` and crashes the form.
- Castle colors: Run 1 blue, Run 2 red, Run 3 green. Line styles: solid / dashed / dotted by slot.

### Launch alignment

- t = 0 is detected as `Throttle > 1.65ms` AND `PowerOut > 10W` with debounce (v1.11+).
- Chart window is fixed `-0.5s → +2.5s` around launch; all logs are re-zeroed to this point.
- The CSV column literally named `Temperature` is mapped to the `ESC Temp` channel.
- RPM has a 2P/4P toggle (4P halves the value); the choice is persisted in config.

## Locale / parsing rules

The Castle CSV header sets `CsvConfiguration(CultureInfo.InvariantCulture)`, but that does **not** apply to manual `double.TryParse` / `int.Parse` / `DateTime.Parse` calls on extracted fields. Every numeric or date parse in `CsvLoader`, `RaceBoxLoader`, and anywhere CSV strings are re-parsed must pass `CultureInfo.InvariantCulture` explicitly — otherwise European-locale machines silently parse channels as 0. Recent commits (`cb388e5`) fixed this across the loaders; preserve the pattern when adding new fields.

## Config

`%AppData%\Roaming\DragOverlay\config.json` stores channel visibility, RPM mode, alignment threshold, debug-logging flag, and build number. `ConfigService.Save()` is called on every channel toggle and RPM-mode change, so it's on the hot path — keep it cheap and don't introduce blocking I/O.

`Services/Logger.cs` writes to `%AppData%\Roaming\DragOverlay\debug_log.txt` only when `EnableDebugLogging` is true in config.

## Conventions and gotchas

- **Empty `points` from `RaceBoxLoader`**: `LoadTelemetry` can return `null` or an empty list for malformed/idle files. Always null- and count-check before `.First()` / `.Last()` / `points[launchIndex]`. RaceBox load handlers in `MainForm` were retrofitted with try/catch (`3c8f431`) — keep that pattern when adding new RaceBox slots.
- **Missing `Power-Out` column**: `CsvLoader` now warns and aborts (`e2035d4`) rather than silently returning empty `DataPoints`. Don't reintroduce the silent path.
- **Hover scan**: `PlotManager`'s mouse hover is now a single linear pass (`f56292f`) — don't re-add per-pixel `Array.Sort` / `BinarySearch` over every scatter.
- **GovGain**: referenced in docs and README but **not implemented** — no axis, no color, no entry in `GetChannelsWithRaw`. Treat it as not-yet-supported, not as broken.
- **`run1`..`run6` field layout**: every new Castle/RaceBox feature touches ~15 sites in `MainForm` because of this. A `Dictionary<int, RunData>` refactor is on the wishlist but not done — when you must add a slot, grep for all six names rather than copy/pasting one block.
- Services (`CsvLoader`, `RaceBoxLoader`) currently call `MessageBox.Show` directly. That makes them un-unit-testable and unsafe off the UI thread — don't add new direct UI calls there if there's another way.

## Project layout

```
src/CastleOverlayV2/
├─ Program.cs, MainForm.cs(/.Designer.cs/.resx)
├─ Controls/   ChannelToggleBar
├─ Models/     RunData, DataPoint, Config, RaceBoxData, RaceBoxPoint
├─ Services/   CsvLoader, RaceBoxLoader, ConfigService, Logger, ThrottlePercent
├─ Plot/       PlotManager (all ScottPlot setup lives here)
├─ Utils/      ColorMap, LineStyleHelper
└─ Resources/  icon + screenshots
Docs/          FEATURES, DELIVERY_PLAN, STRUCTURE, DEVELOPMENT_LOG, audit/
installer/     installer.iss (Inno Setup)
publish/       output of `dotnet publish` (ignored by IDE, used by installer)
Tests/         WorkingMWE (rollback reference only — no test project)
```

## Workflow norms (from project docs)

- One feature per branch (`feature/<name>`), merge back to `main`.
- Deliver full files, not partial snippets. Commit only stable, testable chunks.
- Verify ScottPlot v5 / CsvHelper API against official docs — don't guess.
- `Docs/audit/` contains a recent multi-pass audit (sessions 2026-03-05 / 06). When touching `PlotManager`, `CsvLoader`, or the RaceBox handlers, skim those files first — most known landmines are catalogued there.
