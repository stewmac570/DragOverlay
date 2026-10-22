# Mini Logger: downloading runs over USB

This is the contract DragOverlay uses to find a Mini Logger on USB and pull its runs. The firmware is `firmware/mini_logger/`. `tools/download-mini-logger.ps1` is a working reference client.

## Finding the logger

1. List the COM ports whose USB device ID contains `VID_303A`. That is Espressif's USB ID, which the XIAO ESP32-S3 uses for its built-in USB serial.
2. Open each candidate at 115200 baud, 8N1. Set **DTR on and RTS off**. Raising RTS resets the board.
3. Wait about 1.5 s, discard anything already received, then send `id` followed by `\n`.
4. The logger replies `MINILOGGER 2`. Ignore any port that doesn't reply within 2 s.

Other Espressif boards share the same USB ID, so the `id` reply is what proves it's a logger.

## Commands

Send each command as one line ending in `\n`. Replies are lines ending in `\n`. Strip any trailing `\r`.

| Command | Reply |
|---|---|
| `id` | `MINILOGGER 2` |
| `list` | One line per saved run, `Run N: S KB`, or `No runs saved`, then `END` |
| `dump N` | `BEGIN run_NNNN.csv`, the CSV header, one line per reading, then `END`. If run N doesn't exist: `Run N not found` |
| `dumpall` | A `dump` block for every saved run, then `DONE` |
| `erase all` | `All saved runs erased` |
| `status` | One human-readable line. Don't parse it |

- The logger keeps recording while you talk to it. The run being recorded right now, which is this USB session, is never listed, dumped or erased.
- Lines that don't match the expected reply can arrive at any time, for example the startup banner. Skip them while you wait for `BEGIN`, `END` or `DONE`.
- Run numbers start again from 1 after `erase all`, so the same name can mean a different run later. Save each download into its own folder, as the reference script does (`Mini Logger Runs\<yyyy-MM-dd_HHmm>`), or rename the files on save.
- Don't erase automatically. Leave erasing as a separate, confirmed action.

## CSV format

```
Time (s),Throttle (ms),Motor RPM,Wheel (rev/s)
0.000,0.000,0,0.00
0.001,1.520,15528,0.00
```

- 1000 readings a second. Time starts at 0 at power-on.
- Throttle is the receiver pulse width in ms. 0 means no signal.
- Motor RPM is real motor RPM, already divided by the pole pairs. Don't apply the 4-pole halving to it.
- Wheel speed is front wheel revolutions per second. mph = rev/s × π × tyre diameter in metres × 2.23694. The tyre diameter belongs to the car, so make it a setting. Jayfab's front tyre is 70 mm.
- Runs saved by the older trigger-based firmware (runs before 21 Oct 2026) come out in the same format.

## Size and time

A run is about 1100 events per second at 30000 rpm on the bench. A 15-minute run is about 900,000 CSV lines, roughly 25 MB. A long download hasn't been timed yet. Show progress by counting lines against `Time (s)`, and use a read timeout of at least 15 s.

## Safety

The car's BEC can feed back up the USB cable into the PC if the logger is connected to the receiver with the car switched on. Before connecting, the overlay should tell the user to **switch the car off and unplug the logger's receiver lead**.
