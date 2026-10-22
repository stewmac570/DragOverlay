# Split view plan: up to three stacked charts

Status: built. Stage 1 in #151, stages 2 and 3 in #153. Hand checks: issue #154.

## Goal

Split the Logs page chart into two or three charts stacked top to bottom. They
share one time axis, so a moment in the top chart lines up with the same moment
below it. Each channel is placed in the chart you choose, for example Castle on
top, GPS in the middle and data logs at the bottom.

## Review of what exists today

**The chart is one class doing everything.** `Plot/PlotManager.cs` is 1,481
lines and drives a single ScottPlot chart (`FormsPlot`). It owns:

- the lines for all three file types (Castle, GPS, Mini Logger)
- one Y axis per channel (14 axes), the scale locks, and the ↕ line-size bands
- which channel shows its scale on the left (the "focused" channel)
- the cursor line, the cursor value box, and the values sent to the channel list
- dragging a run to line it up, and the right-click trim menu
- the time range and the time labels along the bottom

**The rest of the app reaches the chart through a small set of methods.** The
presenter uses about 25 `PlotManager` methods and events, for example
`PlotRuns`, `SetRunVisibility`, `SetChannelColor`, `CursorMoved` and
`AlignmentDragged`. If those stay the same, the presenter, channel list, tune
page and projects need very few changes.

**ScottPlot 5.0.55 (the pinned version) can link charts.** It has
`Plot.Axes.Link(otherChart, x: true, y: false)`, which keeps the time axis of
several charts in step when you zoom or pan any one of them. No library
upgrade is needed.

**Problems already known that this work touches:**

- The channel scale on the left of the chart doesn't show (it didn't before the
  new layout either). Each pane will show its own focused channel's scale, so
  this gets fixed along the way.
- GPS split lines (66 / 132 ft) are drawn on the one chart. With panes they
  should show in every pane so they still mark the same moment.

## Design

### How you use it

- **Panes switch** at the top of the channel list: `1 · 2 · 3`.
- **By source** button: one click puts Castle in pane 1, GPS in pane 2 and data
  logs in pane 3, and switches to three panes.
- **Pane button on each channel row**, next to B and ↕, showing the pane number.
  Click it to move the channel to the next pane; right-click to pick a pane.
  With one pane the button is hidden.
- **Dividers** between panes can be dragged to make a pane taller or shorter.
- Zoom, pan, the cursor line and lining up runs work across all panes at once.
- The cursor value box shows the picked channels from every pane.
- Panes, pane heights and which channel is in which pane are remembered, and
  saved in projects.

Drag and drop of channel rows onto a pane is left out. The pane button does the
same job with much less risk; it can be added later if the button feels slow.

### How it is built

`PlotManager` stays the one thing the rest of the app talks to. Inside it, the
chart work moves into a new `ChartPane` class, and `PlotManager` holds one to
three of them:

- **`ChartPane`**: one `FormsPlot`. Its lines, its Y axes and scale locks, its
  bands, and its focused-channel scale. It only draws the channels assigned to
  it.
- **`PlotManager`** (coordinator): keeps the channel → pane map, links the time
  axes, draws the one cursor line in every pane, builds the cursor values from
  all panes, and handles alignment dragging and the trim menu wherever you
  click.
- **Logs page**: a stack of panes with draggable dividers in place of the
  single chart.

## Build stages (one PR each)

1. **Split the class, no visible change.** Move the per-chart code into
   `ChartPane`; `PlotManager` drives a single pane. The app must look and behave
   exactly as now. Tests: channel → pane map, band maths still pass.
2. **Two and three panes.** Panes switch, pane button per row, By source
   preset, linked time axis, shared cursor and cursor box, dividers. Saved in
   config and projects.
3. **Polish.** Each pane shows its focused channel's scale, split lines in every
   pane, alignment and trim checked in every pane.

## Risks

- Stage 1 touches most of the chart code. It is the riskiest step, which is why
  it changes nothing you can see and gets checked before panes are added.
- Three ScottPlot charts redraw on every mouse move instead of one. Hover was
  made fast earlier (single pass); this needs checking on a full log with auto
  trim off.
- Projects saved before panes open as one pane.

## Stage 1 review notes (for stage 2)

Stage 1 (`Plot/ChartPane.cs`, `Plot/ChannelPaneMap.cs`, the slimmed `PlotManager`)
was written by Codex and reviewed line by line. It changes nothing today: same
public methods, identical hover values on real logs. Things stage 2 must fix:

1. **Time-axis link is one way.** `PlotManager.AddPane` links pane 1 to each new
   pane, but not back. Zooming in pane 2 or 3 would not move pane 1. Link every
   pair in both directions, and check the link survives a replot:
   `ChartPane.ClearForPlot` and `ResetEmptyPlot` call `Axes.Rules.Clear()`.
2. **The cursor box only lives in pane 1.** It is added to pane 1's chart, but
   `UpdateReadout` positions it with the mouse position of whichever pane the
   mouse is over. Over pane 2 or 3 it would appear in the wrong place. Move it to
   the hovered pane, or host it above all panes.
3. **Channels not in `ChannelText.Order` are never drawn.** Each pane draws a
   fixed set of names taken from that list. Any new channel added later would
   silently disappear. Panes should ask the channel map which pane a channel
   belongs to, with unknown channels going to pane 1.

Minor: most of the old explanatory comments in the chart code were dropped in
the move. Worth restoring the ones that explain why (axis locks, hover,
"PlotManager skips runs with empty DataPoints").

**Likely cause of the missing RPM scale (stage 3):** each chart is laid out with
fixed 40 px margins (`Layout.Fixed(new PixelPadding(left: 40, right: 40, ...))`).
The RPM axis is a right-hand axis, and 40 px is too narrow for its numbers, so
they are clipped. This is a guess from the code, not yet tested.

## Open question

- Pane button (planned) or drag and drop of channel rows onto panes?
