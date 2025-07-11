Castle Log Overlay Tool — DELIVERY\_PLAN.md



✅ Goal
Deliver the Castle Log Overlay Tool one tiny, testable feature at a time, each in its own ChatGPT session, Git feature branch, and commit.
No syntax guessing. Full files only. Always match Castle Link 2 behavior.





📅 Project Phases — High Level
1️⃣ Phase 1 — POC: Load 1 Castle .csv log → plot 1 channel → show hover cursor.
2️⃣ Phase 2 — Multi-Log Overlay: Add up to 3 logs → overlay runs → line style rules.
3️⃣ Phase 3 — Channel Toggles: Sidebar checkboxes → toggle channels → config defaults.
4️⃣ Phase 4 — Run Alignment: Auto-align runs to launch point (t=0).
5️⃣ Phase 5 — Axis Lock \& Zoom: X-axis zoom, Y-axis auto-scale, optional (0,0) pin.
6️⃣ Phase 6 — UI Polish: Save window size, legend, warnings for >3 runs.



✅ These phases stay small — you can break them even further as needed.

✅ Phase Rules


🔑	Rule
✅	Each phase lives in its own Git feature branch (feature/phase-1-poc).
✅	Each phase has its own ChatGPT session — no crossing scopes.
✅	Each phase uses /tests/WorkingMWE/ as fallback checkpoint.
✅	Each phase must pass local tests before merging to main.
✅	Each phase must match FEATURES.md — no extras.

✅ Done Criteria Per Phase


Example — Phase 1 POC:

Loads 1 real Castle .csv log from /logs/.

Plots 1 channel (RPM) on ScottPlot.

Hover cursor line works.

Uses pinned ScottPlot v5.



Saved working version in /tests/WorkingMWE/.

📌 Git Rules
One Issue per phase.

One feature branch per phase.

Use clear commits:

sql
Copy
Edit
git commit -m "feat: Add single CSV loader and basic plot (#1)"
Tag stable merge:

Copy
Edit
v0.1-phase-1-poc




✅ Known Good Reference
Always compare hover, colors, cursor, and alignment logic to Castle Link 2.
If behavior is different, document why.





✅ No Guess Zone
No partial snippets — deliver full files only.

Always check ScottPlot \& CsvHelper docs for version-specific syntax.

If unsure, pause \& verify before writing code.





✅ Tech Stack Reminder
.NET 6+ (WinForms)

• CsvHelper v33.1.0 pinned

• ScottPlot v5.0.8 pinned

Newtonsoft.Json

Local config.json for defaults





✅ Version
DELIVERY\_PLAN.md v1.0 — \[YYYY-MM-DD]
Prepared by: \[Your Name]

