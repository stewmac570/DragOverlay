# Castle Link tune read

## Goal

When DragOverlay downloads a Castle log, it also reads the tune that is on the
ESC and saves it next to the log as a `.dat`. Every pass then records what tune
it ran.

## What is verified

One capture on 2026-11-06, Cobra 8 Ace on firmware 1.36, using
`tools/castle-link/Capture Castle tune.bat`.

- The tune is two reads, using the same 13-byte short frames as the log
  download. A request is `43`, command, checksum, then a 2-byte block number,
  padded with `FF`. A reply is `43`, command, checksum, `00 00`, then 8 bytes.
- Command `06`, blocks 0 to 5: 48 bytes of settings.
- Command `0E`, blocks 0 to 319: 2,560 bytes of curves. These read as five
  tables of 256 values, each value 2 bytes, low byte first.
- The log download already sends both reads before the session list, so the
  tune comes over in the same connection as the logs.
- In this capture the Castle app also wrote a tune to the ESC: command `0A`,
  then `0B` for each settings block (each read back with `06`), `0C`, then `0D`
  for each curve block. The second read matched what was written, byte for
  byte.
- Before the write, turbo values were 0 and the first table was a straight
  line. After it, settings bytes 39 to 42 read 10, 80, 60, 60. The saved `.dat`
  says turbo 10 degrees, activation 80%, acceleration and deceleration 60
  deg/second, so those four bytes are matched by value.

## Settings map

Two more captures on 2026-11-06 (rounds 2 and 3), each changing most settings
in one write and saving a `.dat`. A byte is listed as matched when it moved
with its setting in both rounds and the value fits the `.dat` both times.
Two-byte values are high byte first unless marked.

| Byte | Setting | Value | Status |
| --- | --- | --- | --- |
| 0-1 | Data Log Enabled | low byte first, 8191 = all 13 channels | Matched |
| 2-3 | Full Forward | raw | Matched |
| 4-5 | Full Reverse | raw | Matched |
| 6-7 | Neutral | raw | Matched |
| 8-9 | Boost start | RPM mode: raw x 10 RPM. Throttle mode: % | Matched (rounds 3 and 8) |
| 10-11 | Boost end | RPM mode: raw x 10 RPM. Throttle mode: % | Matched (rounds 3 and 8) |
| 15 | Sensorless Motor Timing | raw | Inference: 5 in Smart Sense, 0 in Sensored Only |
| 16 | Boost Timing Amount and Mode | low 7 bits = degrees; top bit set = throttle based, clear = RPM based | Matched (rounds 3 and 6) |
| 17 | Motor Type | top bit = Sensor Lost Warning checkbox; low bits 0 Smart Sense Brushless, 1 Sensored Only, 2 Brushed Reversing | 0 and 1 matched |
| 18 | Drag Brake | raw / 2.55 %, shown as a whole number with .0 | Matched |
| 19 | Drag Brake Ramp | list position: 0 Disabled (Instantaneous), 1 Very Fast, 2 Fast, 3 Medium, 4 Slow, 5 Very Slow | 0, 1, 3, 4, 5 matched; Fast = 2 by elimination |
| 20 | Punch Control | % | Matched |
| 21 | Max Power | % | Matched |
| 22 | Reverse Percentage | % | Matched round 3; 0 when reverse is off |
| 23 | Brake Amount | % | Matched |
| 24 | Reverse To Forward Brake | % | Matched round 3; 0 when reverse is off |
| 25 | Throttle Dead Band | raw / 1000 ms | Matched (rounds 1 to 3) |
| 26 | Brake Lockout Time | raw / 127.5 seconds, shown to 2 places (255 = 2.00) | Matched (rounds 1 to 4 and 8) |
| 27 | Idle/Error Beep Disable | raw | Matched |
| 28 | Start Power | % | Matched |
| 29 | Reverse Type | 0 = with reverse, 1 = without | Matched |
| 30 | Motor Direction | 0 = normal, 1 = reverse | Matched |
| 31 | Auto-Lipo Volts/Cell | raw / 10 V | Matched (rounds 2 and 8) |
| 32 | Cutoff Voltage | 255 = Auto-Lipo, otherwise raw / 10 V (64 = 6.4 V, read off the app) | Matched (rounds 5 and 8) |
| 33 | Motor Temperature Cutoff | deg C, 0 = disabled | Matched (49, 71, 93) |
| 34 | Auxiliary Wire Mode | list position: 0 None, 1 Max Power, 2 Reverse Percentage, 3 Max Brake, 4 Drag Brake, 5 Reverse Enable/Disable, 6 Torque Control, 7 Start Power Control | 1, 3, 4 matched; rest by list position |
| 35 | BEC Voltage | raw / 10 V | Matched |
| 36 | Sample Frequency | samples per second | Matched |
| 38 | Controller Orientation | 0 = power wires to the front, 1 = to the back | Matched (round 8, Stew confirmed) |
| 39 | Turbo Timing Amount | degrees | Matched |
| 40 | Turbo Activation Throttle | % | Matched round 3 |
| 41 | Turbo Acceleration | deg per second | Matched round 3 |
| 42 | Turbo Deceleration | deg per second | Matched round 3 |
| 43 | RPM Limit | kRPM, 0 = disabled | Matched |
| 44 | Start Power Ramp Time | ms | Matched |
| 45 | Start Power Hold Time | ms | Matched |

The `.dat` keeps showing a setting's last value when that feature is off, for
example boost RPM 1000 to 2000 and turbo activation 100% while the ESC holds 0.

## Curves map

Table 0 is the Forward Curve, table 1 the Brake Curve, table 2 the Reverse
Curve, table 3 Programmed Throttle and table 4 Boost By Throttle. Brake was confirmed by changing only the
brake curve (round 7): only table 1 moved. In the `.dat` each curve is a leading `0` then the 256 table
values, joined by full stops; that matched the ESC exactly in round 3.

## Drop-down wording

From screenshots of every list in Castle Link 2 on 2026-11-06. `(*)` marks the
factory default; some `.dat` files carry it after the value.

- Throttle Dead Band: Very Small (0.0250 ms), Very Small (0.0375 ms), Small
  (0.0500 ms), Small (0.0625 ms), Small (0.0750 ms), Average (0.0875 ms),
  Average (0.1000 ms), Average (0.1125 ms), Large (0.1250 ms), Large (0.1500 ms).
- Motor Temperature Cutoff: Disabled, Low 120/130/140 F, Medium 150/160/170/180
  F, High 190/200/210 F, each written as `Low (120°F / 49°C)`.
- Sensorless Motor Timing: Lowest (0), Low (5), Normal (10), High (15),
  Highest (20).
- Cutoff Voltage: No Cutoff, 6.4V, 9.6V, 12.8V, Auto Li-Po, Custom.
- Controller Orientation: Power Wires to the Front, Back, Left, Right. Only
  Front (0) and Back (1) are matched.
- Sample Frequency: Disabled, 1, 2, 5, 10, 20 Sample / Sec.
- Boost Timing Mode: RPM Based Boost, Throttle Percentage Based Boost.

## Programmed Throttle

Settings live in table 3, not in the settings bytes (round 9):

- Entries 0 to 199 are the ramp from 0 to 65534.
- Entry 200 packs two bytes: low byte = Time Scale x 100 (1.0 s = 100,
  2.0 s = 200), high byte = Enable At Throttle % (100, then 50).
- Interpolation changes only the ramp shape; the ESC holds no separate flag.

## Curve lines in the `.dat`

Two forms appear. Full table: `0.` then the 256 values, as the ESC holds them.
Points: interpolation code, then `x.y` pairs run together, for example Boost By
Throttle `2.0,0.255,39321` is curve type 2 with points (0, 0) and (255, 39321).
Programmed Throttle adds the time scale in front: `2-1.0,0.199,65534` is 2.0 s,
linear (1), points (0, 0) and (199, 65534). Inference from rounds 8 and 9:
curve type 1 = linear, 2 = curve. A tune rebuilt from the ESC can only use the
full table form, because the ESC does not keep the points.

## What is not verified

- Boost Mode Enable and Turbo Enable have no byte of their own. Turning boost
  off (round 6) zeroed bytes 8 to 11 and 16; turning turbo off (round 2)
  zeroed bytes 39 to 42. Inference: the app shows them on when those bytes are
  not zero.
- Torque Control and Current Limiting: changing them (round 8) wrote nothing
  new to this ESC. Inference: the Cobra 8 Ace does not store them. To be
  checked on an ESC that has current limiting.
- Drag Brake Ramp Fast as 2: the only value left once 0, 1, 3, 4 and 5 were
  matched. Motor Type Brushed Reversing as 2, by list position.
- The `.dat` boost RPM range keeps the app's last values while in throttle
  mode, so it cannot be rebuilt from the ESC.
- Programmed Throttle Enable At Throttle does not appear in the `.dat` line.
- Bytes 12 to 14, 37, 46 and 47 never changed.

Round 8 check: rebuilding `download test 7.dat` from the ESC bytes matched 34
settings and all four curves exactly; the only differences were the lockout
and drag brake rounding, now fixed in the table above.

## Samples

In `Tests/castle-link-samples/`:

- `tune-2026-11-06.bin`: the 48 settings bytes then the 2,560 curve bytes, as
  read after the write.
- `tune-2026-11-06.dat`: the `.dat` Castle's app saved for that tune.
- `tune-2026-11-06-before-settings.bin`, `tune-2026-11-06-before-curves.bin`:
  the same reads before the write.
- `tune-2026-11-06-round2.bin` and `.dat`, `tune-2026-11-06-round3.bin` and
  `.dat`: the ESC after each round's write, with the `.dat` saved for it.
- `tune-2026-11-06-round8.bin` and `.dat`: the ESC at the end of the live
  recording, with `download test 7.dat`.
- `tune-2026-11-06-round9.bin` and `.dat`: the same after the programmed
  throttle, aux wire and temperature writes, with `download test 8.dat`.

## Steps

1. Capture a tune read. Done.
2. Map every `.dat` setting to its byte and scale. Mostly done; the gaps are
   listed above.
3. Read the tune during "Get Castle log" and save it next to the log. Done:
   `Services/CastleTuneDat.cs` writes `Castle session N <date>.dat` beside each
   CSV and attaches it to the run. Lines the ESC does not hold (current
   limiting, torque control, the inactive boost range) are left out, and
   Programmed Throttle is written as DragOverlay's own two lines (time scale,
   enable at throttle) because the ESC does not keep Castle's curve points.
