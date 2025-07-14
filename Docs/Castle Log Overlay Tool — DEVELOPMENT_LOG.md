Castle Log Overlay Tool — DEVELOPMENT\_LOG.md



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
This Development\_Log.md stays in /docs/ forever.

Update if you slip — so you don’t repeat the same mess.

Every phase must point back to this for “what not to do”.

✅ Version
Development\_Log.md v1.0 — \[YYYY-MM-DD]
Prepared by: \[Your Name]
--------------------------------------------------------------------------
📄 1. Detailed DEVELOPMENT\_LOG.md (Phase 1)

Content:



Project: Castle Log Overlay Tool — V2



Phase: Phase 1 — Single Run, Multi-Channel, Normalized Overlay



Dates: (use today’s date and your real start date if you want)



Steps:



Initial CSV parsing improvements (metadata skip, launch point, debug logs)



WinForms app with ScottPlot FormsPlot control



Added multiple normalized channels: Speed, Throttle, Voltage, Current, Ripple, PowerOut, MotorTemp, MotorTiming, Acceleration, GovGain



Disabled Y-axis labels and ticks for clean overlay



Added ScaleBar for simple X-axis time reference



Implemented full-screen window mode



Implemented vertical line cursor that follows mouse X position



Verified raw debug logs vs. plotted data



Final working version confirmed with screenshots



Status: Phase 1 — Complete



Lessons learned:



Normalizing multiple channels to 0–1 scale for overlay



How to disable ScottPlot axes \& grids properly



How to wire up hover with ScottPlot Crosshair or VerticalLine + mouse events



Importance of defensive logging
------------------------------------------------------------------------------------





Phase 2 — Multi-Log Overlay

✅ Scope Implemented

Goal:



Support loading up to 3 Castle logs simultaneously.



Overlay all runs on the same ScottPlot chart.



Use CsvHelper with your verified launch-point \& skip logic (no changes).



Plot multiple runs with:



Castle-matched colors (via ColorMap).



Clear line styles (solid, dashed, dotted — via LineStyleHelper).



Keep the simple Castle Link style vertical hover cursor working with 1–3 runs.



Retain all single-run features — no lost behavior.



🗂️ Key Changes by File

📌 /src/Services/CsvLoader.cs

✅ No logic changed to maintain tested Castle skip lines, header, flags, and launch-point detection.



✅ Still uses Load(string) for one file.



✅ MainForm calls Load() per slot — no batch method added (matches Castle Link 2 flow: load one file per slot).



📌 /src/MainForm.cs and /src/MainForm.Designer.cs

✅ Added 3 slots:



run1, run2, run3 → store each RunData.



3 buttons wired: Load Run 1, Load Run 2, Load Run 3.



✅ Each button uses OpenFileDialog to select a log → calls CsvLoader.Load() → assigns to its slot.



✅ After any slot is loaded, PlotManager.PlotRuns() is called with all non-null runs — overlays the runs together.



📌 /src/Utils/ColorMap.cs and /src/Utils/LineStyleHelper.cs

✅ Created helpers for Castle-matched colors (blue, red, green) and clear line patterns (solid, dashed, dotted).



✅ Matches Castle Link 2 visual behavior.



📌 /src/Plot/PlotManager.cs

✅ Added PlotRuns(List<RunData>):



Clears the plot.



Adds each run’s channels (Speed, Throttle, Voltage, etc.), normalized to \[0,1] for same scale.



Each run uses consistent Castle color + line style.



Legend shows file.csv — channel per line.



✅ Vertical hover cursor:



Re-created after each clear to ensure it works for multiple runs.



MouseMove wired once in the constructor → moves \_cursor.X → refreshes.



No IScatter or GetClosest() — keeps stable Scatter type only.



✅ Ensures multiple log loads do not break cursor line behavior.



🔑 What stayed stable

✔️ Original LoadCsvButton\_Click fallback remains — Phase 1 single-load still works.



✔️ CsvLoader skip and launch logic untouched — verified with real logs.



✔️ Castle-matched colors and line styles follow the pinned Castle Link 2 style.



✔️ No broken syntax: no deprecated v4, no unused IScatter.



🟢 What was explicitly deferred

Snap-to-point hover logic using GetClosest() → postponed to Phase 3 because of version conflict \& scope creep.



Channel toggles (on/off) → planned for Phase 3.



Scaling tweaks beyond \[0,1] normalization → future phase.



✅ Stable state

/tests/WorkingMWE/ has known-good:



Working multi-log overlay.



1–3 runs display properly.



Hover line always visible.



No compile errors — tested and tagged as v0.2-phase-2-multilog.



📌 Proposed Development Log Entry

markdown

Copy

Edit

\### 2025-07-12 — Phase 2: Multi-Log Overlay Complete



\- Added multi-log overlay support for up to 3 Castle logs.

\- Expanded `MainForm` to support 3 independent run slots with separate Load buttons.

\- Updated `PlotManager` with `PlotRuns(List<RunData>)` to handle multiple runs.

\- Added `ColorMap` and `LineStyleHelper` utilities for Castle-matched colors and line styles.

\- Restored vertical hover cursor to work across multiple runs by re-adding it after `Plot.Clear()`.

\- Verified ScottPlot v5 syntax — no deprecated API.

\- Preserved single-run fallback; no changes to core `CsvLoader` logic.

\- Known stable version tagged: `v0.2-phase-2-multilog`.



Next: plan Phase 3 for channel toggles and improved scaling.





-----------------------------------------------------------------------------------------------------


 Full Phase 3 — Channel Toggle Bar Implementation & Debug Chat
✅ 1️⃣ Phase Goal Confirmed
You confirmed this chat is only for Phase 3:

Add a horizontal bottom toggle bar like Castle Link Data Logger.

Each channel shows: channel name, log 1–3 hover values, and a Show/Hide toggle.

User can toggle channels ON/OFF — plot updates live.

Hover cursor must keep working for visible channels.

Save ON/OFF defaults in config.json so they persist.

✅ 2️⃣ Initial ChannelToggleBar structure done
You created ChannelToggleBar as a UserControl.

You embedded ChannelRow (sometimes called ChannelToggleRow) as a nested class inside ChannelToggleBar.cs — no separate file needed.

Each ChannelRow:

Shows channel name.

Shows three Labels for log hover values.

Has a CheckBox for Show/Hide toggle.

ChannelToggleBar holds all rows in a horizontal FlowLayoutPanel.

✅ 3️⃣ Connected toggle events
Each ChannelRow fires a ToggleChanged event.

ChannelToggleBar wires ToggleChanged to ChannelVisibilityChanged.

In MainForm, you handle ChannelVisibilityChanged:

Calls _plotManager.SetChannelVisibility(channelName, isVisible).

Calls _plotManager.RefreshPlot().

Saves updated states to config.

✅ 4️⃣ Added hover data feed
You connected _plotManager.CursorMoved to MainForm:

On cursor move, calls OnCursorMoved(valuesAtCursor).

Passes data to ChannelToggleBar.UpdateMousePositionValues().

Each ChannelRow displays log 1–3 current Y-values for that channel.

✅ 5️⃣ Debugged ScottPlot hover API
You hit multiple CS1061 errors because:

Tried using IScatter — not valid in ScottPlot 5.0.55.

Tried using .Xs and .Ys on IScatterSource — does not exist.

You used the real ScottPlot source to confirm:

Scatter stores its data as IScatterSource in .Data.

Correct way is to call GetScatterPoints() → returns Coordinates.

✅ 6️⃣ Final working hover
You rewrote FormsPlot_MouseMove:

Loops _scatters (List<Scatter>).

For each scatter: scatter.Data.GetScatterPoints().

Finds nearest X.

Sets Y-value per run.

This removed all .Xs/.Ys errors and cast failures.

Hover data now feeds the toggle bar live.

✅ 7️⃣ Verified config saving
Toggle ON/OFF states persist to config.json using ConfigService.SaveChannelVisibility().

MainForm loads these states at startup so toggles match previous session.

✅ 8️⃣ You flipped layout to vertical style
Changed each ChannelRow inner FlowLayoutPanel to FlowDirection.TopDown.

Each block now displays:

csharp
Copy
Edit
[ChannelName]
[Log 1]
[Log 2]
[Log 3]
[Show]
✅ 9️⃣ Closed up ChannelToggleRow.cs confusion
You asked whether you needed a separate ChannelToggleRow.cs — answer: NO, your inline ChannelRow is good.

Deleted the unused/broken file that caused CS1001 errors.

✅ 10️⃣ Final Phase 3 result
The toggle bar works: channels ON/OFF, hover values for each log, saves defaults.

UI spacing between channel blocks is too wide — so you agreed to defer polish to final UI pass.

Phase 3 functional deliverable: DONE.

✅✅ 📄 Final Phase 3 Deliverable:
pgsql
Copy
Edit
- Castle Link style channel toggle bar added.
- Supports multiple logs with per-channel hover data.
- Each toggle works: hides/shows channel live.
- Hover cursor works for visible channels only.
- ON/OFF toggle states persist to config.json.
- ScottPlot hover block uses real Scatter.Data.GetScatterPoints() — no cast failures.
- Layout stacks log values vertically per channel.
- UI margin/padding still needs refinement, deferred to final styling phase.
⚡ Key lessons recorded
Always confirm ScottPlot version — no fake .Xs/.Ys on IScatterSource in 5.x.

Scatter.Data is the single source of truth for hover data.

Simple FlowDirection.TopDown achieves vertical per-channel blocks.

Keep ChannelRow nested if it’s local only.

✅ This phase’s status in DEVELOPMENT_LOG.md:
scss
Copy
Edit
PHASE 3 — COMPLETE (functional)
UI styling — Deferred


------------------------------------------------------------------


