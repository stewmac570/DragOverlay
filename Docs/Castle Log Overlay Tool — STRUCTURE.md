Castle Log Overlay Tool — STRUCTURE.md

✅ Purpose
Defines the recommended folder & file layout for the Castle Log Overlay Tool.
Keeps your code modular, easy to test in small phases, and avoids “God files”.
Matches the Castle Link 2 logic flow but uses WinForms for simplicity.

🗂️ Project Root
bash
Copy
Edit
CastleLogOverlayTool/
├─ /docs/
│  ├─ FEATURES.md
│  ├─ DELIVERY_PLAN.md
│  ├─ STRUCTURE.md
│  ├─ Development_Log.md
├─ /config/
│  ├─ config.json         # Stores default channels, alignment threshold
├─ /logs/
│  ├─ example_run1.csv    # Real Castle logs for test
│  ├─ example_run2.csv
├─ /src/
│  ├─ CastleLogOverlayTool.sln  # VS Solution
│  ├─ CastleLogOverlayTool/     # Main WinForms project folder
│  │  ├─ Program.cs              # Entry point
│  │  ├─ MainForm.cs             # Main Form logic
│  │  ├─ MainForm.Designer.cs    # Auto-generated UI designer
│  │  ├─ MainForm.resx           # Form resources
│  │  ├─ Controls/               # (optional) custom user controls
│  │  │  ├─ ChannelTogglePanel.cs
│  │  │  ├─ LegendControl.cs
│  │  ├─ Models/                 # Raw data structures
│  │  │  ├─ RunData.cs
│  │  │  ├─ DataPoint.cs
│  │  │  ├─ Config.cs
│  │  ├─ Services/               # Logic layer
│  │  │  ├─ CsvLoader.cs
│  │  │  ├─ AlignmentService.cs
│  │  │  ├─ ConfigService.cs
│  │  ├─ Utils/                  # Helpers
│  │  │  ├─ ColorMap.cs
│  │  │  ├─ LineStyleHelper.cs
│  │  ├─ Plot/                   # ScottPlot-specific setup
│  │  │  ├─ PlotManager.cs
│  │  │  ├─ CursorHandler.cs
│  │  │  ├─ ZoomHandler.cs
├─ /tests/
│  ├─ WorkingMWE/                # Stable single-run plot to rollback if needed
│  │  ├─ MWE_MainForm.cs
│  │  ├─ OneRun.csv
✅ Key Folders
📁 Controls/
Reusable UI panels (e.g., sidebar channel toggles, legend).

📁 Models/
Pure C# classes for:

RunData — one run’s metadata & sample points.

DataPoint — one row from CSV.

Config — mirrors your config.json defaults.

📁 Services/
Handles:

Parsing Castle .csv logs (CsvHelper).

Auto-aligning runs to t=0 (launch point).

Reading/writing config.json.

📁 Utils/
ColorMap — channel → Castle color.

LineStyleHelper — solid/dashed/dotted styling rules.

📁 Plot/
PlotManager — sets up ScottPlot chart, series, axes.

CursorHandler — vertical dashed hover line.

ZoomHandler — X-axis zoom, crop logic.

✅ Config Folder
config.json stores:

Default channel visibility (ON/OFF).

Launch point threshold (Power-Out or Current).

✅ Tests Folder
/tests/WorkingMWE/ is your known good rollback:

Single-run CSV that loads, plots, hover works.

Working version of MainForm.cs + PlotManager.cs.

⚡ No Guess Zone
🚫 Do not guess syntax.
Always check ScottPlot v5 + CsvHelper official docs.
Use Castle Link 2 as real behavior reference.
If unsure, isolate the feature in /tests/WorkingMWE/ before adding it.

✅ Pinned ScottPlot Version
Use ScottPlot v5.X.Y pinned.
Do not swap or update until fully tested.
Store .dll in /lib/ if you don’t use NuGet.

✅ Version
STRUCTURE.md v1.0 — [YYYY-MM-DD]
Prepared by: [Your Name]

🏁 Why it works
✔️ Small, clear modules.
✔️ Easy to test + commit each piece.
✔️ Matches Castle Link 2’s working separation: data → logic → plot → UI shell.
✔️ No “monster files” mixing everything.
✔️ Pinned MWE fallback if you break it.