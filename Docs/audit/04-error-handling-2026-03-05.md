# DragOverlay — Unhandled Exception Audit
**Date:** 2026-03-05
**Based on:** Full codebase read (all source files) — cross-reference audit 01

---

## Method

For each file, every path that can throw was traced from the throw site through all callers. A site is **unhandled** if no `try/catch` intercepts it before it reaches the WinForms message loop (for `async void` handlers) or before the process terminates (for synchronous paths and `Main`).

Crash likelihood is rated:
- **CRITICAL** — crashes the app or silently corrupts state on common user actions
- **HIGH** — crashes on any file with a non-English locale, or on the first unexpected file content
- **MEDIUM** — crashes in realistic but non-default conditions
- **LOW** — crashes only under degraded conditions (disk full, permissions, etc.)

---

## Finding 1 — `LoadRaceBox1Button_Click` has no try/catch at all ⚠️ CRITICAL

**File:** `MainForm.cs` ~line 462
**Handler type:** `async void`

```csharp
private async void LoadRaceBox1Button_Click(object sender, EventArgs e)
{
    // ... UI setup ...
    var loader = new RaceBoxLoader();
    var header = loader.LoadHeaderOnly(path);        // ← throws; not caught
    // ...
    var points = await Task.Run(() => loader.LoadTelemetry(path, ...)); // ← throws; not caught
    // ...
    var startTime = points.First().Time;   // ← throws if points is null/empty; not caught
    var endTime   = points.Last().Time;    // ← same
}
```

**What throws:**

| Site | Exception | Trigger |
|------|-----------|---------|
| `new StreamReader(path)` in `LoadHeaderOnly` | `FileNotFoundException` | File deleted between dialog and load |
| `new StreamReader(path)` in `LoadHeaderOnly` | `UnauthorizedAccessException` | No read permission |
| `int.Parse(allRows[7][1])` in `LoadHeaderOnly` L231 | `FormatException` | Malformed RaceBox CSV header |
| Explicit `throw new Exception(...)` in `LoadHeaderOnly` L221,225,229,237 | `Exception` | Row count too short, wrong format |
| `DateTime.Parse(telemetryRows[0][timeIndex])` in `LoadTelemetry` L119 | `FormatException` | Non-EN locale or malformed timestamp |
| `DateTime.Parse(row[timeIndex])` in `LoadTelemetry` L125 | `FormatException` | Same, inside loop |
| `points[launchIndex]` in `LoadTelemetry` L152 | `ArgumentOutOfRangeException` | Empty telemetry section |
| Explicit `throw new Exception(...)` in `LoadTelemetry` L98,115 | `Exception` | Column not found in header |
| `points.First()` in handler | `InvalidOperationException` | Empty return from `LoadTelemetry` |
| `points.Last()` in handler | `InvalidOperationException` | Same |

**Outcome:** Any of these exceptions, thrown inside an `async void` handler, is dispatched back to the WinForms `SynchronizationContext` as an **unhandled exception**. The default WinForms behavior is to show the "Application has encountered an error" crash dialog and terminate, or (with `Application.SetUnhandledExceptionMode`) silently crash.

**The user loaded a file. The app crashed.** No error message. No recovery.

---

## Finding 2 — `LoadRaceBox3Button_Click` has no try/catch at all ⚠️ CRITICAL

**File:** `MainForm.cs` ~line 684
**Handler type:** `async void`

Identical structure to Finding 1. Every throw site listed above applies equally. Additionally:

```csharp
if (points.Count > 0)   // ← this check is AFTER the await, but points.First()/Last() follow
```

The count check protects the first/last access but does not protect against exceptions thrown *inside* `LoadHeaderOnly` or `LoadTelemetry`.

---

## Finding 3 — `LoadRaceBox2Button_Click` has no try/catch for thrown exceptions ⚠️ CRITICAL

**File:** `MainForm.cs` ~line 594
**Handler type:** `async void`

This handler adds a null/count guard on `points`:

```csharp
if (points == null || points.Count == 0)
{
    // show warning, return
}
```

This guards against `points.First()`/`points.Last()` but does **not** protect against any exception thrown *inside* `loader.LoadHeaderOnly(path)` or `await Task.Run(() => loader.LoadTelemetry(...))`. All exceptions from Finding 1's table still propagate unhandled.

---

## Finding 4 — `Program.cs` has no try/catch anywhere ⚠️ CRITICAL

**File:** `Program.cs`

```csharp
static void Main()
{
    ApplicationConfiguration.Initialize();
    var configService = new ConfigService();         // ← can throw
    Logger.Init(configService.IsDebugLoggingEnabled());
    Logger.Log($"App started — Build {configService.GetBuildNumber()}");
    Application.Run(new MainForm(configService));    // ← can throw
}
```

**`new ConfigService()` throw paths:**

| Site | Exception | Trigger |
|------|-----------|---------|
| `Directory.CreateDirectory(appDataPath)` L31 | `UnauthorizedAccessException` | `%AppData%` not writable |
| `Directory.CreateDirectory(appDataPath)` L31 | `IOException` | Disk error |

**Outcome:** If `ConfigService` throws, the app crashes before any window opens. The user sees a bare CLR crash dialog with a stack trace — no friendly error message.

**`new MainForm(configService)` is not individually risky**, but if the `MainForm` constructor itself were to throw (e.g., due to a future PlotManager setup failure), there is no catch here either.

---

## Finding 5 — `ConfigService.Save()` propagates from synchronous event handlers ⚠️ HIGH

**File:** `ConfigService.cs` ~line 79
**Callers:** `OnChannelVisibilityChanged`, `OnRpmModeChanged`, shift handlers

```csharp
private void Save()
{
    var json = JsonConvert.SerializeObject(_config, Formatting.Indented);
    File.WriteAllText(_configFilePath, json);   // ← not caught
}
```

**Throw paths:**

| Site | Exception | Trigger |
|------|-----------|---------|
| `File.WriteAllText` | `UnauthorizedAccessException` | Config file made read-only |
| `File.WriteAllText` | `IOException` | Disk full / locked by another process |
| `JsonConvert.SerializeObject` | Various `JsonException` | Should never happen with a plain POCO, but possible if a future field is non-serializable |

**Call chain:**

```
User clicks channel toggle
  → OnChannelVisibilityChanged (MainForm, no try/catch)
    → ConfigService.SetChannelVisibility()
      → Save()
        → File.WriteAllText  ← throws
```

For synchronous callers (not `async void`), an exception here propagates back to the WinForms event dispatch loop. WinForms catches it and (depending on `SetUnhandledExceptionMode`) either shows a crash dialog or silently swallows it, leaving the app in an undefined state.

---

## Finding 6 — `PlotManager` switch expression has no default arm ⚠️ HIGH

**File:** `PlotManager.cs` ~line 339
**Related:** Audit C3

```csharp
var yAxis = channelName switch
{
    "Voltage"       => (IYAxis)voltageAxis,
    "Ripple"        => voltageAxis,
    "Current"       => currentAxis,
    "PowerOut"      => powerAxis,
    "MotorTemp"     => motorTempAxis,
    "ESC Temp"      => escTempAxis,
    "RPM"           => rpmAxis,
    "Throttle %"    => throttleAxis,
    "Acceleration"  => raceBoxGAxis,
    "RaceBox Speed" => raceBoxSpeedAxis,
    "MotorTiming"   => motorTimingAxis,
    // ← no _ => default arm
};
```

**Exception:** `SwitchExpressionException` — thrown at runtime, not compile time.

**Trigger:** Any channel name that does not match a listed case. Current channels that could cause this:
- `"Throttle"` (legacy ms-based channel — still present in some Castle log versions, ColorMap has it commented out)
- Any future channel added to `ChannelColorMap` but not to this switch
- A misspelling or case change in channel name (e.g., `"ESC temp"` vs `"ESC Temp"`)

**Call chain (all unprotected):**

```
OnChannelVisibilityChanged → PlotAllRuns() → PlotManager.PlotRuns()
DeleteRun()                → PlotManager.PlotRuns()
OnRpmModeChanged()         → PlotManager.PlotRuns()
ShiftLeft/Right/Reset      → PlotAllRuns() → PlotManager.PlotRuns()
```

None of these callers have try/catch. `SwitchExpressionException` is a derived `InvalidOperationException` — it propagates to the WinForms event loop.

---

## Finding 7 — `RaceBoxLoader.LoadTelemetry` — `DateTime.Parse` without CultureInfo ⚠️ HIGH

**File:** `RaceBoxLoader.cs` lines 119, 125

```csharp
var startTime = DateTime.Parse(telemetryRows[0][timeIndex]);   // L119
// ...
foreach (var row in telemetryRows)
{
    var time = DateTime.Parse(row[timeIndex]);                 // L125
}
```

**Exception:** `FormatException`

**Trigger:** Any user whose Windows date/time locale uses a format other than what the RaceBox CSV writes (which is typically `HH:mm:ss.fff`). On some locales, `DateTime.Parse` uses locale-specific separators and fails on the RaceBox format.

**Propagates to:** `LoadRaceBox1/2/3Button_Click` — all of which are unprotected (Findings 1–3).

**Fix:** `DateTime.Parse(value, CultureInfo.InvariantCulture)` or `DateTime.ParseExact(value, "HH:mm:ss.fff", CultureInfo.InvariantCulture)`.

---

## Finding 8 — `RaceBoxLoader.LoadTelemetry` — index access on potentially empty list ⚠️ MEDIUM

**File:** `RaceBoxLoader.cs` line 152

```csharp
var launchIndex = points.FindIndex(p => p.Throttle > 0.165 && p.PowerOut > 10);
// launchIndex is -1 if not found
// ...
var launchTime = points[launchIndex].Time.TotalSeconds;   // ← throws if launchIndex == -1
```

Actually `launchIndex` being `-1` would produce an `ArgumentOutOfRangeException` on `points[-1]`. Additionally if `points` is empty, `points[0]` (used elsewhere) would also throw.

**Trigger:** A RaceBox file where no row meets the launch detection criteria (e.g., an idle log, or a log from a non-drag session).

**Propagates to:** All three RaceBox load handlers — unprotected.

---

## Finding 9 — `ConfigService.Load()` catch is too broad; inner `Save()` call is uncaught ⚠️ MEDIUM

**File:** `ConfigService.cs` lines 40–65

```csharp
try
{
    var json = File.ReadAllText(_configFilePath);
    _config = JsonConvert.DeserializeObject<Config>(json) ?? new Config();
}
catch   // ← catches everything, including IOException, JsonException
{
    _config = new Config();
    Save();   // ← Save() can throw; not caught
}
```

If `File.ReadAllText` throws (corrupted config), the catch resets to defaults and calls `Save()`. If `Save()` also throws (disk full during the same event), the exception propagates out of the `ConfigService` constructor — back to `Program.Main` which has no catch (Finding 4).

**Trigger:** Corrupted config file on a full disk. Unlikely but possible.

---

## Finding 10 — `Logger.Log` — uncaught I/O on every log call ⚠️ LOW

**File:** `Logger.cs`

```csharp
public static void Log(string message)
{
    if (!_enabled) return;
    File.AppendAllText(_logPath, $"[{DateTime.Now:HH:mm:ss}] {message}\n");  // ← not caught
}
```

**Exception:** `IOException` / `UnauthorizedAccessException`

**Trigger:** Log file deleted, made read-only, or disk full while debug logging is enabled.

**Impact:** Any `Logger.Log` call site in `MainForm`, `PlotManager`, etc. would throw. These are in synchronous event handlers — the exception propagates to the WinForms loop. Low probability because debug logging is off by default.

---

## Finding 11 — `CsvLoader` exceptions propagate, but callers ARE protected ✅ (noted for completeness)

**File:** `CsvLoader.cs` — called from `LoadRun1/2/3Button_Click`

The Castle load handlers in `MainForm.cs` DO have try/catch blocks wrapping the `await Task.Run(() => loader.Load(...))` calls. This is the correct pattern. CsvLoader exceptions are handled.

The same protection was **not** applied to the equivalent RaceBox handlers (Findings 1–3) — the inconsistency is the bug.

---

## Summary Table — Ranked by Crash Likelihood

| # | Location | Exception | Likelihood | Impact |
|---|----------|-----------|-----------|--------|
| 1 | `LoadRaceBox1Button_Click` — entire handler | Multiple (see Finding 1) | CRITICAL | App crash on load |
| 2 | `LoadRaceBox3Button_Click` — entire handler | Multiple | CRITICAL | App crash on load |
| 3 | `LoadRaceBox2Button_Click` — LoadHeaderOnly/LoadTelemetry | Multiple | CRITICAL | App crash on load |
| 4 | `Program.Main` → `new ConfigService()` | `UnauthorizedAccessException`, `IOException` | CRITICAL | Crash before window opens |
| 5 | `ConfigService.Save()` → `File.WriteAllText` | `IOException`, `UnauthorizedAccessException` | HIGH | Crash on channel toggle, RPM mode change |
| 6 | `PlotManager` switch — no default arm | `SwitchExpressionException` | HIGH | Crash on any unknown channel |
| 7 | `RaceBoxLoader.LoadTelemetry` — `DateTime.Parse` no CultureInfo | `FormatException` | HIGH | Crash for non-EN locale users |
| 8 | `RaceBoxLoader.LoadTelemetry` — `points[launchIndex]` | `ArgumentOutOfRangeException` | MEDIUM | Crash on non-drag RaceBox file |
| 9 | `ConfigService.Load()` catch — inner `Save()` uncaught | `IOException` | MEDIUM | Crash on startup with corrupted+full disk |
| 10 | `Logger.Log` — `File.AppendAllText` | `IOException` | LOW | Crash only when debug logging is enabled |

---

## Quickest Fixes

### Fix 1 — Wrap RaceBox handlers (30 min, eliminates Findings 1–3)

Pattern already used by Castle handlers; apply to all three RaceBox handlers:

```csharp
try
{
    var header = loader.LoadHeaderOnly(path);
    // ...
    var points = await Task.Run(() => loader.LoadTelemetry(path, ...));
    // ... rest of handler
}
catch (Exception ex)
{
    Logger.Log($"RaceBox load failed: {ex.Message}");
    MessageBox.Show($"Failed to load RaceBox file:\n{ex.Message}", "Load Error",
        MessageBoxButtons.OK, MessageBoxIcon.Error);
    // reset button state
}
```

### Fix 2 — Add default arm to PlotManager switch (2 min, eliminates Finding 6)

```csharp
_ => throttleAxis,   // fallback — prevents SwitchExpressionException
```

### Fix 3 — Add CultureInfo to DateTime.Parse in RaceBoxLoader (5 min, eliminates Finding 7)

```csharp
DateTime.Parse(value, CultureInfo.InvariantCulture)
```

### Fix 4 — Wrap Program.Main (10 min, eliminates Finding 4)

```csharp
try
{
    var configService = new ConfigService();
    // ...
    Application.Run(new MainForm(configService));
}
catch (Exception ex)
{
    MessageBox.Show($"Fatal startup error:\n{ex.Message}", "DragOverlay",
        MessageBoxButtons.OK, MessageBoxIcon.Error);
}
```

### Fix 5 — Wrap ConfigService.Save() (10 min, eliminates Findings 5 & 9)

```csharp
private void Save()
{
    try
    {
        var json = JsonConvert.SerializeObject(_config, Formatting.Indented);
        File.WriteAllText(_configFilePath, json);
    }
    catch (Exception ex)
    {
        Logger.Log($"Config save failed: {ex.Message}");
        // non-fatal — config will be re-attempted next save
    }
}
```

---

## Observation — Inconsistent Error Handling Across Load Handlers

The Castle load handlers (`LoadRun1/2/3Button_Click`) correctly wrap all async operations in try/catch. The RaceBox load handlers (`LoadRaceBox1/2/3Button_Click`) do not. This inconsistency suggests the RaceBox handlers were written later and the error handling pattern was not carried forward. **The fix for Findings 1–3 is a direct copy of the Castle handler pattern.**

---

*Error handling audit by Claude Sonnet 4.6 — 2026-03-05*
