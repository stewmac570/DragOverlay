// File: src/CastleOverlayV2/MainForm.cs
using CastleOverlayV2.Controls;
using CastleOverlayV2.Models;
using CastleOverlayV2.Plot;
using CastleOverlayV2.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CastleOverlayV2
{
    public partial class MainForm : Form
    {
        private readonly PlotManager _plotManager;

        // ✅ Config (preserved as-is)
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

        // 🆕 Nudge step sizes (ms) when using modifiers
        private const double SHIFT_FINE_MS = 1;     // Ctrl
        private const double SHIFT_NORMAL_MS = 100; // No modifier (only when you explicitly want ms mode)
        private const double SHIFT_COARSE_MS = 1000; // Shift

        // Menus for the "..." buttons (Run 1–3)
        private ContextMenuStrip _menuRun1;
        private ContextMenuStrip _menuRun2;
        private ContextMenuStrip _menuRun3;

        // Menus for the "..." buttons (RaceBox 1–3)
        private ContextMenuStrip _menuRB1;
        private ContextMenuStrip _menuRB2;
        private ContextMenuStrip _menuRB3;



        public MainForm(ConfigService configService)
        {
            // 0) Store config service (fallback just in case)
            _configService = configService ?? new ConfigService();

            // 1) Create all WinForms controls first
            InitializeComponent();

            // 2) Apply UI state that does not depend on PlotManager yet
            ApplyRunTypeUI();
            UpdateRunTypeLockState();

            // 3) App icon & title
            var iconStream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("CastleOverlayV2.Resources.DragOverlay.ico");
            if (iconStream != null)
                this.Icon = new Icon(iconStream);

            string buildNumber = _configService.GetBuildNumber();
            this.Text = $"DragOverlay — Build {buildNumber}";

            Logger.Log("MainForm initialized");
            Logger.Log("=======================================");
            Logger.Log($"🟢 New Session Started — {DateTime.Now}");
            Logger.Log("=======================================");

            // 4) Disable toggles/deletes at startup (Castle + RaceBox)
            btnToggleRun1.Enabled = false;
            btnDeleteRun1.Enabled = false;
            btnToggleRun2.Enabled = false;
            btnDeleteRun2.Enabled = false;
            btnToggleRun3.Enabled = false;
            btnDeleteRun3.Enabled = false;

            btnToggleRaceBox1.Enabled = false;
            btnDeleteRaceBox1.Enabled = false;
            btnToggleRaceBox2.Enabled = false;
            btnDeleteRaceBox2.Enabled = false;
            btnToggleRaceBox3.Enabled = false;
            btnDeleteRaceBox3.Enabled = false;

            // 5) Disable all shift buttons at startup
            SetShiftButtonsEnabled(1, false, isRaceBox: false);
            SetShiftButtonsEnabled(2, false, isRaceBox: false);
            SetShiftButtonsEnabled(3, false, isRaceBox: false);
            SetShiftButtonsEnabled(1, false, isRaceBox: true);
            SetShiftButtonsEnabled(2, false, isRaceBox: true);
            SetShiftButtonsEnabled(3, false, isRaceBox: true);

            // 6) Maximize on startup
            this.WindowState = FormWindowState.Maximized;

            // 7) Pull config once
            var config = _configService.Config;

            Logger.Log("Config loaded at startup:");
            if (config.ChannelVisibility != null)
            {
                foreach (var kvp in config.ChannelVisibility)
                    Logger.Log($"  Channel: {kvp.Key}, Visible: {kvp.Value}");
            }

            // 8) Create PlotManager AFTER formsPlot1 exists
            _plotManager = new Plot.PlotManager(formsPlot1);
            formsPlot1.Dock = DockStyle.Fill;

            // 9) Start in DRAG profile (Speed-Run OFF) unless your UI flips it later
            _isSpeedRunMode = false;                       // <— removed Config.RunType usage
            _plotManager.SetSpeedMode(_isSpeedRunMode);    // sets the scale profile

            // 10) Build all axes and lock Castle Y ranges
            _plotManager.SetupAllAxes();

            // 11) Channel list (canonical names)
            var channelNames = new List<string>
    {
        "RPM",
        "Throttle %",
        "Voltage",
        "Current",
        "Ripple",
        "PowerOut",
        "MotorTemp",
        "ESC Temp",
        "MotorTiming",
        "Acceleration"
    };

            // 12) Normalize saved visibility keys (map legacy/typos → canonical)
            var rawStates = config.ChannelVisibility ?? new Dictionary<string, bool>();
            var initialStates = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in rawStates)
            {
                string key = kv.Key;

                if (key.Equals("Throttle", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("Throttle %.", StringComparison.OrdinalIgnoreCase))
                    key = "Throttle %";

                initialStates[key] = kv.Value; // last-one-wins
            }

            // (optional) persist normalized map
            _configService.Config.ChannelVisibility = initialStates;
            // _configService.Save();

            // 13) Give PlotManager the initial visibility map BEFORE any runs are plotted
            _plotManager.SetInitialChannelVisibility(initialStates);

            // 14) Build the toggle bar with canonical channels and states
            _channelToggleBar = new ChannelToggleBar(channelNames, initialStates);
            _channelToggleBar.ChannelVisibilityChanged += OnChannelVisibilityChanged;
            _channelToggleBar.RpmModeChanged += OnRpmModeChanged;
            Controls.Add(_channelToggleBar);

            // 15) Inject any valid channels that exist in config but weren’t in the default list
            var allowed = new HashSet<string>(channelNames, StringComparer.OrdinalIgnoreCase);
            foreach (var kv in initialStates)
            {
                if (!allowed.Contains(kv.Key))
                    continue;
                if (!_channelToggleBar.GetChannelStates().ContainsKey(kv.Key))
                {
                    Logger.Log($"🧩 Adding extra config channel to toggle bar: {kv.Key} → {kv.Value}");
                    _channelToggleBar.AddChannel(kv.Key, kv.Value);
                }
            }

            // 16) Apply RPM 4-pole/2-pole mode last (affects RPM axis lock max)
            _isFourPoleMode = config.IsFourPoleMode;
            _plotManager.SetFourPoleMode(_isFourPoleMode);

            // 17) Start with an empty plot message and wire cursor callback
            _plotManager.ResetEmptyPlot();
            _plotManager.CursorMoved += OnCursorMoved;
        }


        private void InitializeEllipsisMenus()
        {
            // Build RUN menus → wire to existing handlers
            _menuRun1 = BuildMenu(miRun1Toggle_Click, miRun1Remove_Click, miRun1Reset_Click);
            _menuRun2 = BuildMenu(miRun2Toggle_Click, miRun2Remove_Click, miRun2Reset_Click);
            _menuRun3 = BuildMenu(miRun3Toggle_Click, miRun3Remove_Click, miRun3Reset_Click);

            // Build RACEBOX menus → wire to existing handlers
            _menuRB1 = BuildMenu(miRB1Toggle_Click, miRB1Remove_Click, miRB1Reset_Click);
            _menuRB2 = BuildMenu(miRB2Toggle_Click, miRB2Remove_Click, miRB2Reset_Click);
            _menuRB3 = BuildMenu(miRB3Toggle_Click, miRB3Remove_Click, miRB3Reset_Click);

            // Wire the “…” buttons (adjust names here if your Designer uses different ones)
            //WireMenuButton(btnMoreRun1, _menuRun1);
            //WireMenuButton(btnMoreRun2, _menuRun2);
           // WireMenuButton(btnMoreRun3, _menuRun3);

            //WireMenuButton(btnMoreRB1, _menuRB1);
            //WireMenuButton(btnMoreRB2, _menuRB2);
            //WireMenuButton(btnMoreRB3, _menuRB3);
        }

        private static ContextMenuStrip BuildMenu(EventHandler onToggle, EventHandler onDelete, EventHandler onResetShift)
        {
            var cms = new ContextMenuStrip
            {
                ShowImageMargin = false,
                AutoClose = true
            };

            // Texts match your existing intent: toggle visibility, delete run, reset SHIFT (not “view”)
            cms.Items.Add(new ToolStripMenuItem("Hide/Show", null, onToggle));
            cms.Items.Add(new ToolStripMenuItem("Delete", null, onDelete));
            cms.Items.Add(new ToolStripSeparator());
            cms.Items.Add(new ToolStripMenuItem("Reset Shift", null, onResetShift));

            return cms;
        }

        private static void WireMenuButton(Button btn, ContextMenuStrip menu)
        {
            if (btn == null || btn.IsDisposed || menu == null) return;

            // Rewire cleanly
            btn.Click -= (s, e) => { };
            btn.Click += (s, e) => ShowMenu(menu, btn);

            // Nice-to-have: keyboard menu (Shift+F10) if user focuses the button
            btn.ContextMenuStrip = menu;
        }

        private static void ShowMenu(ContextMenuStrip menu, Control anchor)
        {
            if (menu == null || anchor == null || anchor.IsDisposed) return;
            menu.Show(anchor, new System.Drawing.Point(0, anchor.Height));
        }

        /// <summary>
        /// ✅ Helper: Shorten a file name for button text
        /// </summary>
        private static string TruncateFileName(string filePath, int maxChars = 28)
        {
            string fileName = Path.GetFileName(filePath) ?? string.Empty;
            if (fileName.Length <= maxChars) return fileName;
            if (maxChars <= 3) return fileName.Substring(0, maxChars);
            return fileName.Substring(0, maxChars - 3) + "...";
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
                run1 = await Task.Run(() => loader.Load(filePath, trimForDrag: (_isSpeedRunMode == false)));

                if (run1 != null && run1.DataPoints.Count > 0)
                {
                    Logger.Log($"Loaded log: Run 1 - {Path.GetFileName(filePath)} - {run1.DataPoints.Count} rows");

                    _plotManager.SetRun(1, run1);
                    _plotManager.SetRunVisibility(1, true);

                    btnLoadRun1.Text = $"Run 1: {Path.GetFileName(filePath)}";

                    PlotAllRuns();
                    _plotManager.SetSpeedMode(_isSpeedRunMode);
                    _plotManager.SetupAllAxes();

                    _plotManager.RefreshPlot();

                    btnToggleRun1.Text = _plotManager.GetRunVisibility(1) ? "Hide" : "Show";
                    btnToggleRun1.Enabled = true;
                    btnDeleteRun1.Enabled = true;

                    SetShiftButtonsEnabled(1, true, isRaceBox: false);
                }
                else
                {
                    Logger.Log("Run 1 load failed or empty data.");
                    MessageBox.Show("This file could not be loaded.\n\nIt may not be a valid Castle log or it contains no data.",
                        "Import Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"ERROR in Run1: {ex.Message}");
                MessageBox.Show("An error occurred while loading the file.\n\n" + ex.Message,
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            UpdateRunTypeLockState();
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
                run2 = await Task.Run(() => loader.Load(filePath, trimForDrag: (_isSpeedRunMode == false)));

                if (run2 != null && run2.DataPoints.Count > 0)
                {
                    Logger.Log($"Loaded log: Run 2 - {Path.GetFileName(filePath)} - {run2.DataPoints.Count} rows");

                    _plotManager.SetRun(2, run2);
                    _plotManager.SetRunVisibility(2, true);

                    btnLoadRun2.Text = $"Run 2: {Path.GetFileName(filePath)}";

                    PlotAllRuns();
                    _plotManager.SetSpeedMode(_isSpeedRunMode);
                    _plotManager.SetupAllAxes();

                    _plotManager.RefreshPlot();

                    btnToggleRun2.Text = _plotManager.GetRunVisibility(2) ? "Hide" : "Show";
                    btnToggleRun2.Enabled = true;
                    btnDeleteRun2.Enabled = true;

                    SetShiftButtonsEnabled(2, true, isRaceBox: false);
                }
                else
                {
                    Logger.Log("Run 2 load failed or empty data.");
                    MessageBox.Show("This file could not be loaded.\n\nIt may not be a valid Castle log or it contains no data.",
                        "Import Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"ERROR in Run2: {ex.Message}");
                MessageBox.Show("An error occurred while loading the file.\n\n" + ex.Message,
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            UpdateRunTypeLockState();
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
                run3 = await Task.Run(() => loader.Load(filePath, trimForDrag: (_isSpeedRunMode == false)));

                if (run3 != null && run3.DataPoints.Count > 0)
                {
                    Logger.Log($"Loaded log: Run 3 - {Path.GetFileName(filePath)} - {run3.DataPoints.Count} rows");

                    _plotManager.SetRun(3, run3);
                    _plotManager.SetRunVisibility(3, true);

                    btnLoadRun3.Text = $"Run 3: {Path.GetFileName(filePath)}";

                    PlotAllRuns();
                    _plotManager.SetSpeedMode(_isSpeedRunMode);
                    _plotManager.SetupAllAxes();

                    _plotManager.RefreshPlot();

                    btnToggleRun3.Text = _plotManager.GetRunVisibility(3) ? "Hide" : "Show";
                    btnToggleRun3.Enabled = true;
                    btnDeleteRun3.Enabled = true;

                    SetShiftButtonsEnabled(3, true, isRaceBox: false);
                }
                else
                {
                    Logger.Log("Run 3 load failed or empty data.");
                    MessageBox.Show("This file could not be loaded.\n\nIt may not be a valid Castle log or it contains no data.",
                        "Import Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"ERROR in Run3: {ex.Message}");
                MessageBox.Show("An error occurred while loading the file.\n\n" + ex.Message,
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            UpdateRunTypeLockState();
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

            if (rbData == null)
            {
                Logger.Log("[MainForm] RaceBox header loading failed — rbData is null");
                return;
            }

            if (rbData.FirstCompleteRunIndex == null)
            {
                MessageBox.Show("No complete run found in this RaceBox file.", "Incomplete Run", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            run.SplitLabels = rbData.SplitLabels;
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

            Logger.Log($"✅ Run4 channels: {string.Join(", ", run.Data.Keys)}");

            // === ✅ Assign RaceBox 1 into slot 4 (paired with Castle Run1) ===
            run4 = run;
            _plotManager.SetRun(4, run); // ❗ Critical for visibility logic
            _plotManager.SetRunVisibility(4, true);

            // ✅ Show filename on the (now-wider) button
            btnLoadRaceBox1.Text = $"RaceBox 1: {TruncateFileName(path)}";

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

            // 🆕 Enable shift controls for RaceBox slot 1 (slot 4)
            SetShiftButtonsEnabled(1, true, isRaceBox: true);

            PlotAllRuns();

            UpdateRunTypeLockState();
        }

        private async void LoadRaceBox2Button_Click(object sender, EventArgs e)
        {
            Logger.Log("RaceBox Load Button Clicked — Slot 2");

            var path = GetCsvFilePath();
            if (path == null) return;

            var rbData = RaceBoxLoader.LoadHeaderOnly(path);

            if (rbData == null)
            {
                Logger.Log("[MainForm] LoadHeaderOnly returned null (invalid RaceBox file)");
                return;
            }

            if (rbData.FirstCompleteRunIndex == null)
            {
                MessageBox.Show("No complete run found in this RaceBox file.", "Incomplete Run", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Logger.Log($"Header loaded: {rbData.RunCount} runs found, FirstCompleteRun = {rbData.FirstCompleteRunIndex + 1}");
            Logger.Log("Parsing RaceBox telemetry for Slot 2...");

            var loader = new RaceBoxLoader();
            var points = await Task.Run(() => loader.LoadTelemetry(path, rbData.FirstCompleteRunIndex.Value));

            if (points == null || points.Count == 0)
            {
                Logger.Log("❌ RaceBox telemetry load failed or returned no points.");
                MessageBox.Show("RaceBox telemetry is empty or could not be parsed.", "Telemetry Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Logger.Log($"📈 RaceBox telemetry parsed: {points.Count} rows for RaceBox2 → run5");

            raceBox2 = rbData;

            var run = new RunData();
            run.IsRaceBox = true;
            run.SplitTimes = rbData.SplitTimes;
            run.SplitLabels = rbData.SplitLabels;
            run.FileName = Path.GetFileName(path);

            run.Data["RaceBox Speed"] = points.Select(p => new DataPoint { Time = p.Time.TotalSeconds, Y = p.SpeedMph }).ToList();
            run.Data["RaceBox G-Force X"] = points.Select(p => new DataPoint { Time = p.Time.TotalSeconds, Y = p.GForceX }).ToList();
            run.DataPoints = points.Select(p => new DataPoint { Time = p.Time.TotalSeconds }).ToList();

            run5 = run;

            // ✅ Register run + enable buttons
            _plotManager.SetRun(5, run);
            _plotManager.SetRunVisibility(5, true);

            // ✅ Show filename on the button
            btnLoadRaceBox2.Text = $"RaceBox 2: {TruncateFileName(path)}";

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

            // 🆕 Enable shift controls for RaceBox slot 2 (slot 5)
            SetShiftButtonsEnabled(2, true, isRaceBox: true);

            PlotAllRuns();

            UpdateRunTypeLockState();
        }

        private async void LoadRaceBox3Button_Click(object sender, EventArgs e)
        {
            Logger.Log("RaceBox Load Button Clicked — Slot 3");

            var path = GetCsvFilePath();
            if (path == null) return;

            var rbData = RaceBoxLoader.LoadHeaderOnly(path);
            if (rbData == null)
            {
                Logger.Log("[MainForm] RaceBox header load failed — rbData was null");
                return;
            }

            if (rbData.FirstCompleteRunIndex == null)
            {
                MessageBox.Show("No complete run found in this RaceBox file.", "Incomplete Run", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            run.SplitTimes = rbData.SplitTimes;
            run.SplitLabels = rbData.SplitLabels;

            run.Data["RaceBox Speed"] = points.Select(p => new DataPoint { Time = p.Time.TotalSeconds, Y = p.SpeedMph }).ToList();
            run.Data["RaceBox G-Force X"] = points.Select(p => new DataPoint { Time = p.Time.TotalSeconds, Y = p.GForceX }).ToList();
            run.DataPoints = points.Select(p => new DataPoint { Time = p.Time.TotalSeconds }).ToList();

            run6 = run;

            // ✅ Register run + enable buttons
            _plotManager.SetRun(6, run);
            _plotManager.SetRunVisibility(6, true);

            // ✅ Show filename on the button
            btnLoadRaceBox3.Text = $"RaceBox 3: {TruncateFileName(path)}";

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

            // 🆕 Enable shift controls for RaceBox slot 3 (slot 6)
            SetShiftButtonsEnabled(3, true, isRaceBox: true);

            PlotAllRuns();

            UpdateRunTypeLockState();
        }

        private string GetCsvFilePath()
        {
            Logger.Log("Opening file picker...");

            using (var ofd = new OpenFileDialog())
            {
                ofd.Title = "Select CSV file";
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
            if (run1 != null) Logger.Log($"  Run 1: {run1.DataPoints.Count} points, shift={run1.TimeShiftMs} ms");
            if (run2 != null) Logger.Log($"  Run 2: {run2.DataPoints.Count} points, shift={run2.TimeShiftMs} ms");
            if (run3 != null) Logger.Log($"  Run 3: {run3.DataPoints.Count} points, shift={run3.TimeShiftMs} ms");
            if (run4 != null) Logger.Log($"  Run 4: {run4.DataPoints.Count} points, shift={run4.TimeShiftMs} ms");
            if (run5 != null) Logger.Log($"  Run 5: {run5.DataPoints.Count} points, shift={run5.TimeShiftMs} ms");
            if (run6 != null) Logger.Log($"  Run 6: {run6.DataPoints.Count} points, shift={run6.TimeShiftMs} ms");

            var runsToPlot = new Dictionary<int, RunData>();
            if (run1 != null) runsToPlot[1] = run1;
            if (run2 != null) runsToPlot[2] = run2;
            if (run3 != null) runsToPlot[3] = run3;
            if (run4 != null) runsToPlot[4] = run4;
            if (run5 != null) runsToPlot[5] = run5;
            if (run6 != null) run6.IsRaceBox = true; // keep flag; slot 6 belongs to racebox
            if (run6 != null) runsToPlot[6] = run6;

            var visibilityMap = _channelToggleBar.GetChannelStates();

            Logger.Log("PlotAllRuns — Channel visibility map before applying:");
            foreach (var kvp in visibilityMap)
                Logger.Log($"  Channel: {kvp.Key}, Visible: {kvp.Value}");

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
                    btnLoadRun1.Text = "Load Run 1"; // 🔄 reset label
                    SetShiftButtonsEnabled(1, false, isRaceBox: false);
                    break;
                case 2:
                    run2 = null;
                    btnLoadRun2.Enabled = true;
                    btnToggleRun2.Enabled = false;
                    btnDeleteRun2.Enabled = false;
                    btnToggleRun2.Text = "Hide";
                    btnLoadRun2.Text = "Load Run 2"; // 🔄 reset label
                    SetShiftButtonsEnabled(2, false, isRaceBox: false);
                    break;
                case 3:
                    run3 = null;
                    btnLoadRun3.Enabled = true;
                    btnToggleRun3.Enabled = false;
                    btnDeleteRun3.Enabled = false;
                    btnToggleRun3.Text = "Hide";
                    btnLoadRun3.Text = "Load Run 3"; // 🔄 reset label
                    SetShiftButtonsEnabled(3, false, isRaceBox: false);
                    break;
                case 4:
                    run4 = null;
                    btnLoadRaceBox1.Enabled = true;
                    btnToggleRaceBox1.Enabled = false;
                    btnDeleteRaceBox1.Enabled = false;
                    btnToggleRaceBox1.Text = "Hide";
                    btnLoadRaceBox1.Text = "Load RaceBox 1"; // 🔄 reset label
                    SetShiftButtonsEnabled(1, false, isRaceBox: true);
                    break;
                case 5:
                    run5 = null;
                    btnLoadRaceBox2.Enabled = true;
                    btnToggleRaceBox2.Enabled = false;
                    btnDeleteRaceBox2.Enabled = false;
                    btnToggleRaceBox2.Text = "Hide";
                    btnLoadRaceBox2.Text = "Load RaceBox 2"; // 🔄 reset label
                    SetShiftButtonsEnabled(2, false, isRaceBox: true);
                    break;
                case 6:
                    run6 = null;
                    btnLoadRaceBox3.Enabled = true;
                    btnToggleRaceBox3.Enabled = false;
                    btnDeleteRaceBox3.Enabled = false;
                    btnToggleRaceBox3.Text = "Hide";
                    btnLoadRaceBox3.Text = "Load RaceBox 3"; // 🔄 reset label
                    SetShiftButtonsEnabled(3, false, isRaceBox: true);
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
            if (run2 != null) run2.TimeShiftMs += 0; // keep compiler happy if stripped warnings; no-op
            if (run2 != null) runsToPlot[2] = run2;
            if (run3 != null) runsToPlot[3] = run3;

            _plotManager.PlotRuns(runsToPlot);

            _configService.SetRpmMode(isFourPole); // ✅ persist to config.json
        }

        private void LogClick(string buttonName)
        {
            Logger.Log($" 🔘 Button clicked: {buttonName}");
        }

        // ================================
        // RaceBox: consolidated handlers
        // ================================
        private void ToggleRaceBox(int slot, Button toggleButtonOrNull)
        {
            LogClick($"Toggle RaceBox {slot - 3}"); // slots 4..6 → labels 1..3

            bool isVisible = _plotManager.ToggleRunVisibility(slot);

            // Only update button text if a real button was passed and it’s visible in the layout
            if (toggleButtonOrNull != null && !toggleButtonOrNull.IsDisposed && toggleButtonOrNull.Visible)
                toggleButtonOrNull.Text = isVisible ? "Hide" : "Show";

            Logger.Log($"🔁 Toggled RaceBox Run {slot - 3} visibility → {(isVisible ? "Visible" : "Hidden")}");
        }

        private void DeleteRaceBox(int slot)
        {
            LogClick($"Delete RaceBox {slot - 3}");
            DeleteRun(slot); // central delete logic handles UI + plot + UpdateRunTypeLockState()
        }

        // ==== Wrappers kept for existing event wires (buttons or menus) ====
        private void ToggleRaceBox1Button_Click(object sender, EventArgs e) => ToggleRaceBox(4, btnToggleRaceBox1);
        private void ToggleRaceBox2Button_Click(object sender, EventArgs e) => ToggleRaceBox(5, btnToggleRaceBox2);
        private void ToggleRaceBox3Button_Click(object sender, EventArgs e) => ToggleRaceBox(6, btnToggleRaceBox3);

        private void DeleteRaceBox1Button_Click(object sender, EventArgs e) => DeleteRaceBox(4);
        private void DeleteRaceBox2Button_Click(object sender, EventArgs e) => DeleteRaceBox(5);
        private void DeleteRaceBox3Button_Click(object sender, EventArgs e) => DeleteRaceBox(6);


        // ======================================================================================
        // 🆕 Time-shift helpers and handlers

        // Returns the user-requested delta for this click:
        // - Ctrl → fine ms, Shift → coarse ms
        // - No modifier → exactly one “dot” (median Δt) for this slot
        private double GetClickDeltaMs(int slot)
        {
            if ((ModifierKeys & Keys.Control) == Keys.Control) return SHIFT_FINE_MS;
            if ((ModifierKeys & Keys.Shift) == Keys.Shift) return SHIFT_COARSE_MS;

            // Default: one sample step (“one dot”)
            double stepSec = _plotManager.GetSampleStepSeconds(slot);
            if (stepSec <= 0) stepSec = 0.01; // safety
            return stepSec * 1000.0;
        }

        private void ApplyShift(ref RunData run, int slot, double deltaMs)
        {
            if (run == null) return;
            run.TimeShiftMs += deltaMs;
            Logger.Log($"⏱️ Run {slot} shift {(deltaMs >= 0 ? "+" : "")}{deltaMs} ms → total {run.TimeShiftMs} ms");
            PlotAllRuns();
        }

        private void ResetShift(ref RunData run, int slot)
        {
            if (run == null) return;
            run.TimeShiftMs = 0;
            Logger.Log($"⏱️ Run {slot} shift reset → 0 ms");
            PlotAllRuns();
        }

        private void SetShiftButtonsEnabled(int slotIndex, bool enabled, bool isRaceBox)
        {
            if (!isRaceBox)
            {
                switch (slotIndex)
                {
                    case 1:
                        btnShiftLeftRun1.Enabled = enabled;
                        btnShiftRightRun1.Enabled = enabled;
                        btnShiftResetRun1.Enabled = enabled;
                        break;
                    case 2:
                        btnShiftLeftRun2.Enabled = enabled;
                        btnShiftRightRun2.Enabled = enabled;
                        btnShiftResetRun2.Enabled = enabled;
                        break;
                    case 3:
                        btnShiftLeftRun3.Enabled = enabled;
                        btnShiftRightRun3.Enabled = enabled;
                        btnShiftResetRun3.Enabled = enabled;
                        break;
                }
            }
            else
            {
                switch (slotIndex)
                {
                    case 1:
                        btnShiftLeftRB1.Enabled = enabled;
                        btnShiftRightRB1.Enabled = enabled;
                        btnShiftResetRB1.Enabled = enabled;
                        break;
                    case 2:
                        btnShiftLeftRB2.Enabled = enabled;
                        btnShiftRightRB2.Enabled = enabled;
                        btnShiftResetRB2.Enabled = enabled;
                        break;
                    case 3:
                        btnShiftLeftRB3.Enabled = enabled;
                        btnShiftRightRB3.Enabled = enabled;
                        btnShiftResetRB3.Enabled = enabled;
                        break;
                }
            }
        }

        // --- Run 1 (Castle slot 1)
        private void ShiftLeftRun1_Click(object sender, EventArgs e) => ApplyShift(ref run1, 1, -GetClickDeltaMs(1));
        private void ShiftRightRun1_Click(object sender, EventArgs e) => ApplyShift(ref run1, 1, +GetClickDeltaMs(1));
        private void ShiftResetRun1_Click(object sender, EventArgs e) => ResetShift(ref run1, 1);

        // --- Run 2 (Castle slot 2)
        private void ShiftLeftRun2_Click(object sender, EventArgs e) => ApplyShift(ref run2, 2, -GetClickDeltaMs(2));
        private void ShiftRightRun2_Click(object sender, EventArgs e) => ApplyShift(ref run2, 2, +GetClickDeltaMs(2));
        private void ShiftResetRun2_Click(object sender, EventArgs e) => ResetShift(ref run2, 2);

        // --- Run 3 (Castle slot 3)
        private void ShiftLeftRun3_Click(object sender, EventArgs e) => ApplyShift(ref run3, 3, -GetClickDeltaMs(3));
        private void ShiftRightRun3_Click(object sender, EventArgs e) => ApplyShift(ref run3, 3, +GetClickDeltaMs(3));
        private void ShiftResetRun3_Click(object sender, EventArgs e) => ResetShift(ref run3, 3);

        // --- RaceBox 1 (slot 4, UI group 1)
        private void ShiftLeftRB1_Click(object sender, EventArgs e) => ApplyShift(ref run4, 4, -GetClickDeltaMs(4));
        private void ShiftRightRB1_Click(object sender, EventArgs e) => ApplyShift(ref run4, 4, +GetClickDeltaMs(4));
        private void ShiftResetRB1_Click(object sender, EventArgs e) => ResetShift(ref run4, 4);

        // --- RaceBox 2 (slot 5, UI group 2)
        private void ShiftLeftRB2_Click(object sender, EventArgs e) => ApplyShift(ref run5, 5, -GetClickDeltaMs(5));
        private void ShiftRightRB2_Click(object sender, EventArgs e) => ApplyShift(ref run5, 5, +GetClickDeltaMs(5));
        private void ShiftResetRB2_Click(object sender, EventArgs e) => ResetShift(ref run5, 5);

        // --- RaceBox 3 (slot 6, UI group 3)
        private void ShiftLeftRB3_Click(object sender, EventArgs e) => ApplyShift(ref run6, 6, -GetClickDeltaMs(6));
        private void ShiftRightRB3_Click(object sender, EventArgs e) => ApplyShift(ref run6, 6, +GetClickDeltaMs(6));
        private void ShiftResetRB3_Click(object sender, EventArgs e) => ResetShift(ref run6, 6);

        // Optional helper if you want to call “move one sample” directly somewhere else:
        private void ShiftRunOneSample(int slot, int direction) // direction: -1 left, +1 right
        {
            if (!_plotManager.Runs.TryGetValue(slot, out var run) || run is null)
                return;

            double stepSec = _plotManager.GetSampleStepSeconds(slot);
            int stepMs = (int)Math.Round(stepSec * 1000.0);
            run.TimeShiftMs += direction * stepMs;

            _plotManager.PlotRuns(_plotManager.Runs.ToDictionary(k => k.Key, v => v.Value));
        }

        // ================================
        // Ellipsis menu handlers (Run 1)
        // ================================
        private void miRun1Toggle_Click(object sender, EventArgs e) => ToggleRun1Button_Click(sender, e);
        private void miRun1Remove_Click(object sender, EventArgs e) => DeleteRun1Button_Click(sender, e);
        private void miRun1Reset_Click(object sender, EventArgs e) => ShiftResetRun1_Click(sender, e);

        // ================================
        // Ellipsis menu handlers (RaceBox 1)
        // ================================
        private void miRB1Toggle_Click(object sender, EventArgs e) => ToggleRaceBox1Button_Click(sender, e);
        private void miRB1Remove_Click(object sender, EventArgs e) => DeleteRaceBox1Button_Click(sender, e);
        private void miRB1Reset_Click(object sender, EventArgs e) => ShiftResetRB1_Click(sender, e);

        // ================================
        // Ellipsis menu handlers (Run 2)
        // ================================
        private void miRun2Toggle_Click(object sender, EventArgs e) => ToggleRun2Button_Click(sender, e);
        private void miRun2Remove_Click(object sender, EventArgs e) => DeleteRun2Button_Click(sender, e);
        private void miRun2Reset_Click(object sender, EventArgs e) => ShiftResetRun2_Click(sender, e);

        // ================================
        // Ellipsis menu handlers (RaceBox 2)
        // ================================
        private void miRB2Toggle_Click(object sender, EventArgs e) => ToggleRaceBox2Button_Click(sender, e);
        private void miRB2Remove_Click(object sender, EventArgs e) => DeleteRaceBox2Button_Click(sender, e);
        private void miRB2Reset_Click(object sender, EventArgs e) => ShiftResetRB2_Click(sender, e);

        // ================================
        // Ellipsis menu handlers (Run 3)
        // ================================
        private void miRun3Toggle_Click(object sender, EventArgs e) => ToggleRun3Button_Click(sender, e);
        private void miRun3Remove_Click(object sender, EventArgs e) => DeleteRun3Button_Click(sender, e);
        private void miRun3Reset_Click(object sender, EventArgs e) => ShiftResetRun3_Click(sender, e);

        // ================================
        // Ellipsis menu handlers (RaceBox 3)
        // ================================
        private void miRB3Toggle_Click(object sender, EventArgs e) => ToggleRaceBox3Button_Click(sender, e);
        private void miRB3Remove_Click(object sender, EventArgs e) => DeleteRaceBox3Button_Click(sender, e);
        private void miRB3Reset_Click(object sender, EventArgs e) => ShiftResetRB3_Click(sender, e);

        // ================================
        // Run Type UI (Drag vs Speed) — Compact Pill Switch
        // ================================
        #region RunType

        // Keep this single source of truth:
        private bool _isSpeedRunMode = false; // false=Drag, true=Speed
                                              // --- RunType colors ---
        private readonly Color _rtActiveFg = Color.FromArgb(35, 35, 35);      // near-black
        private readonly Color _rtInactiveFg = Color.FromArgb(140, 140, 140);   // grey
        private readonly Color _rtActiveBg = Color.FromArgb(225, 235, 255);   // light tint behind active
        private readonly Color _rtBaseBg = Color.FromArgb(235, 235, 235);   // pill base background

        private bool IsAnyRunLoaded()
        {
            return run1 != null || run2 != null || run3 != null ||
                   run4 != null || run5 != null || run6 != null;
        }

        /// <summary>Position/paint the pill and (re)apply layout.</summary>
        private void SyncRunTypeUI()
        {
            // slide knob (0 for Drag, 54 for Speed)
            if (runTypeKnob != null)
                runTypeKnob.Left = _isSpeedRunMode ? 54 : 0;

            // container background (neutral)
            if (runTypeSwitch != null)
                runTypeSwitch.BackColor = _rtBaseBg;

            // active vs inactive styling
            // active vs inactive styling (active = black/bold, inactive = grey/regular)
            bool speed = _isSpeedRunMode;
            var activeFg = Color.FromArgb(35, 35, 35);
            var inactiveFg = Color.FromArgb(140, 140, 140);

            if (runTypeDrag != null)
            {
                runTypeDrag.ForeColor = speed ? inactiveFg : activeFg;
                runTypeDrag.Font = new Font(runTypeDrag.Font, speed ? FontStyle.Regular : FontStyle.Bold);
            }

            if (runTypeSpeed != null)
            {
                runTypeSpeed.ForeColor = speed ? activeFg : inactiveFg;
                runTypeSpeed.Font = new Font(runTypeSpeed.Font, speed ? FontStyle.Bold : FontStyle.Regular);
            }


            // lock when any run is loaded
            bool locked = IsAnyRunLoaded();
            if (runTypeSwitch != null)
                runTypeSwitch.Enabled = !locked;

            string tip = locked
                ? "Clear all loaded logs to change mode."
                : "Drag | Speed";

            var tt = new ToolTip(this.components);
            if (runTypeSwitch != null) tt.SetToolTip(runTypeSwitch, tip);
            if (runTypeKnob != null) tt.SetToolTip(runTypeKnob, tip);
            if (runTypeDrag != null) tt.SetToolTip(runTypeDrag, "Drag");
            if (runTypeSpeed != null) tt.SetToolTip(runTypeSpeed, "Speed");

            // keep your existing visibility rules
            ApplyRunTypeUI();
        }


        /// <summary>
        /// Classic map:
        /// - Speed: hide ALL RaceBox rows (1–3) + Castle Run 3 row
        /// - Drag:  show everything
        /// </summary>
        private void ApplyRunTypeUI()
        {
            bool isSpeedRun = _isSpeedRunMode;

            // RaceBox 1
            btnLoadRaceBox1.Visible = !isSpeedRun;
            btnShiftLeftRB1.Visible = !isSpeedRun;
            btnShiftRightRB1.Visible = !isSpeedRun;
            btnMenuRB1.Visible = !isSpeedRun;

            // RaceBox 2
            btnLoadRaceBox2.Visible = !isSpeedRun;
            btnShiftLeftRB2.Visible = !isSpeedRun;
            btnShiftRightRB2.Visible = !isSpeedRun;
            btnMenuRB2.Visible = !isSpeedRun;

            // RaceBox 3
            btnLoadRaceBox3.Visible = !isSpeedRun;
            btnShiftLeftRB3.Visible = !isSpeedRun;
            btnShiftRightRB3.Visible = !isSpeedRun;
            btnMenuRB3.Visible = !isSpeedRun;

            // Castle Run 3
            btnLoadRun3.Visible = !isSpeedRun;
            btnShiftLeftRun3.Visible = !isSpeedRun;
            btnShiftRightRun3.Visible = !isSpeedRun;
            btnMenuRun3.Visible = !isSpeedRun;

            topButtonPanel?.PerformLayout();
            topButtonPanel?.Refresh();
        }

        private void UpdateRunTypeLockState()
        {
            // keep current lock behaviour; just call Sync to reflect enable/tooltip
            SyncRunTypeUI();
        }

        // Click anywhere on the pill toggles mode (unless locked)
        private void RunTypeSwitch_Click(object sender, EventArgs e)
        {
            _isSpeedRunMode = !_isSpeedRunMode;

            ApplyRunTypeUI();
            UpdateRunTypeLockState();

            PlotAllRuns();
            _plotManager.SetSpeedMode(_isSpeedRunMode);
            _plotManager.SetupAllAxes();

            _plotManager.RefreshPlot();
        }



        #endregion


    }
}
