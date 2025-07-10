 Castle Log Overlay Tool — FEATURES.md
✅ Project Goal
Build a simple, stable Windows .exe that loads multiple Castle ESC .csv log files, aligns them, and overlays up to 3 runs on a ScottPlot chart for fast RC drag car tuning comparison — with proper Castle-style colors, line styles, hover cursor, axis locking, and no scope drift.

Reference: Castle Link 2 behavior is the standard for hover, alignment, colors, and cursor.

📊 Core Features (MUST-HAVE)
1️⃣ Multi-Log Loader

User can select 1–3 Castle .csv log files at once.

Each log fully parsed with CsvHelper.

Supports real Castle logs with correct time & channels.

2️⃣ Run Overlay

Runs align automatically to a common launch point (t=0).

Each run uses Castle-style channel colors but with solid, dashed, dotted line styles.

3️⃣ Hover Cursor & Tooltip

Vertical dashed cursor line tracks mouse on X-axis.

Hover shows time & each visible run’s values.

Snaps to nearest real sample point.

4️⃣ Channel Toggle Panel

Sidebar with checkboxes for each channel.

Toggles on/off dynamically.

Uses local config.json for default ON/OFF states.

5️⃣ Axis Lock & Zoom

X-axis zooms in/out, Y-axis stays auto-scaled to visible data.

Option to pin axes to (0,0) where needed — matches Castle Link 2.

6️⃣ Launch Point Alignment

Runs align so t=0 = launch, based on Power-Out or Current threshold.

Threshold value stored in config.json.

7️⃣ Clean UI

WinForms or WPF.

Simple, resizable chart area.

Sidebar for toggles & legend.

Loads maximized by default.

🚫 Out of Scope (V1)
No log editing or trimming inside the app.

No PDF/image export (future idea only).

No tune sheets or notes.

No cloud sync or online storage.

No mobile version.

🌟 Future Nice-to-Haves
Save/load “overlay sessions” as .json for quick compare.

Snapshot export to PNG or PDF.

Basic trend analysis across sessions.

🧩 Core Tech Stack
.NET 6+ (WinForms or WPF)

ScottPlot v5.X.Y pinned — match Castle Link 2 if possible

CsvHelper

Local config.json for user defaults

/tests/WorkingMWE/ rollback for stable single-run version

⚡ Ground Rules
Always match Castle Link 2 behavior where possible.

No syntax guessing. Use official ScottPlot & CsvHelper docs.

1 feature = 1 Git branch = 1 ChatGPT session.

Always deliver full files — never partial broken snippets.

Commit only stable, testable chunks.

📁 Where This Lives
/docs/FEATURES.md — pinned as the single source of truth for all phases.

✅ Version: FEATURES.md v1.0 — [YYYY-MM-DD]
Prepared by: [Your Name]

🏁 Done means:
✔️ 3 logs overlaid.
✔️ Castle colors, lines, cursor, hover.
✔️ Channels toggle cleanly.
✔️ Config.json defaults respected.
✔️ No random “maybe this works” code.