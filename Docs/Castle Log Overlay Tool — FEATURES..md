✅ Project Goal

Build a simple, stable Windows .exe that loads multiple Castle ESC .csv log files, aligns them, and overlays up to 3 runs on a ScottPlot chart for fast RC drag car tuning comparison — with proper Castle-style colors, line styles, hover cursor, axis locking, and no scope drift.



Reference: Castle Link 2 behavior is the standard for hover, alignment, colors, and cursor.



📊 Core Features (MUST-HAVE)



1️⃣ Multi-Log Loader

\- User can select 1–3 Castle .csv log files at once.

\- Each log fully parsed with CsvHelper.

\- Supports real Castle logs with correct time \& channels.



2️⃣ Run Overlay

\- Runs align automatically to a common launch point (t=0).

\- Each run uses Castle-style channel colors but with solid, dashed, dotted line styles.



3️⃣ Hover Cursor \& Tooltip

\- Vertical dashed cursor line tracks mouse on X-axis.

\- Hover shows time \& each visible run’s values.

\- Snaps to nearest real sample point.



4️⃣ Channel Toggle Panel

\- Horizontal Castle-style bottom bar with one block per channel.

\- Live hover display for all 3 logs.

\- “Show / Hide” toggle buttons.

\- Uses local config.json for default ON/OFF states.



5️⃣ Axis Lock \& Zoom

\- X-axis zooms in/out, Y-axis uses true physical units.

\- Each channel has a hidden, locked Y-axis.

\- Clean, uncluttered chart (no label noise).



6️⃣ Launch Point Alignment

\- Logs are trimmed and aligned so t=0 = launch.

\- Launch = throttle > 1.65ms and PowerOut > 10W.

\- Only -0.5s to +2.5s shown.

\- Time values are re-zeroed.



7️⃣ ESC Temp Channel

\- CSV column "Temperature" mapped to “ESC Temp”.

\- Plotted in real units (°C) with color and axis assigned.

\- Toggles ON/OFF, hover works.



8️⃣ RPM 2P / 4P Mode

\- Toggle button switches RPM display between 2 Pole (raw) and 4 Pole (halved).

\- Affects both plot scale and hover values.

\- Setting saved to config.json.



9️⃣ Safe Delete Handling

\- Deleting all logs resets the plot cleanly — no crashes.

\- Title and UI elements reset to default state.



🔟 RaceBox Overlay

Adds side-by-side overlay of RaceBox GPS timing data onto Castle ESC logs.



Aligns RaceBox runs to Castle launch point (t = 0) for direct performance comparison.



Each Castle log slot (Run 1–3) gets a matching “Load RaceBox” button.



RaceBox lines use unique colors but match the Castle log’s line style (solid/dash/dot).



Toggle bar is extended with new RaceBox channels (e.g., GPS speed, acceleration, split times).



RaceBox files use a separate parser (RaceBoxLoader.cs) but share common hover/plot infrastructure.



Uses existing config.json for visibility toggles.



Behavior: ✅ Original innovation — no Castle Link equivalent.



🔧 Tech Stack

\- .NET 6+ (WinForms)

\- ScottPlot v5.X.Y pinned — match Castle Link 2

\- CsvHelper

\- Newtonsoft.Json

\- Local config.json for user defaults

\- /tests/WorkingMWE/ rollback for stable single-run version



⚡ Ground Rules

\- Always match Castle Link 2 behavior where possible.

\- No syntax guessing. Use official ScottPlot \& CsvHelper docs.

\- 1 feature = 1 Git branch = 1 ChatGPT session.

\- Always deliver full files — never partial broken snippets.

\- Commit only stable, testable chunks.



📁 Where This Lives

/docs/FEATURES.md — pinned as the single source of truth for all phases.



✅ Version: FEATURES.md v1.1 — 2025-07-22

Prepared by: ChatGPT + Stewart McMillan



🏁 Done means:

✔️ 3 logs overlaid.

✔️ Castle colors, lines, cursor, hover.

✔️ Channels toggle cleanly.

✔️ Config.json defaults respected.

✔️ Trimmed logs start at true launch.

✔️ ESC Temp + RPM modes work.

✔️ No random “maybe this works” code.


✅ Version  

FEATURES.md v1.1 — 2025-07-22  

Prepared by: Stewart McMillan


