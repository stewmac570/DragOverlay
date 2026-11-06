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
| 8-9 | Boost RPM start | raw x 10 | Matched once (round 3) |
| 10-11 | Boost RPM end | raw x 10 | Matched once (round 3) |
| 15 | Sensorless Motor Timing | raw | Inference: 5 in Smart Sense, 0 in Sensored Only |
| 16 | Boost Timing Amount and Mode | low 7 bits = degrees; top bit set = throttle based, clear = RPM based | Matched (rounds 3 and 6) |
| 17 | Motor Type | top bit = checkbox, low bits = type, 0x81 = Sensored Only | Matched |
| 18 | Drag Brake | raw / 255 x 100 % | Matched |
| 19 | Drag Brake Ramp | 0 = disabled, 1 = very fast, 4 = slow | Matched |
| 20 | Punch Control | % | Matched |
| 21 | Max Power | % | Matched |
| 22 | Reverse Percentage | % | Matched round 3; 0 when reverse is off |
| 23 | Brake Amount | % | Matched |
| 24 | Reverse To Forward Brake | % | Matched round 3; 0 when reverse is off |
| 25 | Throttle Dead Band | raw / 1000 ms | Matched (rounds 1 to 3) |
| 26 | Brake Lockout Time | raw / 128 seconds | Matched (rounds 1 to 4) |
| 27 | Idle/Error Beep Disable | raw | Matched |
| 28 | Start Power | % | Matched |
| 29 | Reverse Type | 0 = with reverse, 1 = without | Matched |
| 30 | Motor Direction | 0 = normal, 1 = reverse | Matched |
| 32 | Cutoff Voltage | raw / 10 V (64 = 6.4 V, read off the app) | Matched once (round 5) |
| 33 | Motor Temperature Cutoff | deg C, 0 = disabled | Matched |
| 34 | Auxiliary Wire Mode | 1 = max power, 3 = max brake | Matched |
| 35 | BEC Voltage | raw / 10 V | Matched |
| 36 | Sample Frequency | samples per second | Matched |
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
Curve and table 4 Boost By Throttle. Brake was confirmed by changing only the
brake curve (round 7): only table 1 moved. In the `.dat` each curve is a leading `0` then the 256 table
values, joined by full stops; that matched the ESC exactly in round 3.

## What is not verified

- Auto-Lipo, and why the `.dat` said Cutoff Voltage 255 in round 3 while byte
  32 held 1. Byte 31 went 32 to 0 in round 2 only.
- Boost Mode Enable and Turbo Enable have no byte of their own. Turning boost
  off (round 6) zeroed bytes 8 to 11 and 16; turning turbo off (round 2)
  zeroed bytes 39 to 42. Inference: the app shows them on when those bytes are
  not zero.
- Where the boost throttle activation range lives.
- Table 3, all zero in every capture, and Programmed Throttle.
- Bytes 12 to 14, 37, 38, 46 and 47 never changed.

## Samples

In `Tests/castle-link-samples/`:

- `tune-2026-11-06.bin`: the 48 settings bytes then the 2,560 curve bytes, as
  read after the write.
- `tune-2026-11-06.dat`: the `.dat` Castle's app saved for that tune.
- `tune-2026-11-06-before-settings.bin`, `tune-2026-11-06-before-curves.bin`:
  the same reads before the write.
- `tune-2026-11-06-round2.bin` and `.dat`, `tune-2026-11-06-round3.bin` and
  `.dat`: the ESC after each round's write, with the `.dat` saved for it.

## Steps

1. Capture a tune read. Done.
2. Map every `.dat` setting to its byte and scale. Mostly done; the gaps are
   listed above.
3. Read the tune during "Get Castle log" and save it next to the log.
