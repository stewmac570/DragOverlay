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

## What is not verified

- Everything about the download protocol: serial settings, commands, framing
  and the log data format. None of it has been seen in a capture yet.
- Whether the Castle Link board or the ESC stores the log.
- The capture script end to end. It needs admin rights, so its first real run
  is the first real test.

## Steps

1. Identify how the Castle Link appears in Windows. Done.
2. Capture a full log download. Use `tools/castle-link/Capture Castle download.bat`.
   Each run goes in its own folder under `Tests/castle-link-captures/`, with the
   CSV exported from the same log so the decoded bytes can be checked against it.
3. Decode the request and response sequence and the log data format.
4. Add a "Get Castle log" button to DragOverlay.
5. Auto-name the file and line it up with the matching logger pass.
