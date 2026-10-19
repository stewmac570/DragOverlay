# DragOverlay v1.18

Thanks for using DragOverlay! This update adds RT Pro GPS logs and gives data logs their own buttons.

## What's new

### 📡 RT Pro GPS logs
RT Pro telemetry files (`RTProTree_..._telemetry.csv`) now load just like RaceBox logs:

- They show on the **GPS Speed, Distance and G-Force** channels.
- **66 ft and 132 ft split lines** are worked out from the speed.
- A file holds a whole session, so DragOverlay picks your **fastest pass** and starts the clock at the standstill just before it.

### 🔘 GPS and Data Log buttons
The buttons along the top are now three groups:

- **Load Castle 1–3** for Castle ESC logs.
- **Load GPS 1–3** for RaceBox and RT Pro logs.
- **Load Data Log 1–3** for Mini Logger logs, which now have their own slots.

Pick a file with the wrong button and it goes into the right group anyway. Projects saved with an earlier version still open.

### ↔️ Buttons fit the window
The buttons shrink and grow with the window, so all nine always fit on one row. Long file names end in "…".

## Fixes
- Projects that include an RT Pro GPS run now reopen.

## How to install

1. Download **`DragOverlay-Setup-v1.18.exe`** from the **Assets** below.
2. Run it and follow the prompts.
3. Launch **DragOverlay** from the Start menu.

> Works on Windows 10 and 11. If Windows SmartScreen shows an "unrecognized app" warning, click **More info → Run anyway**.
