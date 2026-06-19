# DragOverlay — UI Redesign Specification

> Author: Stewart McMillan
> Target app: `DragOverlay` (.NET 8 WinForms, ScottPlot v5, CsvHelper)
> Purpose: full UI/UX design spec for the redesign. This is the reference document for the Claude Code build sessions. It defines *what* the UI is and *why*; per-phase CC prompts (TASK / FILES / SPECS / ACCEPTANCE) are written separately against the build sequence at the end.

---

## 1. What this app is

DragOverlay loads RC drag-pass telemetry — Castle ESC datalogs and RaceBox GPS logs — and overlays two runs (Run A vs Run B) on a single time axis so the user can compare tunes and find where time is being left on the strip. It is the analysis counterpart to the live tools; it is used at the bench and at the track, often on a small laptop.

The redesign keeps the Castle data model (independent per-channel scaling, per-channel min/max/avg/cursor readout, per-channel hide) and replaces the dated presentation: 1990s chrome, a wall of equally-loud axes, and an eye-searing primary palette.

---

## 2. Design principles (the rules everything follows)

1. **The plot is the hero.** The plot is the only element with a permanent, fixed place on screen. It fills 100% of the space not currently taken by an *expanded* panel. On a small laptop with everything collapsed, the plot occupies almost the whole window.
2. **Chrome is transient.** Every control surface either collapses to a thin handle or only appears while it is relevant (e.g. the alignment bar appears only while a log is armed). Nothing sits permanently beside the plot competing for width.
3. **One channel is loud, the rest are context.** Instead of Castle's twelve equally-bright axes (overwhelming) or the current build's hidden axes (unreadable), exactly one channel is *focused* at a time: it draws bright and owns the visible Y axis; every other visible channel dims to a low-opacity context trace.
4. **Controls do double duty.** The channel panel is simultaneously the legend, the stats readout, and the on/off switches. One surface, three jobs — that is how it earns its screen space.
5. **Run identity = line style; channel identity = colour.** Run A is always a solid line, Run B is always dashed, in the *same hue* per channel. Source (ESC vs GPS) is conveyed by grouping in the drawer, never by colour or line style.

---

## 3. Target and constraints

| Constraint | Value |
|---|---|
| Minimum window size | 1280 × 720 (must be fully usable; design for 13" laptops) |
| Default / typical | 1920 × 1080 |
| Framework | .NET 8 WinForms |
| Plot control | ScottPlot v5 `FormsPlot` |
| CSV parsing | CsvHelper |
| Theme | Dark default. Light theme is out of scope for v2 (same palette swapped later if wanted). |
| Code conventions | Follow the repo's existing style / `CLAUDE.md`. UI spec does not dictate code style. |

---

## 4. Layout architecture

### Regions (top to bottom)

| Region | Permanence | Collapses to | Notes |
|---|---|---|---|
| Title bar | permanent | — | App name, fullscreen-plot toggle. Thin. |
| Run strip | permanent (thin) | already thin | One chip per loaded log + master visibility toggle. Big "Load" buttons only show before a log occupies that slot. |
| Alignment bar | **transient** | hidden when no log armed | Appears only while a log is armed for alignment. |
| Plot | **permanent, fills remainder** | never | The hero. |
| Channel drawer | collapsible | thin toggle strip (coloured dots) | Legend + stats + on/off. |

### Two working states

**Race read (collapsed)** — for quick reads and small screens. Title bar + thin run strip + huge plot + a single-row strip of coloured channel dots (tap a dot to toggle that line). No stats, no alignment bar. This is the default on a fresh launch once logs are loaded.

**Analysis (expanded)** — drawer open showing full stat cards; alignment bar visible if a log is armed; plot shrinks to make room but is still the largest element.

### Resize / small-laptop behaviour

- The plot absorbs all size changes; fixed-height regions (bars, drawer) keep their height, the plot takes the rest.
- Below ~1366 px wide, the run strip chips truncate filenames with an ellipsis (full name on hover/tooltip) before they wrap.
- The channel drawer is a horizontally-wrapping grid of cards (`auto-fit`, min card width ~150 px) so it reflows instead of clipping on narrow windows.
- A **fullscreen-plot toggle** (title bar button + `F11`) hides every region except the plot for the cleanest possible read.

---

## 5. Design tokens

### Surfaces

| Token | Hex | Usage |
|---|---|---|
| `surface.window` | `#13171E` | App background |
| `surface.bar` | `#1B212B` | Title bar, run strip |
| `surface.panel` | `#161B23` | Drawer / toolbar background |
| `surface.plot` | `#0E1218` | ScottPlot data area |
| `surface.card` | `#1A1F27` | Stat card, inactive |
| `surface.card.focus` | `#172230` | Stat card, focused channel |
| `surface.armed` | `#15314E` | Alignment bar background |

### Text

| Token | Hex | Usage |
|---|---|---|
| `text.primary` | `#E6E9EF` | Values, primary labels |
| `text.secondary` | `#9AA3B2` | Secondary labels |
| `text.dim` | `#5C6573` | Disabled / hidden-channel text |
| `text.accent` | `#9AC7F2` | Focused / armed accent text |

### Borders

| Token | Value | Usage |
|---|---|---|
| `border.default` | `rgba(255,255,255,0.08)` | Region dividers |
| `border.emphasis` | `rgba(255,255,255,0.12)` | Window outline |
| `border.focus` | `#2C5A86` | Focused card, armed bar |
| `border.grid` | `rgba(255,255,255,0.05)` | Plot gridlines |

### Trace palette

Run A = solid, full opacity. Run B = dashed (`5 3`), same hue, full opacity. Context (non-focused, visible) = same hue at **0.28 opacity**. Hidden = not drawn.

| Channel | Hex |
|---|---|
| Motor RPM | `#4C9AED` (blue) |
| Throttle | `#EF9F27` (amber) |
| Battery Current | `#2BB48A` (teal) |
| Battery Voltage | `#E0744B` (coral) |
| Power (W) | `#8E86E6` (purple) |
| Power Out % | `#7FB6F2` (light blue) |
| Battery Ripple | `#97C459` (green) |
| ESC / Controller Temp | `#E24B4A` (red) |
| Motor Temp | `#BA7517` (deep amber) |
| Motor Timing | `#B4B2A9` (warm grey) |
| Acceleration | `#D85A30` (dark coral) |
| Capacity (mAh) | `#888780` (grey) |
| **GPS — Speed (mph)** | `#D4537E` (pink) |
| **GPS — Distance** | `#639922` (dark green) |
| **GPS — G-force** | `#9F8FE0` (light purple) |

> Note on colour: chasing fifteen non-clashing hues simultaneously is a losing game — it is exactly what made the Castle plot a wall of noise. The real defence against clash is the focus model (only one channel is bright at a time) plus the user rarely showing more than 3–4 channels at once. Keep these assignments **stable** (a channel is always the same colour) and accept that two dim context traces may sit near each other; that is acceptable because neither is the one being read.

### Split markers

| Marker | Hex | Style |
|---|---|---|
| 66 ft | `#7C5A2E` | vertical dashed `3 4` |
| 132 ft | `#7C5A2E` | vertical dashed `3 4` |
| Finish | `#9A3D2E` | vertical dashed `3 4` |

### Typography

WinForms default UI font (Segoe UI) is fine. Sizes: values 12–13 px, labels 11 px, axis/tick text ~10 px. Two weights only — regular and medium. No all-caps, sentence case throughout.

---

## 6. Component specifications

### 6.1 Title bar
- Height ~32 px, `surface.bar`.
- Left: app glyph + `DragOverlay`.
- Right: fullscreen-plot toggle (`ti-arrows-maximize` equivalent), then standard window controls.

### 6.2 Run strip
- Height ~40 px, `surface.bar`, wraps if needed.
- One **chip per loaded log**. Chip contents: master visibility toggle (eye / eye-off), source tag (`ESC` or `GPS`), short name (ellipsis-truncated, tooltip = full path).
- A chip in the **armed-for-alignment** state gets `border.focus` + `text.accent`.
- Empty slots show a `Load …` button; once a log is loaded the button is replaced by the chip. Default config: up to 3 ESC + 3 GPS logs (matches current build), but the strip should grow gracefully if that changes.

**Master visibility toggle:** hides/shows *every line from that log at once*, independent of and without losing the per-channel toggles in the drawer. Re-showing the log restores the previous per-channel state.

### 6.3 Plot (ScottPlot v5)
- `surface.plot` data area, `border.grid` gridlines, no plot title.
- Traces: line only, **no per-point markers** (markers were a major noise source). Line width ~2 px for focused, ~1.3 px for context.
- **Focused channel** owns the single visible left Y axis, labelled in its hue with real units (e.g. `Motor RPM`, `0 / 20k / 40k / …`). When focus changes, the visible axis re-scales/re-labels to the new channel.
- **Legend:** compact, inside the plot, top-left or top-right; entries are the *visible* channels, colour-coded. (Optional — the drawer already serves as a legend; include only if it does not crowd the data.)
- X axis: `Time (s)`, shared by all logs.
- Split markers drawn as vertical lines per token above, with small top labels (`66ft`, `132ft`, `finish`).
- Cursor readout: a vertical crosshair follows the mouse; the X value drives the `@cursor` figures in the stat cards (interpolate each visible channel at cursor X).

### 6.4 Channel drawer
- `surface.panel`, full width, docked under the plot, **collapsible**.
- Collapsed: a single ~32 px row — a chevron handle + one coloured dot per channel with a short label; tapping a dot toggles that line's visibility. Hidden channels show a dim dot.
- Expanded: a wrapping grid of **stat cards** (`auto-fit`, min ~150 px) + chevron handle to collapse.
- Cards are grouped/ordered by log so GPS channels read as a set distinct from ESC channels.

### 6.5 Stat card
Per channel, per the focus/visibility state:

```
[● colour dot] Channel name              [eye toggle]
A  max ……  · avg ……
B  max ……  · avg ……
@cursor  A ……  · B ……
```

- **Default** card: `surface.card`, `border.default`, `text.secondary` figures.
- **Focused** card: `surface.card.focus`, `border.focus`, `text.accent` heading. Clicking anywhere on a card (not the eye) focuses that channel.
- **Hidden** channel card: `text.dim` throughout, eye shows `eye-off`, `@cursor` reads `hidden`.
- Numbers: thousands separators, sensible per-unit precision (RPM integer, volts 2 dp, % integer, etc.). Min may also be shown if space allows; Max / Avg / @cursor are the priority three.

### 6.6 Alignment system
Aligning means time-shifting one log so its launch overlays the reference run (ESC and GPS logs do not start at the same instant, and two ESC runs launch at different log times).

- **Arming:** click a log's run-strip chip → that log is *armed*; the alignment bar appears.
- **Alignment bar** (`surface.armed`, `border.focus`): label `aligning: <log name>`, then `auto` (bolt), `«` `‹` `›` `»` nudge buttons, a live `offset +0.04s` readout, and a `reset` action. Disappears when the log is disarmed (chip clicked again / Esc).
- **Primary interaction — drag:** with a log armed, the user drags its trace left/right directly on the plot to slide it in time. Cheaper and more intuitive than clicking buttons repeatedly.
- **Fine interaction — arrow keys:** `←` / `→` nudge the armed log by a fine step; `Shift+←/→` by a coarse step. (The `«/»` buttons are the coarse step, `‹/›` the fine step — same actions, discoverable for users who don't know about drag.)
- **Auto-align:** one-shot snap. Detect launch and align it to the reference: ESC launch = throttle pulse > 1.65 ms; GPS launch = speed onset. Sets the offset, which remains user-adjustable afterwards.
- **Offset model:** alignment applies a per-log X (time) offset added to that log's timestamps before plotting. It must **not** mutate source data, must persist while the session is open, and `reset` returns the offset to zero.

---

## 7. States and interactions

| Element | Action | Behaviour |
|---|---|---|
| Channel dot (collapsed strip) | click | Toggle that channel's visibility; dot dims when hidden |
| Stat card body | click | Focus that channel — it brightens, owns the Y axis, others dim to context |
| Stat card eye | click | Toggle visibility without changing focus |
| Drawer handle (chevron) | click | Collapse ⇄ expand the drawer; plot resizes to fill |
| Run-strip chip eye | click | Master toggle: hide/show all lines of that log, preserving per-channel state |
| Run-strip chip body | click | Arm/disarm that log for alignment; alignment bar appears/hides |
| Plot, log armed | drag horizontally | Slide the armed log in time; offset readout updates live |
| Plot, log armed | `←` `→` / `Shift+arrow` | Fine / coarse time nudge of armed log |
| Alignment `auto` | click | Detect launch and snap offset; remains adjustable |
| Alignment `reset` | click | Offset back to 0 |
| Plot | mouse move | Crosshair tracks X; `@cursor` figures update for visible channels |
| Title bar fullscreen / `F11` | toggle | Hide all regions except plot, and back |

Transitions: panel collapse/expand may animate briefly (~120 ms) or be instant; do not block interaction on animation. Avoid anything decorative.

---

## 8. Edge cases

- **No logs loaded:** plot shows a quiet centered `Waiting for log…` empty state on `surface.plot`; run strip shows `Load …` buttons; drawer collapsed/empty.
- **One run only (no Run B):** everything works; Run B rows in cards read `—`; no dashed trace drawn. Alignment still available for aligning ESC vs GPS within the single run.
- **GPS loaded, no ESC (or vice-versa):** valid; only the present source's channels appear.
- **Mismatched sample rates** (ESC ~20 Hz vs RaceBox ~25 Hz): both plot against real time on the shared axis; do not resample for display. `@cursor` interpolates each channel independently at the cursor's X.
- **All channels hidden:** plot is empty but axes/markers remain; do not error. A subtle hint ("all channels hidden") is acceptable.
- **Very long filenames:** ellipsis-truncate in chips and card headers; full text in tooltip.
- **Window below minimum:** regions keep height, plot may become short but must not be clipped to zero; drawer should auto-collapse if the plot would drop below a usable height (~200 px).
- **Auto-align fails** (no clear launch found): leave offset unchanged, surface a brief non-blocking message, fall back to manual.

---

## 9. Keyboard and accessibility

- `F11` — fullscreen plot toggle.
- `Esc` — disarm alignment / exit fullscreen.
- `←` `→` — fine nudge armed log; `Shift+←/→` — coarse nudge.
- Tab order: run strip → alignment bar (when present) → drawer handle → cards.
- Every icon-only control (eye toggles, handle, nudge buttons, fullscreen) needs an accessible name / tooltip describing its action and current state ("hide Motor RPM", "show ESC · Yeti tune").
- Do not rely on colour alone: hidden state is shown by the `eye-off` icon and dimmed text, not just a faded dot.

---

## 10. ScottPlot v5 implementation notes

These are direction, not gospel — verify against the installed ScottPlot v5 API; the v5 surface differs from v4 and shifts between minor versions.

- **Theme:** set figure background, data background, axis frame, tick label and grid colours to the tokens (`Plot.FigureBackground`, `Plot.DataBackground`, `Plot.Axes.*`, grid colour via the grid object). Colours via `ScottPlot.Color.FromHex("#…")`.
- **Traces:** add each channel as a `Scatter` / signal; set `MarkerStyle` invisible (size 0), `LineWidth`, `Color` (with alpha for context), `LinePattern.Dashed` for Run B.
- **Multiple / focused axis:** v5 supports additional axes (`Plot.Axes.AddLeftAxis()` etc.) and binding a plottable to an axis via its `Axes.YAxis`. For the focus model, keep one visible left axis bound to the focused channel and hide the others; rebind/relabel on focus change. Alternatively keep all axes but show only the focused one.
- **Legend:** `Plot.Legend` / `ShowLegend`, populate from visible channels.
- **Crosshair / cursor:** add a `Crosshair` plottable; on mouse-move convert pixel → coordinate (`Plot.GetCoordinates`) to get cursor X, then interpolate each visible channel for the `@cursor` readout.
- **Alignment offset:** apply the per-log time offset to the X array feeding the plottables (or via the plottable's X-offset property if available) and refresh — never edit source data. Drag = capture pixel delta → convert to time delta via the axis scale → update offset.
- Always `FormsPlot.Refresh()` after state changes.

---

## 11. WinForms feasibility notes (honest)

- **Native / easy:** the entire plot — dark theme, palette, focus brighten/dim, single focused axis, legend, split markers, crosshair, no markers — is standard ScottPlot v5 config plus a little focus logic. This is the bulk of the visual win.
- **Easy:** stat cards' content, eye toggles, collapse logic, master log toggle, arrow-key nudge — straight WinForms panels + events.
- **Needs custom paint:** dark chrome and the rounded card look are not free in WinForms. Two routes: (a) set dark `BackColor` / flat `FlatStyle` on every control and owner-draw the card panels — free, moderate effort, ~90% of the mockup; (b) a theming control library — faster but a dependency. **Rounded corners are the one fiddly bit** — square cards with a 1 px border get most of the way without paint code, so do not block on corner radius.
- Drag-to-align needs mouse capture + pixel→time conversion; straightforward but test the conversion against the axis scale.

---

## 12. Build sequence

Each phase is an independent, visually-QA-able win. Implement and review in order; later phases assume earlier ones.

1. **Theme + plot restyle** — dark surfaces, the trace palette, kill per-point markers, single focused axis + on-plot legend, split markers. *Accept: app looks like the dark mockup with existing data loaded; one channel can be focused (even if focus is hard-coded initially).*
2. **Collapsible channel drawer + per-channel toggles** — drawer collapses to the dot strip and expands to stat cards; each card/dot toggles its line; cards show Max/Avg/@cursor. *Accept: drawer collapses/expands, plot resizes to fill, toggles show/hide lines, cursor updates @cursor figures.*
3. **Focus model** — clicking a card focuses its channel: it brightens, owns the Y axis, others dim to context. *Accept: focus changes the bright trace and the visible axis/labels.*
4. **Run strip + whole-log master toggles** — chips per log, master visibility toggle hides/shows a whole log preserving per-channel state. *Accept: master toggle dims an entire log and restores prior state.*
5. **RaceBox GPS ingest** — parse GPS logs onto the shared time axis as additional channels (Speed, Distance, G-force), grouped in the drawer. *Accept: a GPS log loads and its channels overlay correctly with ESC channels.*
6. **Alignment** — arm a log via its chip, drag / nudge / auto-align, live offset readout, reset, non-destructive offset. *Accept: an armed log slides in time, auto-align snaps the launch, reset zeroes it, source data untouched.*

---

## 13. Out of scope for v2

- Light theme (palette swap, revisit if needed for sunlit pit use).
- Export / report generation.
- Persisting alignment offsets or per-channel state between sessions.

---

## 14. Implementation entry points (CC to confirm)

Before phase 1, the build session should locate and note:
- The main form class name and file.
- Where the `FormsPlot` is instantiated and currently configured.
- The CSV ingest path (CsvHelper mapping) for ESC logs, as the template for the GPS reader in phase 5.
- The existing launch-detection logic (throttle > 1.65 ms) to reuse in auto-align.
