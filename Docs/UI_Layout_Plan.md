# UI layout plan: Logs page and Tune page

Status: planning. This document records what is agreed so far and what is
still open. It is updated as decisions land.

## Why

The main screen has grown one panel at a time. The run strip sits on top, the
Tune panel takes a fixed 460 px on the right, the channel strip sits along the
bottom, and the alignment bar pops in above the chart. Each new feature (tunes,
projects, Mini Logger, trimming) was added to whichever edge had room. The chart
is boxed in on three sides and the next feature has no natural home.

## Reference tools

Screenshots are in `Docs/` (FuelTech FTManager, Holley, Ghost ESC/A.R.C.,
BBRC Stock Data Logger).

- **Copy: FuelTech FTManager.** One left sidebar lists every channel in
  collapsible groups. Each row is a tick box, the name, and a coloured box with
  the value at the cursor. That one list is the legend, the readout and the
  on/off switch. The chart fills the rest. An overview strip under the chart
  picks the zoom window. Tabs above the chart hold extra pages.
- **Take one idea: BBRC logger.** A row of peak-value cards (peak RPM, peak
  amps, run time) as a quick summary.
- **Avoid: Ghost/A.R.C.** Per-run settings grids and large buttons fill the
  side panel and crowd out the channels.

## Agreed

1. **Two pages, switched by tabs: Logs and Tune.** The Logs page shows only
   logs. Tune moves off the Logs page.
2. **Project open and save move to the top bar.** A project holds logs and
   tunes, so it does not belong inside the Tune panel.
3. **Tunes compare like logs.** The Tune page shows every loaded run's tune at
   once, not one at a time from a drop-down:
   - A settings table with one column per run (Run 1, 2, 3) and one row per
     setting: throttle curve points, radio settings, boost, turbo, max power,
     drag brake, sample rate, firmware.
   - The table includes the radio throttle speed settings for each run:
     radio mode, and for points 1, 2 and 3 the point position (points 1 and
     2), turn speed and return speed. These are typed in per run, not read
     from the .dat file, and are saved with the project.
   - Rows where the runs differ are highlighted, with an option to show only
     the rows that differ.
   - Throttle curve and boost-by-throttle curve are overlaid, one line per run,
     in the same run colours and line styles as the Logs page.
4. **No tune content on the Logs page.** Tune curves are not shown under the
   chart. Tunes are viewed on the Tune tab only.

## Proposed, not yet agreed

- **Logs page layout (FuelTech model).** Thin run strip on top. Left channel
  sidebar grouped by source (ESC, GPS, Mini Logger), each row showing the
  cursor value for each loaded run. This replaces the bottom channel drawer and
  stat cards. Chart fills the rest.

## Open questions

- Whether the peak-value summary row is wanted.
- Whether the chart should support stacked panes (one pane per channel group).

## Out of scope for this plan

Code changes. This plan is agreed first, then built in stages.
