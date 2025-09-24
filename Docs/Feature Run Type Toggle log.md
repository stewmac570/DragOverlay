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