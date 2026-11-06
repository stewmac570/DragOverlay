# DragOverlay v1.20

Thanks for using DragOverlay! This update saves the ESC's tune with every Castle log, adds the Big logger, and lines data logs up with Castle logs on RPM.

## What's new

### 🎚️ The tune is saved with every Castle log
**Get Castle log** now also reads the tune that is on the ESC and saves it as a `.dat` next to each log, with the same name. It is attached to the run, so the **Tune** tab shows what that pass ran.

- Settings, throttle, brake, reverse and boost curves all come straight off the ESC.
- Current limiting and torque control are not stored on the ESC, so they are left out.

Tested on a Cobra 8 Ace with firmware 1.36. Other ESCs show their ID instead of their name.

### 📈 Big logger
The **From logger** button now finds the Big logger as well as the Mini Logger, lists its runs newest first and downloads them. Big logger runs show pack volts, forward accel, pitch, roll and front and rear ride height.

### 🔗 Data logs line up on RPM
A Mini Logger or Big logger run now lines up with the Castle run by matching the whole RPM trace. If either log has no RPM, it falls back to the throttle step as before.

### 🎛️ TC tab
- **Hard (pull)** and **Soft (pulse)** curves. Pick which one to edit; the other is drawn dashed. **Send to logger** sends both.
- Trim any log by hand: right-click the chart to remove data before or after a point. Ctrl+Z undoes.
- The red cursor line and readout match the Logs chart.
- Left-drag pans the chart.

## Fixes
- The channel list repaints cleanly and keeps room for long names.

## How to install

1. Download **`DragOverlay-Setup-v1.20.exe`** from the **Assets** below.
2. Run it and follow the prompts.
3. Launch **DragOverlay** from the Start menu.

> Works on Windows 10 and 11. If Windows SmartScreen shows an "unrecognized app" warning, click **More info → Run anyway**.
