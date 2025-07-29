using CastleOverlayV2.Controls;
using CastleOverlayV2.Models;
using CastleOverlayV2.Models;
using CastleOverlayV2.Plot;
using CastleOverlayV2.Services;
using CastleOverlayV2.Services;
using System.Reflection;
using System.Text;


namespace CastleOverlayV2
{
    public partial class MainForm : Form
    {
        private readonly PlotManager _plotManager;

        // ✅ New: ConfigService instance for Phase 4
        private readonly ConfigService _configService;

        // ✅ Multi-run slots
        private RunData run1;
        private RunData run2;
        private RunData run3;
        private RunData run4;
        private RunData run5; 
        private RunData run6;

        // ✅ RaceBox slots (Stage 1 only uses header metadata)
        private RaceBoxData raceBox1;
        private RaceBoxData raceBox2;
        private RaceBoxData raceBox3;

        private ChannelToggleBar _channelToggleBar;

        private bool _isFourPoleMode = false;


        public MainForm(ConfigService configService)
        {
            _configService = configService;

            InitializeComponent();

            var iconStream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("CastleOverlayV2.Resources.DragOverlay.ico");

            if (iconStream != null)
            {
                this.Icon = new Icon(iconStream);
            }

            string buildNumber = _configService.GetBuildNumber();  // already implemented
            this.Text = $"DragOverlay — Build {buildNumber}";


            Logger.Log("MainForm initialized");

            Logger.Log("=======================================");
            Logger.Log($"🟢 New Session Started — {DateTime.Now}");
            Logger.Log("=======================================");


            // ✅ Disable all toggle/delete buttons at startup
            btnToggleRun1.Enabled = false;
            btnDeleteRun1.Enabled = false;


            btnToggleRun2.Enabled = false;
            btnDeleteRun2.Enabled = false;

            btnToggleRun3.Enabled = false;
            btnDeleteRun3.Enabled = false;

            // ✅ Disable RaceBox toggle/delete buttons at startup
            btnToggleRaceBox1.Enabled = false;
            btnDeleteRaceBox1.Enabled = false;

            btnToggleRaceBox2.Enabled = false;
            btnDeleteRaceBox2.Enabled = false;

            btnToggleRaceBox3.Enabled = false;
            btnDeleteRaceBox3.Enabled = false;


            // Maximize the window on startup
            this.WindowState = FormWindowState.Maximized;

            // ✅ Init ConfigService + load config
            _configService = new ConfigService();
            var config = _configService.Config;

            Logger.Log("Config loaded at startup:");
            Logger.Log("Config loaded at startup:");
            foreach (var kvp in config.ChannelVisibility)
            {
                Logger.Log($"  Channel: {kvp.Key}, Visible: {kvp.Value}");
            }


            // Wire up the PlotManager with your FormsPlot control (must match your Designer)
            _plotManager = new PlotManager(formsPlot1);
            _plotManager.ResetEmptyPlot();
            _plotManager.CursorMoved += OnCursorMoved;
            formsPlot1.Dock = DockStyle.Fill;

            var channelNames = new List<string>
{
    "RPM",
    "Throttle",
    "Voltage",
    "Current",
    "Ripple",
    "PowerOut",
    "MotorTemp",
    "ESC Temp",
    "MotorTiming",
    "Acceleration"
};

            // ✅ 2️⃣ Use config states for toggles
            var initialStates = config.ChannelVisibility ?? new Dictionary<string, bool>();

            // ✅ 3️⃣ Create ChannelToggleBar
            _channelToggleBar = new ChannelToggleBar(channelNames, initialStates);
            _channelToggleBar.ChannelVisibilityChanged += OnChannelVisibilityChanged;
            _channelToggleBar.RpmModeChanged += OnRpmModeChanged;
            Controls.Add(_channelToggleBar);

            // ✅ Inject any missing channels from config that weren't in original list
            foreach (var kvp in config.ChannelVisibility)
            {
                if (!_channelToggleBar.GetChannelStates().ContainsKey(kvp.Key))
                {
                    Logger.Log($"🧩 Adding extra config channel to toggle bar: {kvp.Key} → {kvp.Value}");
                    _channelToggleBar.AddChannel(kvp.Key, kvp.Value);
                }
            }

            // ✅ Apply saved RPM mode from config.json
            _isFourPoleMode = config.IsFourPoleMode;
            _plotManager.SetFourPoleMode(_isFourPoleMode);

        }

        /// <summary>
        /// ✅ Original single-load for fallback testing
        /// </summary>
        private async void LoadCsvButton_Click(object sender, EventArgs e)
        {
            Logger.Log("LoadCsvButton_Click started");

            using (var openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "CSV files (*.csv)|*.csv";

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    Logger.Log("CSV file selected from dialog.");
                    string filePath = openFileDialog.FileName;

                    try
                    {
                        var loader = new CsvLoader(_configService);
                        run1 = await Task.Run(() => loader.Load(filePath));
       
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"LoadCsvButton_Click ERROR: {ex.Message}");

                    }
                }
            }
        }

        /// <summary>
        /// ✅ New: Load Run 1 slot
        /// </summary>
        private async void LoadRun1Button_Click(object sender, EventArgs e)
        {
            LogClick("Load Run 1");
            Logger.Log("LoadRun1Button_Click started");

            string filePath = GetCsvFilePath();
            if (filePath == null) return;

            try
            {
                var loader = new CsvLoader(_configService);
                run1 = await Task.Run(() => loader.Load(filePath));

                if (run1 != null && run1.DataPoints.Count > 0)
                {
                    Logger.Log($"Loaded log: Run 1 - {Path.GetFileName(filePath)} - {run1.DataPoints.Count} rows");

                    // Assign the loaded run to slot 1 in the plot manager
                    _plotManager.SetRun(1, run1);

                    // Ensure run visibility is true on load for slot 1 (not 0)
                    _plotManager.SetRunVisibility(1, true);

                    PlotAllRuns();

                    // Sync toggle button text with visibility state
                    btnToggleRun1.Text = _plotManager.GetRunVisibility(1) ? "Hide" : "Show";
                    btnToggleRun1.Enabled = true;
                    btnDeleteRun1.Enabled = true;
                }
                else
                {
                    Logger.Log("Run 1 load failed or empty data.");
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"ERROR in Run1: {ex.Message}");
            }
        }




        /// <summary>
        /// ✅ New: Load Run 2 slot
        /// </summary>
        private async void LoadRun2Button_Click(object sender, EventArgs e)
        {
            LogClick("Load Run 2");
            Logger.Log("LoadRun2Button_Click started");

            string filePath = GetCsvFilePath();
            if (filePath == null) return;

            try
            {
                var loader = new CsvLoader(_configService);
                run2 = await Task.Run(() => loader.Load(filePath));

                if (run2 != null && run2.DataPoints.Count > 0)
                {
                    Logger.Log($"Loaded log: Run 2 - {Path.GetFileName(filePath)} - {run2.DataPoints.Count} rows");

                    _plotManager.SetRun(2, run2);
                    _plotManager.SetRunVisibility(2, true);

                    PlotAllRuns();

                    // ✅ Update button state
                    btnToggleRun2.Text = "Hide";
                    btnToggleRun2.Enabled = true;
                    btnDeleteRun2.Enabled = true;

                    // ✅ Refresh layout to ensure visual state updates
                    btnToggleRun2.Parent?.PerformLayout();
                    btnDeleteRun2.Parent?.PerformLayout();
                    btnToggleRun2.Refresh();
                    btnDeleteRun2.Refresh();
                }
                else
                {
                    Logger.Log("Run 2 load failed or empty data.");
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"ERROR in Run2: {ex.Message}");
            }
        }




        /// <summary>
        /// ✅ New: Load Run 3 slot
        /// </summary>
        private async void LoadRun3Button_Click(object sender, EventArgs e)
        {
            LogClick("Load Run 3");
            Logger.Log("LoadRun3Button_Click started");

            string filePath = GetCsvFilePath();
            if (filePath == null) return;

            try
            {
                var loader = new CsvLoader(_configService);
                run3 = await Task.Run(() => loader.Load(filePath));

                if (run3 != null && run3.DataPoints.Count > 0)
                {
                    Logger.Log($"Loaded log: Run 3 - {Path.GetFileName(filePath)} - {run3.DataPoints.Count} rows");

                    // Assign the loaded run to slot 3 in the plot manager
                    _plotManager.SetRun(3, run3);

                    // Ensure run visibility is true on load for slot 3
                    _plotManager.SetRunVisibility(3, true);

                    PlotAllRuns();

                    // Sync toggle button text with visibility state
                    btnToggleRun3.Text = _plotManager.GetRunVisibility(3) ? "Hide" : "Show";
                    btnToggleRun3.Enabled = true;
                    btnDeleteRun3.Enabled = true;
                }
                else
                {
                    Logger.Log("Run 3 load failed or empty data.");
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"ERROR in Run3: {ex.Message}");
            }
        }


        /// <summary>
        /// ✅ Load RaceBox CSV for Run 1 slot
        /// </summary>
        private async void LoadRaceBox1Button_Click(object sender, EventArgs e)
        {
            Logger.Log("RaceBox Load Button Clicked — Slot 1");

            var path = GetCsvFilePath();
            if (path == null) return;

            Logger.Log("Opening file picker...");
            Logger.Log($"Selected file: {path}");

            var rbData = RaceBoxLoader.LoadHeaderOnly(path);
            if (rbData.FirstCompleteRunIndex == null)
            {
                MessageBox.Show("No complete run found in this RaceBox file.");
                return;
            }

            Logger.Log($"Header loaded: {rbData.RunCount} runs found, FirstCompleteRun = {rbData.FirstCompleteRunIndex + 1}");
            Logger.Log("Parsing RaceBox telemetry for Slot 1...");

            var loader = new RaceBoxLoader();

            Logger.Log("[MainForm] Calling RaceBoxLoader.LoadTelemetry(...) now...");
            var points = await Task.Run(() =>
                loader.LoadTelemetry(path, rbData.FirstCompleteRunIndex.Value)
            );
            Logger.Log("[MainForm] LoadTelemetry() returned successfully");

            Logger.Log($"📈 RaceBox telemetry parsed: {points.Count} rows for Slot 1");

            // ✅ Record number range logging
            int firstRecord = points.First().Record;
            int lastRecord = points.Last().Record;
            Logger.Log($"📌 RaceBox Record range used for Slot 1: {firstRecord} → {lastRecord}");

            raceBox1 = rbData;
            Logger.Log($"✅ RaceBox 1 loaded. RunCount = {rbData.RunCount}, FirstCompleteRun = {rbData.FirstCompleteRunIndex + 1}");


            // === ✅ Convert RaceBox points into RunData for plotting ===
            var run = new RunData();
            run.IsRaceBox = true;
            run.SplitTimes = rbData.SplitTimes;
            Logger.Log($"✅ SplitTimes loaded: {string.Join(", ", run.SplitTimes ?? new List<double>())}");

            run.FileName = Path.GetFileName(path);

            // ✅ Store Castle-style DataPoints into per-channel dictionary
            run.Data["RaceBox Speed"] = points.Select(p => new DataPoint
            {
                Time = p.Time.TotalSeconds,
                Y = p.SpeedMph
            }).ToList();

            run.Data["RaceBox G-Force X"] = points.Select(p => new DataPoint
            {
                Time = p.Time.TotalSeconds,
                Y = p.GForceX
            }).ToList();

            // ✅ REQUIRED: Populate dummy DataPoints list so plot manager doesn't skip it
            run.DataPoints = points.Select(p => new DataPoint
            {
                Time = p.Time.TotalSeconds
            }).ToList();

            //---------------------------------------------
            Logger.Log($"✅ Run4 channels: {string.Join(", ", run.Data.Keys)}");

            // === ✅ Assign RaceBox 1 into slot 4 (paired with Castle Run1) ===
            run4 = run;

            _plotManager.SetRun(4, run); // ❗ Critical for visibility logic


            btnToggleRaceBox1.Enabled = true;
            btnDeleteRaceBox1.Enabled = true;

            Logger.Log($"✅ Run1 channels: {string.Join(", ", run.Data.Keys)}");

            // === 🆕 Add RaceBox channels to toggle bar if missing ===
            var addedChannels = false;

            if (!_channelToggleBar.GetChannelStates().ContainsKey("RaceBox Speed"))
            {
                Logger.Log("🆕 Adding RaceBox Speed to toggle bar");
                _channelToggleBar.AddChannel("RaceBox Speed", true);
                addedChannels = true;
            }
            else
            {
                Logger.Log("ℹ️ RaceBox Speed already exists in toggle bar");
            }

            if (!_channelToggleBar.GetChannelStates().ContainsKey("RaceBox G-Force X"))
            {
                Logger.Log("🆕 Adding RaceBox G-Force X to toggle bar");
                _channelToggleBar.AddChannel("RaceBox G-Force X", true);
                addedChannels = true;
            }
            else
            {
                Logger.Log("ℹ️ RaceBox G-Force X already exists in toggle bar");
            }

            // === 🧽 Force layout refresh if we added new controls
            if (addedChannels)
            {
                Logger.Log("🔄 Forcing layout refresh after adding RaceBox toggles");
                _channelToggleBar.PerformLayout();
                _channelToggleBar.Refresh();
            }

            if (run.Data.TryGetValue("RaceBox Speed", out var speedPoints))
                Logger.Log($"✅ Run1 point count (Speed): {speedPoints.Count}");

            if (run.Data.TryGetValue("RaceBox G-Force X", out var gxPoints))
                Logger.Log($"✅ Run1 point count (G-Force X): {gxPoints.Count}");

            PlotAllRuns();

        }

        private async void LoadRaceBox2Button_Click(object sender, EventArgs e)
        {
            Logger.Log("RaceBox Load Button Clicked — Slot 2");

            var path = GetCsvFilePath();
            if (path == null) return;

            var rbData = RaceBoxLoader.LoadHeaderOnly(path);
            if (rbData.FirstCompleteRunIndex == null)
            {
                MessageBox.Show("No complete run found in this RaceBox file.");
                return;
            }

            Logger.Log($"Header loaded: {rbData.RunCount} runs found, FirstCompleteRun = {rbData.FirstCompleteRunIndex + 1}");
            Logger.Log("Parsing RaceBox telemetry for Slot 2...");

            var loader = new RaceBoxLoader();
            var points = await Task.Run(() => loader.LoadTelemetry(path, rbData.FirstCompleteRunIndex.Value));

            Logger.Log($"📈 RaceBox telemetry parsed: {points.Count} rows for RaceBox2 → run5");

            raceBox2 = rbData;

            var run = new RunData();
            run.IsRaceBox = true;
            run.SplitTimes = rbData.SplitTimes;   //1 
            Logger.Log($"✅ SplitTimes loaded: {string.Join(", ", run.SplitTimes ?? new List<double>())}");
            run.FileName = Path.GetFileName(path);

            run.Data["RaceBox Speed"] = points.Select(p => new DataPoint { Time = p.Time.TotalSeconds, Y = p.SpeedMph }).ToList();
            run.Data["RaceBox G-Force X"] = points.Select(p => new DataPoint { Time = p.Time.TotalSeconds, Y = p.GForceX }).ToList();
            run.DataPoints = points.Select(p => new DataPoint { Time = p.Time.TotalSeconds }).ToList();

            run5 = run;


            // ✅ FIX: Register run + enable buttons
            _plotManager.SetRun(5, run);
            _plotManager.SetRunVisibility(5, true);
            btnToggleRaceBox2.Enabled = true;
            btnDeleteRaceBox2.Enabled = true;

            var addedChannels = false;

            if (!_channelToggleBar.GetChannelStates().ContainsKey("RaceBox Speed"))
            {
                Logger.Log("🆕 Adding RaceBox Speed to toggle bar");
                _channelToggleBar.AddChannel("RaceBox Speed", true);
                addedChannels = true;
            }

            if (!_channelToggleBar.GetChannelStates().ContainsKey("RaceBox G-Force X"))
            {
                Logger.Log("🆕 Adding RaceBox G-Force X to toggle bar");
                _channelToggleBar.AddChannel("RaceBox G-Force X", true);
                addedChannels = true;
            }

            if (addedChannels)
            {
                _channelToggleBar.PerformLayout();
                _channelToggleBar.Refresh();
            }

            PlotAllRuns();
        }



        private async void LoadRaceBox3Button_Click(object sender, EventArgs e)
        {
            Logger.Log("RaceBox Load Button Clicked — Slot 3");

            var path = GetCsvFilePath();
            if (path == null) return;

            var rbData = RaceBoxLoader.LoadHeaderOnly(path);
            if (rbData.FirstCompleteRunIndex == null)
            {
                MessageBox.Show("No complete run found in this RaceBox file.");
                return;
            }

            Logger.Log($"Header loaded: {rbData.RunCount} runs found, FirstCompleteRun = {rbData.FirstCompleteRunIndex + 1}");
            Logger.Log("Parsing RaceBox telemetry for Slot 3...");

            var loader = new RaceBoxLoader();
            var points = await Task.Run(() => loader.LoadTelemetry(path, rbData.FirstCompleteRunIndex.Value));

            Logger.Log($"📈 RaceBox telemetry parsed: {points.Count} rows for RaceBox3 → run6");

            raceBox3 = rbData;

            var run = new RunData();
            run.IsRaceBox = true;
            run.FileName = Path.GetFileName(path);
            run.SplitTimes = rbData.SplitTimes;//2

            run.Data["RaceBox Speed"] = points.Select(p => new DataPoint { Time = p.Time.TotalSeconds, Y = p.SpeedMph }).ToList();
            run.Data["RaceBox G-Force X"] = points.Select(p => new DataPoint { Time = p.Time.TotalSeconds, Y = p.GForceX }).ToList();
            run.DataPoints = points.Select(p => new DataPoint { Time = p.Time.TotalSeconds }).ToList();

            run6 = run;

            // ✅ FIX: Register run + enable buttons
            _plotManager.SetRun(6, run);
            _plotManager.SetRunVisibility(6, true);
            btnToggleRaceBox3.Enabled = true;
            btnDeleteRaceBox3.Enabled = true;

            var addedChannels = false;

            if (!_channelToggleBar.GetChannelStates().ContainsKey("RaceBox Speed"))
            {
                Logger.Log("🆕 Adding RaceBox Speed to toggle bar");
                _channelToggleBar.AddChannel("RaceBox Speed", true);
                addedChannels = true;
            }

            if (!_channelToggleBar.GetChannelStates().ContainsKey("RaceBox G-Force X"))
            {
                Logger.Log("🆕 Adding RaceBox G-Force X to toggle bar");
                _channelToggleBar.AddChannel("RaceBox G-Force X", true);
                addedChannels = true;
            }

            if (addedChannels)
            {
                _channelToggleBar.PerformLayout();
                _channelToggleBar.Refresh();
            }

            PlotAllRuns();
        }


        private string GetCsvFilePath()
{
    Logger.Log("Opening file picker...");

    using (var ofd = new OpenFileDialog())
    {
        ofd.Title = "Select RaceBox CSV file";
        ofd.Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*";

        var result = ofd.ShowDialog();
        Logger.Log($"File dialog result: {result}");

        if (result == DialogResult.OK)
        {
            Logger.Log($"Selected file: {ofd.FileName}");
            return ofd.FileName;
        }
        else
        {
            Logger.Log("No file selected.");
            return null;
        }
    }
}


        /// <summary>
        /// ✅ Helper: Collect non-null runs and plot all
        /// </summary>
        private void PlotAllRuns()
{
            Logger.Log("PlotAllRuns called with runs:");
            if (run1 != null) Logger.Log($"  Run 1: {run1.DataPoints.Count} points");
            if (run2 != null) Logger.Log($"  Run 2: {run2.DataPoints.Count} points");
            if (run3 != null) Logger.Log($"  Run 3: {run3.DataPoints.Count} points");
            if (run4 != null) Logger.Log($"  Run 4: {run4.DataPoints.Count} points");
            if (run5 != null) Logger.Log($"  Run 5: {run5.DataPoints.Count} points");
            if (run6 != null) Logger.Log($"  Run 6: {run6.DataPoints.Count} points");

            var runsToPlot = new Dictionary<int, RunData>();
            if (run1 != null) runsToPlot[1] = run1;
            if (run2 != null) runsToPlot[2] = run2;
            if (run3 != null) runsToPlot[3] = run3;
            if (run4 != null) runsToPlot[4] = run4;
            if (run5 != null) runsToPlot[5] = run5;
            if (run6 != null) runsToPlot[6] = run6;


            var visibilityMap = _channelToggleBar.GetChannelStates();

    Logger.Log("PlotAllRuns — Channel visibility map before applying:");
    foreach (var kvp in visibilityMap)
    {
        Logger.Log($"  Channel: {kvp.Key}, Visible: {kvp.Value}");
    }

    _plotManager.SetInitialChannelVisibility(visibilityMap);
    _plotManager.PlotRuns(runsToPlot);
}



        /// <summary>
        /// ✅ Toggle changed — update plot and persist to config
        /// </summary>
        private void OnChannelVisibilityChanged(string channelName, bool isVisible)
        {
            Logger.Log($"📎 Channel toggle clicked: {channelName} → {(isVisible ? "Show" : "Hide")}");

            _plotManager.SetChannelVisibility(channelName, isVisible);
            _plotManager.RefreshPlot();

            // ✅ Save updated state immediately
            _configService.SetChannelVisibility(channelName, isVisible);

            // 🔄 Force full replot to ensure sync
            Logger.Log("🔄 Forcing full replot after channel toggle");
            PlotAllRuns();
        }


        /// <summary>
        /// ✅ Hover data updates toggle bar
        /// </summary>
        private void OnCursorMoved(Dictionary<string, double?[]> valuesAtCursor)
        {
            _channelToggleBar.UpdateMousePositionValues(valuesAtCursor);
        }

        private void ToggleRun1Button_Click(object sender, EventArgs e)
        {
            LogClick("Toggle Run 1");

            bool isNowVisible = _plotManager.ToggleRunVisibility(1);
            btnToggleRun1.Text = isNowVisible ? "Hide" : "Show";
        }


        private void ToggleRun2Button_Click(object sender, EventArgs e)
        {
            LogClick("Toggle Run 2");
            bool isNowVisible = _plotManager.ToggleRunVisibility(2);
            btnToggleRun2.Text = isNowVisible ? "Hide" : "Show";
        }


        private void ToggleRun3Button_Click(object sender, EventArgs e)
        {
            LogClick("Toggle Run 3");
            bool isNowVisible = _plotManager.ToggleRunVisibility(3);
            btnToggleRun3.Text = isNowVisible ? "Hide" : "Show";
        }


        private void DeleteRun1Button_Click(object sender, EventArgs e)
        {
            LogClick("Delete Run 1");
            DeleteRun(1);
        }

        private void DeleteRun2Button_Click(object sender, EventArgs e)
        {
            LogClick("Delete Run 2");
            DeleteRun(2);
        }

        private void DeleteRun3Button_Click(object sender, EventArgs e)
        {
            LogClick("Delete Run 3");
            DeleteRun(3);
        }


        private void DeleteRun(int slot)
        {
            // Reset buttons and clear local run reference
            switch (slot)
            {
                case 1:
                    run1 = null;
                    btnLoadRun1.Enabled = true;
                    btnToggleRun1.Enabled = false;
                    btnDeleteRun1.Enabled = false;
                    btnToggleRun1.Text = "Hide";
                    break;
                case 2:
                    run2 = null;
                    btnLoadRun2.Enabled = true;
                    btnToggleRun2.Enabled = false;
                    btnDeleteRun2.Enabled = false;
                    btnToggleRun2.Text = "Hide";
                    break;
                case 3:
                    run3 = null;
                    btnLoadRun3.Enabled = true;
                    btnToggleRun3.Enabled = false;
                    btnDeleteRun3.Enabled = false;
                    btnToggleRun3.Text = "Hide";
                    break;
                case 4:
                    run4 = null;
                    btnLoadRaceBox1.Enabled = true;
                    btnToggleRaceBox1.Enabled = false;
                    btnDeleteRaceBox1.Enabled = false;
                    btnToggleRaceBox1.Text = "Hide";
                    break;
                case 5:
                    run5 = null;
                    btnLoadRaceBox2.Enabled = true;
                    btnToggleRaceBox2.Enabled = false;
                    btnDeleteRaceBox2.Enabled = false;
                    btnToggleRaceBox2.Text = "Hide";
                    break;
                case 6:
                    run6 = null;
                    btnLoadRaceBox3.Enabled = true;
                    btnToggleRaceBox3.Enabled = false;
                    btnDeleteRaceBox3.Enabled = false;
                    btnToggleRaceBox3.Text = "Hide";
                    break;
            }

            // Force hide this run if it's still considered visible
            bool isVisibleNow = _plotManager.GetRunVisibility(slot);
            if (isVisibleNow)
                _plotManager.ToggleRunVisibility(slot);

            // Rebuild run dictionary (1–6)
            var activeRuns = new Dictionary<int, RunData>();
            if (run1 != null) activeRuns[1] = run1;
            if (run2 != null) activeRuns[2] = run2;
            if (run3 != null) activeRuns[3] = run3;
            if (run4 != null) activeRuns[4] = run4;
            if (run5 != null) activeRuns[5] = run5;
            if (run6 != null) activeRuns[6] = run6;

            _plotManager.PlotRuns(activeRuns);
        }



        private void OnRpmModeChanged(bool isFourPole)
        {
            _isFourPoleMode = isFourPole;
            _plotManager.SetFourPoleMode(_isFourPoleMode);

            var runsToPlot = new Dictionary<int, RunData>();
            if (run1 != null) runsToPlot[1] = run1;
            if (run2 != null) runsToPlot[2] = run2;
            if (run3 != null) runsToPlot[3] = run3;

            _plotManager.PlotRuns(runsToPlot);

            _configService.SetRpmMode(isFourPole); // ✅ persist to config.json
        }


        private void LogClick(string buttonName)
        {
            Logger.Log($"🔘 Button clicked: {buttonName}");
        }

        private void ToggleRaceBox1Button_Click(object sender, EventArgs e)
        {
            LogClick("Toggle RaceBox 1");

            bool isVisible = _plotManager.ToggleRunVisibility(4); // Slot 4 = RaceBox 1
            btnToggleRaceBox1.Text = isVisible ? "Hide" : "Show";

            Logger.Log($"🔁 Toggled RaceBox Run 1 visibility → {(isVisible ? "Visible" : "Hidden")}");
        }

        private void DeleteRaceBox1Button_Click(object sender, EventArgs e)
        {
            LogClick("Delete RaceBox 1");
            DeleteRun(4); // ✅ Reuse Castle logic
        }
        private void ToggleRaceBox2Button_Click(object sender, EventArgs e)
        {
            LogClick("Toggle RaceBox 2");

            bool isVisible = _plotManager.ToggleRunVisibility(5); // Slot 5 = RaceBox 2
            btnToggleRaceBox2.Text = isVisible ? "Hide" : "Show";

            Logger.Log($"🔁 Toggled RaceBox Run 2 visibility → {(isVisible ? "Visible" : "Hidden")}");
        }

        private void ToggleRaceBox3Button_Click(object sender, EventArgs e)
        {
            LogClick("Toggle RaceBox 3");

            bool isVisible = _plotManager.ToggleRunVisibility(6); // Slot 6 = RaceBox 3
            btnToggleRaceBox3.Text = isVisible ? "Hide" : "Show";

            Logger.Log($"🔁 Toggled RaceBox Run 3 visibility → {(isVisible ? "Visible" : "Hidden")}");
        }

        private void DeleteRaceBox2Button_Click(object sender, EventArgs e)
        {
            LogClick("Delete RaceBox 2");
            DeleteRun(5); // Reuse shared delete logic
        }

        private void DeleteRaceBox3Button_Click(object sender, EventArgs e)
        {
            LogClick("Delete RaceBox 3");
            DeleteRun(6); // Reuse shared delete logic
        }




    }
}
