# Castle Link direct download plan

## Goal

Plug the Castle Link into the ESC and the PC, press one button in DragOverlay,
and the log arrives already named and matched to the pass. This replaces the
Castle app steps: download, viewer, name, save, import.

## Locked decisions

- The cable swap stays: unplug the ESC lead, plug in the Castle Link.
- PC-side only. Nothing changes on the ESP32 logger.
- The feature is built into DragOverlay.
- Capture what the Castle app sends and receives, work out the conversation,
  and rebuild it in C#.

## What is verified

- The Castle Link appears in Windows as a standard COM port. Its USB name is
  "Castlelink Board", USB ID `C410:8370`, using the Windows `usbser` driver.
  Seen live on 2026-11-02 on COM3.
- Windows remembers a second board with a different serial number on COM13.
  Treating that as a second Castle Link is an inference.
- `USBPcapCMD` is installed. Wireshark's own capture does not work on this PC
  because Npcap is missing, and it is not needed for this job.
- The capture script works. Two captures on 2026-11-02, a Cobra 8 Ace on
  firmware 1.36, sessions 33 and 32.
- Serial settings seen in the capture: 9600 8N1.
- Every message checksum passes: all bytes in a frame add up to 0 (mod 256).
- The decoder and formulas below rebuilt both logs and matched every cell of
  both exported CSVs: 767 rows, 9,204 cells, no misses. Cells were compared to
  the precision the CSV prints.

## Protocol as captured

Frames:

- Short frame, 13 bytes: `43`, command, checksum, then 10 bytes. Requests pad
  unused bytes with `FF`.
- Log page, 261 bytes: `47 31`, checksum, `00 00`, then 256 bytes of log.

Commands seen in the download, in order:

| Command | Purpose | Status |
| --- | --- | --- |
| `12` | Link version check, returns `V7.006` | Seen, polled about once a second |
| `01` | Poll that follows `12` | Purpose unknown |
| `D3`, `D5`, `05`, `09`, `D1`, `D4` | Connection setup, returns `GL00136` | Purpose unknown |
| `06`, `0E` | Block reads before the log list | Purpose unknown, may not be needed |
| `32` | Prepare the session list | Purpose unknown |
| `D8` | Read one session list entry | Bytes 5-6 are session number, 7-9 are size in pages |
| `33`, `D9`, `D7` | Select a session. `D7` carries the number | Exact role of `33` and `D9` unknown |
| `31` | Read the selected session | Sent with block numbers 0 up to pages + 3: 8 requests for 4 pages, 14 for 10 pages |

Log format:

- Pages join into one stream. Each record is: length byte, body, checksum byte.
  A zero byte is padding at the end of a page.
- The first record holds every field. Later records hold only what changed, so
  an empty record means the row repeats the one before it.
- Body starts with a change mask. If the first mask byte has its top bit set,
  its low 7 bits are the high byte and a low byte follows. Otherwise it is the
  high byte alone.
- Values follow in bit order, lowest bit first.

| Bit | Channel | Formula | Range proven in the captures |
| --- | --- | --- | --- |
| 0 | Voltage | raw / 8 | 5.25 to 7.75 |
| 1 | Ripple | raw / 8 | 0 to 0.125 |
| 2 | Current | raw x 3 | 0 to 18 |
| 3 | Temperature | raw is deg C; CSV is raw x 1.8 + 32 | 77 to 78.8 |
| 4 | Throttle | 0.5 + raw / 127.6 | 1.5266 to 1.7774 |
| 5 | Power-Out | raw x 100 / 255 | 0 to 17.647 |
| 6 and 7 | Speed | (bit 6 x 256 + bit 7) x 10 | 0 to 41950 |
| 8 | Gov. Gain | raw | 203 to 204 |
| 9 | BEC Voltage | raw x 0.0404 | 5.45 to 5.62 |
| 10 | Motor Temp. | raw x 1.8 + 32 | 392 only |
| 11 | Acceleration | raw / 16 | 0 to 0.13 |
| 12 | Motor Timing | raw, by elimination | 0 only |

Every value is one byte. Speed is the only channel that uses two slots.

## What is not verified

- The formulas above the ranges in the table. Neither log is a hard pass:
  the highest power seen is 17.6% and the highest current 18. The formulas may
  hold, but that is a prediction until a bigger log is compared to its CSV.
- Bit 12 being Motor Timing. It was always 0.
- Motor Temp. at any value but 200.
- Whether the `06` and `0E` reads are needed for a clean download. That needs
  a test against the ESC.
- Whether the Castle Link board or the ESC stores the log.
- The second serial number on COM13 being a second Castle Link.

Sample bytes and the matching CSV are in `Tests/castle-link-samples/`. Raw
captures stay out of git because they record every USB device on the PC,
including the keyboard.

## Steps

1. Identify how the Castle Link appears in Windows. Done.
2. Capture a full log download. Use `tools/castle-link/Capture Castle download.bat`.
   Each run goes in its own folder under `Tests/castle-link-captures/`, with the
   CSV exported from the same log so the decoded bytes can be checked against it.
   Done, two captures.
3. Decode the request and response sequence and the log data format. Done for
   the range seen; see the unverified list.
4. Add a "Get Castle log" button to DragOverlay.
5. Auto-name the file and line it up with the matching logger pass.
