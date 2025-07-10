Castle Log Overlay Tool — DEVELOPMENT_LOG.md


✅ Project Summary
Goal: Build a WinForms .exe that loads multiple Castle ESC .csv logs, aligns them, overlays up to 3 runs on a ScottPlot chart, with Castle-matched colors, hover cursor, axis lock, channel toggles, and config.json for defaults.

Reference: Castle Link 2 behavior for plotting, cursor, and alignment.

✅ What Actually Happened (July 2025)
Basic folder structure set up using STRUCTURE.md.

CsvHelper + ScottPlot installed.

CSV import works for single logs.

Started multi-log overlay but unstable.

Axis lock partly attempted — caused repeated issues.

No stable config-based channel toggles yet.

Repeated syntax errors: ScottPlot API mismatches, version drift.

Too many partial snippets, unverified guesses, and context breaks.

✅ By The Numbers
🔑 Metric	Result
⏳ Active Dev Time	~15–20 hours
🧑‍💻 Stable LOC	~100–150
❌ Guess-based Errors	~20–30
📅 Timeline	7–11 July 2025

✅ Root Cause
ScottPlot syntax and version drift (v4 vs v5).

No pinned known-good Minimum Working Example (MWE).

No single source of truth for features vs. in-progress guesses.

Multiple partial code snippets broke stable working bits.

✅ Lessons Learned
1️⃣ Pin Official Docs

Always open ScottPlot + CsvHelper docs.

Paste version-specific code only.

2️⃣ Lock Version Early

ScottPlot v5.X.Y pinned to match Castle Link 2 behavior.

Never upgrade mid-phase without tests.

3️⃣ Work In Micro-Phases

1 feature = 1 chat = 1 Git branch.

Finish → test → commit → merge → tag → next.

4️⃣ Full Files Only

Never paste half-broken snippets.

Always deliver self-contained files.

5️⃣ Debug First, Polish Later

Get 1 CSV to plot before messing with overlays, toggles, styling.

6️⃣ Keep Real Test Logs

Use /logs/ with actual Castle files every time you test.

7️⃣ No Scope Creep

Stick to /docs/FEATURES.md exactly.

Ideas go to “Future Nice-to-Haves”.

8️⃣ Pause If Unsure

Never guess syntax.

If unsure → check Castle Link 2 behavior or real docs → then code.

✅ One-Line Takeaway
“Always pin real API docs, work in tiny phases, deliver full files, never guess syntax.”

✅ Next
This Development_Log.md stays in /docs/ forever.

Update if you slip — so you don’t repeat the same mess.

Every phase must point back to this for “what not to do”.

✅ Version
Development_Log.md v1.0 — [YYYY-MM-DD]
Prepared by: [Your Name]