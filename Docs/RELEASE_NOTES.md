# DragOverlay v1.19

Thanks for using DragOverlay! This update pulls Castle logs straight off the ESC and adds split charts, Mini Logger downloads and a TC tab.

## What's new

### ⤓ Get Castle log
Plug the Castle Link into the ESC and the PC, then press **Get Castle log** on the Logs page. You don't need the Castle app.

- Pick from the logs on the ESC. The newest is ticked for you.
- The log is named, saved in your Castle log folder and loaded.
- Close Castle Link 2 first, because only one program can use the Castle Link at a time.

Tested on a Cobra 8 Ace with firmware 1.36. Other ESCs have not been tested.

### 📊 Split view
The chart can split into 1 to 3 stacked panes that share one time axis, each with its own scales. Set it from the **View** menu, or sort the panes by source.

### 🔌 Mini Logger over USB
**From logger** finds the Mini Logger on USB and downloads its saved runs. Car gearing and slip channels are also new, and the front wheel size and FDR are in the car settings.

### 🎛️ TC tab
A new TC tab loads TC logger runs, shows the RPM curve and lets you edit Castle TC curves on the chart. It has its own **Load log** button, and it uses its own logs.

### ✂️ Trimming
- **Auto trim** has an on/off button in the top bar.
- **Undo the last trim** from the chart menu or with Ctrl+Z.

### Smaller changes
- Each tab shows only its own buttons.
- Data logs have their own folder setting.

## Fixes
- Castle RPM at the cursor no longer halves twice in 4-pole mode.
- Bench-test Castle logs now line up.
- Mini Logger files that kept their BEGIN and END lines now load.
- The chart no longer shows side scales.
- The TC chart zooms in time only, like the Logs chart.

## How to install

1. Download **`DragOverlay-Setup-v1.19.exe`** from the **Assets** below.
2. Run it and follow the prompts.
3. Launch **DragOverlay** from the Start menu.

> Works on Windows 10 and 11. If Windows SmartScreen shows an "unrecognized app" warning, click **More info → Run anyway**.
