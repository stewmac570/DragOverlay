// File: /src/MainForm.cs

using System;
using System.IO;
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
        /// Handles loading a single Castle log file.
        /// Call this from a button click or Form_Load for testing.
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

                        _plotManager.LoadRun(run);   // ✅ THIS IS THE MISSING PIECE
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"=== ERROR: {ex.Message}");
                    }
                }
            }
        }




    }
}
