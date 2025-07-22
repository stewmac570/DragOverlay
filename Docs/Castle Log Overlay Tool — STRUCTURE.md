Castle Log Overlay Tool — STRUCTURE.md

✅ Purpose
Defines the recommended folder \& file layout for the Castle Log Overlay Tool.
Keeps your code modular, easy to test in small phases, and avoids “God files”.
Matches the Castle Link 2 logic flow but uses WinForms for simplicity.



🗂️ Project Root

```bash

CastleLogOverlayTool/

├─ /docs/

│  ├─ FEATURES.md

│  ├─ DELIVERY\_PLAN.md

│  ├─ STRUCTURE.md

│  ├─ DEVELOPMENT\_LOG.md

├─ /config/

│  ├─ config.json              # Stores default channels, alignment threshold, user prefs

├─ /logs/

│  ├─ example\_run1.csv         # Real Castle logs for test

│  ├─ example\_run2.csv

├─ /src/

│  ├─ CastleLogOverlayTool.sln         # VS Solution

│  ├─ CastleLogOverlayTool/            # Main WinForms project folder

│  │  ├─ Program.cs                    # Entry point

│  │  ├─ MainForm.cs                   # Main Form logic

│  │  ├─ MainForm.Designer.cs         # Auto-generated UI designer

│  │  ├─ MainForm.resx                # Form resources

│  │  ├─ Controls/                    # Custom user controls (toggle bar, legend)

│  │  │  ├─ ChannelTogglePanel.cs

│  │  │  ├─ LegendControl.cs

│  │  ├─ Models/                      # Raw data structures

│  │  │  ├─ RunData.cs

│  │  │  ├─ DataPoint.cs

│  │  │  ├─ Config.cs

│  │  ├─ Services/                    # Logic layer

│  │  │  ├─ CsvLoader.cs             # Castle log parser

│  │  │  ├─ AlignmentService.cs      # Launch point logic

│  │  │  ├─ ConfigService.cs         # Reads/writes config.json

│  │  ├─ Utils/                       # Helpers

│  │  │  ├─ ColorMap.cs              # Castle color mapping

│  │  │  ├─ LineStyleHelper.cs       # Solid/dash/dot per run

│  │  │  ├─ Logger.cs                # Central logging system

│  │  ├─ Plot/                        # ScottPlot-specific setup

│  │  │  ├─ PlotManager.cs

│  │  │  ├─ CursorHandler.cs

│  │  │  ├─ ZoomHandler.cs

│  │  ├─ RaceBox/                     # RaceBox GPS overlay (Phase 7)

│  │  │  ├─ RaceBoxLoader.cs         # Parses RaceBox CSV format



✅ Key Folders



📁 Controls/  

Reusable UI panels:  

\- Channel toggle panel (Castle-style)  

\- Legend and layout controls  



📁 Models/  

Pure C# data structures:  

\- RunData — one run’s metadata \& Castle sample points  

\- DataPoint — one row from Castle CSV  

\- Config — mirrors config.json user settings  

\- (planned) RaceBoxData — one row from RaceBox GPS file  



📁 Services/  

Logic for parsing and state management:  

\- CsvLoader — parses Castle ESC logs  

\- RaceBoxLoader — parses RaceBox GPS logs  

\- AlignmentService — detects launch point (t=0)  

\- ConfigService — loads/saves config.json  



📁 Utils/  

Helper utilities:  

\- ColorMap — Castle channel → color  

\- LineStyleHelper — solid/dash/dot rules per log  

\- Logger — centralized debug logging to AppData  



📁 Plot/  

Handles all ScottPlot chart setup:  

\- PlotManager — manages series, axes, redraw  

\- CursorHandler — dashed hover line  

\- ZoomHandler — X-axis zoom, crop logic  



📁 RaceBox/  

New GPS overlay module:  

\- RaceBoxLoader — reads RaceBox .csv  

\- (future) RaceBoxData — structured sample rows  





✅ Config Folder  

`config.json` stores user preferences:  

\- Default channel visibility (ON/OFF per channel)  

\- Launch point detection threshold (Power-Out or Throttle)  

\- RPM mode (2P / 4P toggle)  

\- EnableDebugLogging flag  

\- Build number  



⚡ No Guess Zone  

🚫 Never guess syntax or APIs.  

✅ Always verify with official ScottPlot v5 and CsvHelper docs.  

✅ Match Castle Link 2 behavior exactly — hover, color, layout.  

❌ Do not rely on fallback test folders — all validation is via tagged Git builds.



✅ Pinned ScottPlot Version  

\- ScottPlot v5.0.8 pinned — do not upgrade mid-phase  

\- CsvHelper v33.1.0  

\- Newtonsoft.Json for config  

\- Use NuGet or store .dll in `/lib/` if pinned manually





✅ Version  

STRUCTURE.md v1.1 — 2025-07-22  

Prepared by: Stewart McMillan



🏁 Why it works  

✔️ Small, clear modules  

✔️ Easy to test, debug, and commit each piece  

✔️ Matches Castle Link 2’s working separation: data → logic → plot → UI shell  

✔️ No “monster files” mixing everything  

✔️ Uses stable Git tags instead of fallback folders



