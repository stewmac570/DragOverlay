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

- The capture script works. First capture: 2026-11-02, a Cobra 8 Ace on
  firmware 1.36, session number 33.
- Serial settings seen in the capture: 9600 8N1.
- Every message checksum passes: all bytes in a frame add up to 0 (mod 256).
- Decoding session 33 rebuilt all 289 rows and matched the exported CSV for
  every channel that had a live value.

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
| `31` | Read the selected session | Sent with block numbers 0-7 for a 4-page session; how the count scales is unknown |

Log format:

- Pages join into one stream. Each record is: length byte, body, checksum byte.
  A zero byte is padding at the end of a page.
- The first record holds every field. Later records hold only what changed, so
  an empty record means the row repeats the one before it.
- Body starts with a change mask. If the first mask byte has its top bit set,
  its low 7 bits are the high byte and a low byte follows. Otherwise it is the
  high byte alone.
- Values follow in bit order, lowest bit first.

| Bit | Channel | Scale seen |
| --- | --- | --- |
| 0 | Voltage | raw / 8 (61 = 7.63) |
| 3 | Temperature | raw is deg C, CSV is deg F (26 = 78.8) |
| 4 | Throttle | 131 = 1.5266, 132 = 1.5344 |
| 8 | Gov. Gain | raw (203 = 203) |
| 9 | BEC Voltage | about raw x 0.0404 (137 = 5.53) |
| 10 | Motor Temp. | raw is deg C, CSV is deg F (200 = 392.00) |
| 11 | Acceleration | raw / 16 (2 = 0.13) |

## What is not verified

- Bits 1, 2, 5, 6, 7 and 12. They were zero all through session 33, so which
  channel each one carries is unknown. Expected there: Ripple, Current,
  Power-Out, Speed and Motor Timing. One bit has no expected channel yet.
- Whether any value takes more than one byte. Every value seen fitted in one,
  but session 33 was idle. Speed and Current in a real pass may not fit.
- Scales for throttle and BEC beyond the few values above.
- Whether the `06` and `0E` reads, and the block count for `31`, are needed
  for a clean download. That needs a test against the ESC.
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
   First capture done. A second capture of a real pass is needed.
3. Decode the request and response sequence and the log data format. Partly
   done; see the unverified list.
4. Add a "Get Castle log" button to DragOverlay.
5. Auto-name the file and line it up with the matching logger pass.
