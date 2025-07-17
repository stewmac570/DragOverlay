using CastleOverlayV2.Controls;
using CastleOverlayV2.Models;
using CastleOverlayV2.Plot;
using CastleOverlayV2.Services;
using ScottPlot.WinForms;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

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

        private ChannelToggleBar _channelToggleBar;

        public MainForm()
        {
            InitializeComponent();

            // ✅ Disable all toggle/delete buttons at startup
            btnToggleRun1.Enabled = false;
            btnDeleteRun1.Enabled = false;

            btnToggleRun2.Enabled = false;
            btnDeleteRun2.Enabled = false;

            btnToggleRun3.Enabled = false;
            btnDeleteRun3.Enabled = false;

            // Maximize the window on startup
            this.WindowState = FormWindowState.Maximized;

            // ✅ Init ConfigService + load config
            _configService = new ConfigService();
            var config = _configService.Config;

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
            Controls.Add(_channelToggleBar);
        }

        /// <summary>
        /// ✅ Original single-load for fallback testing
        /// </summary>
        private async void LoadCsvButton_Click(object sender, EventArgs e)
        {
            Console.WriteLine("=== LoadCsvButton_Click started ===");

            using (var openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "CSV files (*.csv)|*.csv";

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    Console.WriteLine("=== File selected ===");
                    string filePath = openFileDialog.FileName;

                    try
                    {
                        RunData run = await Task.Run(() => CsvLoader.Load(filePath));
                        Console.WriteLine($"=== Load() finished — points: {run.DataPoints.Count} ===");

                        _plotManager.LoadRun(run);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"=== ERROR: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// ✅ New: Load Run 1 slot
        /// </summary>
        private async void LoadRun1Button_Click(object sender, EventArgs e)
        {
            Console.WriteLine("=== LoadRun1Button_Click started ===");

            string filePath = GetCsvFilePath();
            if (filePath == null) return;

            try
            {
                run1 = await Task.Run(() => CsvLoader.Load(filePath));
                Console.WriteLine($"=== Run1 loaded — points: {run1.DataPoints.Count} ===");
                btnToggleRun1.Enabled = true;
                btnDeleteRun1.Enabled = true;
                PlotAllRuns();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"=== ERROR in Run1: {ex.Message}");
            }
        }

        /// <summary>
        /// ✅ New: Load Run 2 slot
        /// </summary>
        private async void LoadRun2Button_Click(object sender, EventArgs e)
        {
            Console.WriteLine("=== LoadRun2Button_Click started ===");

            string filePath = GetCsvFilePath();
            if (filePath == null) return;

            try
            {
                run2 = await Task.Run(() => CsvLoader.Load(filePath));
                Console.WriteLine($"=== Run2 loaded — points: {run2.DataPoints.Count} ===");
                btnToggleRun2.Enabled = true;
                btnDeleteRun2.Enabled = true;
                PlotAllRuns();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"=== ERROR in Run2: {ex.Message}");
            }
        }

        /// <summary>
        /// ✅ New: Load Run 3 slot
        /// </summary>
        private async void LoadRun3Button_Click(object sender, EventArgs e)
        {
            Console.WriteLine("=== LoadRun3Button_Click started ===");

            string filePath = GetCsvFilePath();
            if (filePath == null) return;

            try
            {
                run3 = await Task.Run(() => CsvLoader.Load(filePath));
                Console.WriteLine($"=== Run3 loaded — points: {run3.DataPoints.Count} ===");
                btnToggleRun3.Enabled = true;
                btnDeleteRun3.Enabled = true;
                PlotAllRuns();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"=== ERROR in Run3: {ex.Message}");
            }
        }

        /// <summary>
        /// ✅ Helper: OpenFileDialog logic
        /// </summary>
        private string GetCsvFilePath()
        {
            using (var openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "CSV files (*.csv)|*.csv";
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                    return openFileDialog.FileName;
            }
            return null;
        }

        /// <summary>
        /// ✅ Helper: Collect non-null runs and plot all
        /// </summary>
        private void PlotAllRuns()
        {
            var runsToPlot = new List<RunData>();
            if (run1 != null) runsToPlot.Add(run1);
            if (run2 != null) runsToPlot.Add(run2);
            if (run3 != null) runsToPlot.Add(run3);

            // ✅ Inject saved toggle states before plotting
            var visibilityMap = _channelToggleBar.GetChannelStates();
            _plotManager.SetInitialChannelVisibility(visibilityMap);

            _plotManager.PlotRuns(runsToPlot);
        }

        /// <summary>
        /// ✅ Toggle changed — update plot and persist to config
        /// </summary>
        private void OnChannelVisibilityChanged(string channelName, bool isVisible)
        {
            _plotManager.SetChannelVisibility(channelName, isVisible);
            _plotManager.RefreshPlot();

            // ✅ Save updated state immediately
            _configService.SetChannelVisibility(channelName, isVisible);
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
            bool isNowVisible = _plotManager.ToggleRunVisibility(0);
            btnToggleRun1.Text = isNowVisible ? "Hide" : "Show";
        }


        private void ToggleRun2Button_Click(object sender, EventArgs e)
        {
            ToggleRunVisibility(1, btnToggleRun2);
        }

        private void ToggleRun3Button_Click(object sender, EventArgs e)
        {
            ToggleRunVisibility(2, btnToggleRun3);
        }
        private void ToggleRunVisibility(int runIndex, Button toggleButton)
        {
            bool isVisibleNow = _plotManager.ToggleRunVisibility(runIndex);
            toggleButton.Text = isVisibleNow ? "Hide" : "Show";
        }

        private void DeleteRun1Button_Click(object sender, EventArgs e)
        {
            DeleteRun(0);
        }

        private void DeleteRun2Button_Click(object sender, EventArgs e)
        {
            DeleteRun(1);
        }

        private void DeleteRun3Button_Click(object sender, EventArgs e)
        {
            DeleteRun(2);
        }

        private void DeleteRun(int runIndex)
        {
         
            // Optional: reset button states
            switch (runIndex)
            {
                case 0:
                    run1 = null;
                    btnLoadRun1.Enabled = true;
                    btnToggleRun1.Enabled = false;
                    btnDeleteRun1.Enabled = false;
                    btnToggleRun1.Text = "Hide";
                    break;
                case 1:
                    run2 = null;
                    btnLoadRun2.Enabled = true;
                    btnToggleRun2.Enabled = false;
                    btnDeleteRun2.Enabled = false;
                    btnToggleRun2.Text = "Hide";
                    break;
                case 2:
                    run3 = null;
                    btnLoadRun3.Enabled = true;
                    btnToggleRun3.Enabled = false;
                    btnDeleteRun3.Enabled = false;
                    btnToggleRun3.Text = "Hide";
                    break;
            }


            var activeRuns = new List<RunData>();
            if (run1 != null) activeRuns.Add(run1);
            if (run2 != null) activeRuns.Add(run2);
            if (run3 != null) activeRuns.Add(run3);

            _plotManager.PlotRuns(activeRuns);

        }



    }
}
