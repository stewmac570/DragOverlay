using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using CastleOverlayV2.Models;
using CastleOverlayV2.Plot;
using CastleOverlayV2.Services;
using ScottPlot.WinForms;

namespace CastleOverlayV2
{
    public partial class MainForm : Form
    {
        private readonly PlotManager _plotManager;

        // ✅ Multi-run slots
        private RunData run1;
        private RunData run2;
        private RunData run3;

        public MainForm()
        {
            InitializeComponent();

            // Maximize the window on startup
            this.WindowState = FormWindowState.Maximized;

            // Wire up the PlotManager with your FormsPlot control (must match your Designer)
            _plotManager = new PlotManager(formsPlot1);
            formsPlot1.Dock = DockStyle.Fill;
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

            _plotManager.PlotRuns(runsToPlot);
        }
    }
}
