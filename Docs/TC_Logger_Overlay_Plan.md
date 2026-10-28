# TC logger support in DragOverlay: plan

Written 28 Oct 2026 by the rc-logger session, for a DragOverlay (CastleOverlayV2) session to build.
Ready-to-run prompt at the end.

## Why

The TC logger is a version of the Mini Logger that sits inline on the throttle, between the receiver and the ESC. It's a demonstration that a box fitted as a data logger can do traction control, to back the case for banning data loggers from racing. During a pass it compares motor RPM with an **RPM-against-time curve**. If RPM gets near the curve, it lowers the throttle it sends to the ESC.

The curve is meant to be made in DragOverlay. Stew opens a good pass, draws a curve a bit above its RPM trace, and sends it to the logger over the USB cable. DragOverlay also needs to load TC runs and show where the logger cut.

## Source of truth (rc-logger repo)

`C:\Users\Stewart McMillan\source\repos\rc-logger`. Merge rc-logger PR #15 first, so these are on main:

- `docs/FIRMWARE_TC_AND_BIG.md`: the TC logger brief, with "TC as built" at the end of section 3.
- `docs/Mini_Logger_USB_Download.md`: the existing USB contract (already used by "Download from Mini Logger").
- `firmware/tc_logger/tc_logger.ino`: the command handling (`tcCommand`, `tcStatus`), if anything below is unclear.
- `firmware/tc_logger/tc_config.h`: settings and their defaults.

## Facts DragOverlay needs

### Telling a TC logger from a Mini Logger
- Both answer `id` with `MINILOGGER 2`, so the existing connect works unchanged.
- Then send `tc status`. A TC logger's first reply line starts with `TC ` (for example `TC DISARMED | in 1520 us | ...`), and the reply ends with a line `END`. A plain Mini Logger replies with one line starting `Commands:`.

### `tc status` reply
```
TC <STATE> | in <us> us | out <us> us | ceiling <us or none> | rpm <rpm> | curve <n> points[, uploaded <s> s ago]
  neutral <us> us | full <us> us | brake <us> us | pulses sent <n> | pulses cut <n> | signal timeouts <n> | log lost <n>
  output edge delay in the interrupt: ...            (only once pulses have been sent)
  neutralUs <v>
  fullUs <v>
  brakeUs <v>
  signalTimeoutMs <v>
  armBrakePct <v>
  armHoldMs <v>
  blipPct <v>
  blipMs <v>
  launchPct <v>
  nearRpm <v>
  gainUsPerRpm <v>
  recoverUsPerMs <v>
  confirmSamples <v>
  firmware tc_logger <date> <time>[ SIMULATION]
END
```
`<STATE>` is `DISARMED`, `ARMED`, `ACTIVE`, `DONE` or `NO_SIGNAL`. Parse the second line for neutral, full and brake (the values in use, learned or stored). Parse the `NAME VALUE` lines for the settings.

### Sending a curve
```
tc curve begin              -> OK                 (ERROR busy: a pass is running, if ACTIVE)
tc curve pt <t_ms> <rpm>    -> OK                 (repeat; t_ms whole ms after launch, strictly rising; rpm whole)
tc curve end                -> OK <n> points      (saved in flash, survives power-off)
tc curve clear              -> OK 0 points
```
- **Up to 128 points.** Linear interpolation between them. Before the first point, the first point's RPM applies. **After the last point the logger stops cutting** (DONE), so the last point sets how long traction control is active.
- RPM is **real motor RPM**, the same number as the CSV's `Motor RPM` column (already divided by pole pairs). Never apply the 4-pole halving to it.
- Any reply starting `ERROR` means stop, show the message, and don't send `end`. The logger keeps its old curve until `end` succeeds.
- Read replies line by line and ignore lines that don't start with `OK` or `ERROR`. The startup banner can arrive at any time.
- After `end`, send `tc status` and check the point count matches.
- There's no command to read the curve back yet. If DragOverlay needs one, ask the rc-logger session for `tc curve list`; don't guess at one.

### Time zero: the curve's clock
The logger's t = 0 is **the moment the radio throttle passes `launchPct` of forward travel**:

`launch_us = neutral + (full - neutral) * launchPct / 100` (from `tc status`; default launchPct 20)

This is **not** DragOverlay's current Mini Logger alignment point (50% of rest to peak, `MiniLoggerLoader.LaunchThrottlePercent`). When building a curve from a run:
- **TC run (has the `TC State` column):** t = 0 is the first row where `TC State` changes to 2 (ACTIVE). That's the logger's own launch, exact.
- **Plain Mini Logger run, or a TC run that was never armed:** t = 0 is the first row of the pass where `Throttle (ms) * 1000 >= launch_us`, using neutral, full and launchPct read from the connected logger with `tc status`. Without a connected logger, ask for them, or use neutral = the run's median rest throttle, full = neutral + 400 µs and launchPct = 20 (the firmware defaults), and say so on screen.

### TC run CSV
```
Time (s),Throttle (ms),Motor RPM,Wheel (rev/s),Throttle Out (ms),TC State
```
- The first four columns are exactly as the Mini Logger's. **`Throttle Out (ms)`** is what was sent to the ESC (0 = nothing sent). **`TC State`** is 0 DISARMED, 1 ARMED, 2 ACTIVE, 3 DONE, 4 NO_SIGNAL.
- A cut shows where `Throttle Out` is below `Throttle (ms)` while the state is 2.
- **This won't load today.** `MiniLoggerLoader.IsMiniLoggerCsv` requires the header to *equal* the four-column header. The row parser already copes with extra fields (`f.Length < 4`), so only the header check needs to accept the four-column header followed by more columns.

### Safety to show the user
Before connecting: "Switch the car off and unplug the logger's receiver lead." It's the same as for the Mini Logger. The car's BEC can back-feed the USB cable if both are connected.

## What to build

1. **Load TC runs.** Accept the six-column header. Add two channels, for example "Logger Throttle Out %" (on the same percent scale as "Logger Throttle %") and "TC State". Shade or mark ACTIVE, and the rows where the output was cut.
2. **Make a curve from a run.** From a loaded Mini Logger or TC run, take Motor RPM against time from the logger's launch, as defined above. Offer a starting curve: the run's RPM plus a margin (a user setting, in RPM or percent), thinned to 128 points or fewer, ending at a user-chosen time. Show it on the chart over the RPM trace, and let the user edit points before sending.
3. **Send to logger.** Connect with the existing `MiniLoggerUsb` connect, confirm it's a TC logger with `tc status`, refuse while ACTIVE, send `begin`, the points and `end` with each reply checked, then confirm the count with `tc status`. Show the result.
4. **Save the curve with the project,** so it can be resent. Optional.

## Open items (not decided, don't invent)
- Margin default and curve length: Stew's call, so make them settings.
- All logger settings are placeholders until Stew tunes them on the bench. Read them from `tc status`, and never hard-code them.
- Nothing on the TC logger has run on hardware yet, so the first real send is part of the bench test.

---

## Prompt for the DragOverlay session

```
TASK
Add TC logger support to DragOverlay: load TC logger runs (two extra CSV columns), build an RPM-against-time curve from a run, and send it to the TC logger over the USB cable.

CONTEXT
Read Docs/TC_Logger_Overlay_Plan.md in full first. It is the contract. The logger side is in the rc-logger repo (see "Source of truth"). Don't change anything in rc-logger. Read the existing Mini Logger code first: Services/MiniLoggerLoader.cs and Services/MiniLoggerUsb.cs, and how "Download from Mini Logger" uses them. Build on them; don't duplicate them.

FILES TO EDIT
- Services/MiniLoggerLoader.cs: accept the four-column header followed by more columns. Parse "Throttle Out (ms)" and "TC State" when present.
- Services/MiniLoggerUsb.cs: add TC detection (tc status), parse tc status, and send the curve (begin, pt, end) with reply checks.
- New UI for making, editing and sending the curve, in the existing style.
- Tests or a verify plan in the PR body for the header change and the curve send (use the existing fake transport pattern if MiniLoggerUsb has one).

DO NOT
- Do not change behaviour for plain Mini Logger files or the existing download.
- Do not invent logger commands. If something is missing (e.g. reading the curve back), stop and say so.
- Do not apply 4-pole halving to logger RPM.
- Do not hard-code logger settings; read them from tc status.
- Do not send anything while tc status reports ACTIVE.

EXACT SPECIFICATIONS
- Curve: up to 128 points, whole ms strictly rising, whole RPM, t = 0 as defined in the plan ("Time zero").
- Any ERROR reply aborts the send without "tc curve end", and is shown to the user.
- After sending, tc status point count must match, or report failure.
- Before connecting, show: "Switch the car off and unplug the logger's receiver lead."

ACCEPTANCE CRITERIA
- A plain Mini Logger CSV loads exactly as before.
- A six-column TC CSV loads, with Throttle Out and TC State visible.
- Curve send works against a fake transport that replays the replies in the plan, including an ERROR case.
- Builds clean (dotnet build, Release).
```
