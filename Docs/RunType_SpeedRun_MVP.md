RunType_SpeedRun_MVP.md
Overview

Add a global Run Type toggle with two modes:

Drag (default): keep today’s behavior (auto-trim around launch; 3 slots; RaceBox enabled).

Speed Run: show 2 slots only (Run 1 & Run 2), hide all RaceBox UI, and do not trim Castle logs.

While any log is loaded, the Run Type toggle is locked (disabled).

Purpose: Quickly compare Primary vs Slave ESC logs side-by-side, nothing more.

Goals

Add Run Type control in the main UI: Drag | Speed Run.

In Speed Run:

Hide Slot 3 (Run 3 + RaceBox 3 group).

Hide all RaceBox UI (for Slots 1 & 2 as well).

Load Castle logs with no trim (full log).

Lock the Run Type control while any log is loaded.

No changes to channel toggles, colors, hover, axes, or ScottPlot configuration.

UI Surface (exact to current Designer)

Container: topButtonPanel (first row above the plot).

New controls (names suggested):

lblRunType — label “Run Type:”

rdoDrag — radio “Drag”

rdoSpeedRun — radio “Speed Run”

Behavior:

Default = Drag (checked).

Both radios disabled when any of the following are non-null: run1, run2, run3, run4, run5, run6.

Tooltip when disabled: “Clear all logs to change Run Type.”

(Scope-only: control placement and exact styling follow current top bar conventions.)

Visibility Rules (map to real controls)
Drag (today’s behavior)

No changes. All current rows/buttons remain visible.

Speed Run (MVP)

Hide Slot 3 (Castle + RB):

Castle 3 (slot 3): btnLoadRun3, btnToggleRun3, btnDeleteRun3, btnShiftLeftRun3, btnShiftRightRun3, btnShiftResetRun3, btnMenuRun3.

RaceBox 3 (slot 6): btnLoadRaceBox3, btnToggleRaceBox3, btnDeleteRaceBox3, btnShiftLeftRB3, btnShiftRightRB3, btnShiftResetRB3, btnMenuRB3.

Prefer hiding the entire Run 3 panel (the TableLayoutPanel created as panelRun3 in the Designer). If that panel isn’t a field, falling back to hiding each listed control is acceptable for MVP.

Hide all RaceBox UI for Slots 1 & 2:

Slot 1 RB (slot 4): btnLoadRaceBox1, btnToggleRaceBox1, btnDeleteRaceBox1, btnShiftLeftRB1, btnShiftRightRB1, btnShiftResetRB1, btnMenuRB1.

Slot 2 RB (slot 5): btnLoadRaceBox2, btnToggleRaceBox2, btnDeleteRaceBox2, btnShiftLeftRB2, btnShiftRightRB2, btnShiftResetRB2, btnMenuRB2.

Keep Castle rows for Slots 1 & 2 visible and functional:

Slot 1: btnLoadRun1, btnToggleRun1, btnDeleteRun1, btnShiftLeftRun1, btnShiftRightRun1, btnShiftResetRun1, btnMenuRun1.

Slot 2: btnLoadRun2, btnToggleRun2, btnDeleteRun2, btnShiftLeftRun2, btnShiftRightRun2, btnShiftResetRun2, btnMenuRun2.

Scope note: Hiding = UI visibility only; it does not implicitly clear any loaded data. Clearing is still done via existing Delete actions.

Loading & Trimming (behavioral contract)

Drag mode: Castle CSV loads retain current auto-trim behavior (detect launch; crop −0.5s→+2.5s; re-zero).

Speed Run mode: Castle CSV loads perform no trimming (full log shown).
Optional (allowed): re-zero time to the file’s first sample for readability.

Where this applies: the CSV import path used by LoadRun1Button_Click, LoadRun2Button_Click, LoadRun3Button_Click. (Implementation later; scope here defines the expected behavior.)

RaceBox: In Speed Run mode, RaceBox UI is hidden and not used.

Locking Rules

Run Type radios (rdoDrag, rdoSpeedRun) are disabled if any of run1..run6 is non-null.

Radios re-enable only when all runs are cleared (run1..run6 == null).

If the user attempts to switch with loaded runs present, the UI stays in the current mode; tooltip clarifies the rule.

Acceptance Criteria

Startup (Drag default):

3 Castle rows (Run 1–3) and 3 RaceBox rows (RB 1–3) visible; all current buttons and menus present.

Switch to Speed Run with no logs loaded:

Slot 3 (Run 3 + RB 3) disappears.

All RaceBox rows for Slots 1 & 2 disappear.

Only Castle rows for Run 1 and Run 2 remain.

Load two Castle logs (Run 1 & Run 2) in Speed Run:

Logs load successfully.

No drag trim is applied; full time range is visible.

Hide/Show, Delete, Shift, and menu actions behave as today.

Run Type cannot change while any log is loaded (radios disabled + tooltip).

Clear all logs → radios re-enable; switching back to Drag restores the full 3-slot layout and all RaceBox rows.

No regressions in plotting, hover readouts, axis behavior, channel toggles, or shift controls.

Edge Cases

User toggles to Speed Run while Slot 3 is loaded → prevented by lock rule (radios disabled).

Hiding RaceBox rows in Speed Run does not affect Castle plotting or channel toggles.

PlotAllRuns() continues to operate on whichever run1..run6 are non-null; UI visibility does not forcibly modify run dictionaries.

Config migration: if no value is present, default to Drag.

Config

Persist last choice:

{
  "RunType": "Drag" // or "SpeedRun"
}


Applied on startup:

Drag → current full layout.

SpeedRun → Slot 3 hidden; all RaceBox rows hidden.

Out of Scope (explicit)

No pairing/combining of logs, no derived totals, no fine alignment changes.

No changes to RaceBox parsing, only UI visibility changes in Speed Run.

No changes to ScottPlot styling, axes, colors, or hover/cursor logic.

Test Checklist

 Fresh start → Drag mode UI (3 Castle + 3 RaceBox).

 With no logs, select Speed Run → only Run 1 & Run 2 (Castle) visible.

 Load Run 1 CSV → full log (no crop); controls active for Run 1.

 Load Run 2 CSV → full log (no crop); controls active for Run 2.

 Radios disabled while any run is loaded; tooltip present.

 Delete Run 1 and Run 2 → radios re-enable; switch back to Drag → full UI returns.

 No stray controls remain hidden/visible incorrectly after mode changes.

✅ Summary

This MVP adds a single, global Run Type toggle.
Speed Run mode: 2 Castle slots, no RaceBox UI, no trim on load, and no mid-session mode switching.
Everything else stays exactly as-is.

File: /docs/RunType_SpeedRun_MVP.md