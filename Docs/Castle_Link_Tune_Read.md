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

## What is not verified

- Which table is which. Inference from shape: table 0 is the forward curve
  (rises fast, ends at 65535), tables 1 and 2 are straight lines (one is the
  brake curve), table 4 ends at 39321, which is the boost-by-throttle value in
  the `.dat`. Table 3 is all zero.
- Which byte is which setting, apart from the four above. The `.dat` is a text
  file of about 50 named settings, so each one needs mapping to a byte and a
  scale before DragOverlay can write a `.dat` Castle's app will open.
- How the forward curve's control points in the `.dat` relate to the 256 values
  the ESC holds.

## Samples

In `Tests/castle-link-samples/`:

- `tune-2026-11-06.bin`: the 48 settings bytes then the 2,560 curve bytes, as
  read after the write.
- `tune-2026-11-06.dat`: the `.dat` Castle's app saved for that tune.
- `tune-2026-11-06-before-settings.bin`, `tune-2026-11-06-before-curves.bin`:
  the same reads before the write.

## Steps

1. Capture a tune read. Done.
2. Map every `.dat` setting to its byte and scale. Changing one setting at a
   time and reading again shows which byte moves.
3. Read the tune during "Get Castle log" and save it next to the log.
