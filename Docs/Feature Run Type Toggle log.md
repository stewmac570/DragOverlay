✨ Feature: Run Type Toggle (Drag ↔ Speed Run)

UI Changes

Replaced old RadioButton group with a compact pill switch.

Moved toggle to the far right of the top menu bar for consistent placement.

Added slide knob animation: left = Drag, right = Speed.

Behavior

Active mode label now shows as black + bold; inactive mode is greyed out.

Labels ("Drag" and "Speed") always stay visible — knob sits behind text.

Toggle locks automatically when runs are loaded (to prevent mode change mid-session).

Tooltip hints clarify lock behavior:

“Clear all loaded logs to change mode.”

“Drag | Speed” when unlocked.

Logic

ApplyRunTypeUI() updated:

Drag mode → all Run + RaceBox slots visible.

Speed mode → hides Run 3 + all RaceBox slots (only Run 1–2 used).

Centralized in SyncRunTypeUI() to keep knob, colors, and lock state in sync.

Result

Cleaner, minimal UI with clear visual feedback.

Mode switching is obvious, compact, and resilient against layout issues on small screens.

---

Summary — Commit 2 (Run Type: Trim behavior)

CsvLoader

Signature changed to Load(string filePath, bool trimForDrag = true).

Drag mode (trimForDrag == true): preserves Castle Link-style auto-trim (detect launch, crop −0.5s→+2.5s, re-zero at launch).

Speed Run mode (trimForDrag == false): loads the full log (no trimming) and re-zeros to the first sample.

All existing logging, headers parsing, throttle % derivation, and safety checks retained.

MainForm

LoadRun1/2/3Button_Click now call loader.Load(filePath, trimForDrag: (_isSpeedRunMode == false)).

Drag mode → trimming ON.

Speed Run mode → trimming OFF.

No other UI/plot logic changed.

Result

Drag behaves exactly as before.

Speed Run shows full-length logs (expected heavier plots; next commit will address performance/axes).