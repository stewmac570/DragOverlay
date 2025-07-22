Castle Log Overlay Tool — DELIVERY\_PLAN.md



✅ Goal
Deliver the Castle Log Overlay Tool one tiny, testable feature at a time, each in its own ChatGPT session, Git feature branch, and commit.
No syntax guessing. Full files only. Always match Castle Link 2 behavior.





1️⃣ Phase 1 — Single Log POC  

Load 1 Castle .csv log → plot 1 channel (RPM) → show hover cursor  



2️⃣ Phase 2 — Multi-Log Overlay  

Add up to 3 Castle logs → overlay runs → apply Castle-matched colors + line styles  



3️⃣ Phase 3 — Channel Toggle Bar  

Add bottom toggle panel → ON/OFF per channel → live plot update → hover values per run  



4️⃣ Phase 4 — Launch Point Alignment  

Auto-align logs to t=0 using throttle > 1.65ms and PowerOut > 10  



5️⃣ Phase 5 — Multi-Axis Overlay \& Plot UI  

\- Lock Y-axis per channel to real-world units (°C, V, ms, etc.)  

\- Hide all visual axis labels for clean overlay  

\- Sync hover cursor with true values across logs  



5.4 — Add ESC Temp channel  

Parse “Temperature” column → plot as “ESC Temp” with axis + toggle support  



5.5 — UI Cleanup \& Title Alignment  

Fix title padding + run button state handling → ensure consistent layout on load/reset  



5.6 — Auto-Trim Around Launch  

Detect throttle spike and trim window to -0.5s → +2.5s → re-zero time to 0.00s  



5.7 — ToggleBar UI Finalization  

Switch from checkboxes to Show/Hide buttons → bold font → multi-column Castle layout  



5.8 — Safe Delete Handling  

If all runs deleted → reset plot cleanly with no crash  



5.9 — RPM 2P / 4P Mode Toggle  

User can halve RPM values for 4P mode → affects plot + hover → saved to config  



6️⃣ Phase 6 — Visual Polish \& Stability  

Final toggle bar spacing, fixed icon, startup layout, default config generation  



7️⃣ Phase 7 — RaceBox Overlay  

Add “Load RaceBox” buttons → align RaceBox GPS data to Castle log → add toggle bar support for GPS channels → plot side-by-side



✅ These phases stay small — you can break them even further as needed.



✅ Phase Rules

🔑 Rule  

✅ Each phase lives in its own Git feature branch (e.g. feature/phase-3-channel-toggle)  

✅ Each phase has its own dedicated ChatGPT session — no scope mixing  

✅ Each phase is tested locally and visually before merging to main  

❌ /tests/WorkingMWE/ fallback not used — replaced by stable Git tags (e.g. v0.4-phase-4-config)  

✅ Each phase matches scope defined in FEATURES.md — no extras or guesswork  



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

DELIVERY\_PLAN.md v1.1 — 2025-07-22  

Prepared by: Stewart McMillan



