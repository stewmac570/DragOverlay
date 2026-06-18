// File: src/CastleOverlayV2/MainForm.cs
using CastleOverlayV2.Controls;
using CastleOverlayV2.Models;
using CastleOverlayV2.Plot;
using CastleOverlayV2.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CastleOverlayV2
{
    public partial class MainForm : Form
    {
        private readonly ConfigService _configService;
        private readonly PlotManager _plotManager;
        private ChannelToggleBar _channelToggleBar;
        private MainFormPresenter _presenter;

        public MainForm(ConfigService configService)
        {
            _configService = configService ?? new ConfigService();

            InitializeComponent();

            // App icon + title
            var iconStream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("CastleOverlayV2.Resources.DragOverlay.ico");
            if (iconStream != null)
                this.Icon = new Icon(iconStream);
            this.Text = $"DragOverlay — Build {_configService.GetBuildNumber()}";

            Logger.Log("MainForm initialized");
            Logger.Log($"🟢 New Session Started — {DateTime.Now}");

            // Disable per-slot action buttons until a run is loaded.
            DisableAllSlotButtons();
            for (int i = 1; i <= 3; i++)
            {
                SetSlotShiftButtonsEnabled(i, false, isRaceBox: false);
                SetSlotShiftButtonsEnabled(i, false, isRaceBox: true);
            }

            this.WindowState = FormWindowState.Maximized;

            // PlotManager needs formsPlot1, which exists after InitializeComponent().
            _plotManager = new PlotManager(formsPlot1);
            formsPlot1.Dock = DockStyle.Fill;
            _plotManager.SetupAllAxes();

            // Build canonical channel list + normalize saved visibility keys.
            var channelNames = new List<string>
            {
                "RPM", "Throttle %", "Voltage", "Current", "Ripple",
                "PowerOut", "MotorTemp", "ESC Temp", "MotorTiming", "Acceleration"
            };
            var rawStates = _configService.Config.ChannelVisibility ?? new Dictionary<string, bool>();
            var initialStates = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in rawStates)
            {
                string key = kv.Key;
                if (key.Equals("Throttle", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("Throttle %.", StringComparison.OrdinalIgnoreCase))
                    key = "Throttle %";
                initialStates[key] = kv.Value;
            }
            _configService.Config.ChannelVisibility = initialStates;
            _plotManager.SetInitialChannelVisibility(initialStates);

            _channelToggleBar = new ChannelToggleBar(channelNames, initialStates);
            Controls.Add(_channelToggleBar);

            // Inject any extra known channels found in config but missing from the default list.
            var allowed = new HashSet<string>(channelNames, StringComparer.OrdinalIgnoreCase);
            foreach (var kv in initialStates)
            {
                if (!allowed.Contains(kv.Key)) continue;
                if (!_channelToggleBar.GetChannelStates().ContainsKey(kv.Key))
                    _channelToggleBar.AddChannel(kv.Key, kv.Value);
            }

            _plotManager.ResetEmptyPlot();

            // Presenter owns all run state + business logic. It subscribes to plot/toggle events.
            _presenter = new MainFormPresenter(this, _configService, _plotManager, _channelToggleBar);

            // RunType pill: initial appearance (Drag mode, no runs loaded → not locked).
            ApplyRunTypeUI(_presenter.IsSpeedRunMode);
            UpdateRunTypeLockState();
        }

        // =====================================================================
        // Public view methods called by the Presenter
        // =====================================================================

        /// <summary>Open file picker; null if cancelled.</summary>
        public string? PickCsvFile()
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Select CSV file",
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*"
            };
            return ofd.ShowDialog() == DialogResult.OK ? ofd.FileName : null;
        }

        public void ShowError(string title, string message) =>
            MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);

        public void ShowInfo(string title, string message) =>
            MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);

        /// <summary>A run was successfully loaded into <paramref name="slot"/>. Update button text + enable controls.</summary>
        public void SetSlotLoadedUI(int slot, string loadButtonText, bool isVisible)
        {
            switch (slot)
            {
                case 1:
                    btnLoadRun1.Text = loadButtonText;
                    btnToggleRun1.Enabled = true;
                    btnDeleteRun1.Enabled = true;
                    btnToggleRun1.Text = isVisible ? "Hide" : "Show";
                    SetSlotShiftButtonsEnabled(1, true, isRaceBox: false);
                    break;
                case 2:
                    btnLoadRun2.Text = loadButtonText;
                    btnToggleRun2.Enabled = true;
                    btnDeleteRun2.Enabled = true;
                    btnToggleRun2.Text = isVisible ? "Hide" : "Show";
                    SetSlotShiftButtonsEnabled(2, true, isRaceBox: false);
                    break;
                case 3:
                    btnLoadRun3.Text = loadButtonText;
                    btnToggleRun3.Enabled = true;
                    btnDeleteRun3.Enabled = true;
                    btnToggleRun3.Text = isVisible ? "Hide" : "Show";
                    SetSlotShiftButtonsEnabled(3, true, isRaceBox: false);
                    break;
                case 4:
                    btnLoadRaceBox1.Text = loadButtonText;
                    btnToggleRaceBox1.Enabled = true;
                    btnDeleteRaceBox1.Enabled = true;
                    btnToggleRaceBox1.Text = isVisible ? "Hide" : "Show";
                    SetSlotShiftButtonsEnabled(1, true, isRaceBox: true);
                    break;
                case 5:
                    btnLoadRaceBox2.Text = loadButtonText;
                    btnToggleRaceBox2.Enabled = true;
                    btnDeleteRaceBox2.Enabled = true;
                    btnToggleRaceBox2.Text = isVisible ? "Hide" : "Show";
                    SetSlotShiftButtonsEnabled(2, true, isRaceBox: true);
                    break;
                case 6:
                    btnLoadRaceBox3.Text = loadButtonText;
                    btnToggleRaceBox3.Enabled = true;
                    btnDeleteRaceBox3.Enabled = true;
                    btnToggleRaceBox3.Text = isVisible ? "Hide" : "Show";
                    SetSlotShiftButtonsEnabled(3, true, isRaceBox: true);
                    break;
            }
        }

        /// <summary>Reset a slot's per-slot buttons to the empty/unloaded state.</summary>
        public void ResetSlotUI(int slot)
        {
            switch (slot)
            {
                case 1:
                    btnLoadRun1.Enabled = true;
                    btnLoadRun1.Text = "Load Run 1";
                    btnToggleRun1.Enabled = false;
                    btnToggleRun1.Text = "Hide";
                    btnDeleteRun1.Enabled = false;
                    SetSlotShiftButtonsEnabled(1, false, isRaceBox: false);
                    break;
                case 2:
                    btnLoadRun2.Enabled = true;
                    btnLoadRun2.Text = "Load Run 2";
                    btnToggleRun2.Enabled = false;
                    btnToggleRun2.Text = "Hide";
                    btnDeleteRun2.Enabled = false;
                    SetSlotShiftButtonsEnabled(2, false, isRaceBox: false);
                    break;
                case 3:
                    btnLoadRun3.Enabled = true;
                    btnLoadRun3.Text = "Load Run 3";
                    btnToggleRun3.Enabled = false;
                    btnToggleRun3.Text = "Hide";
                    btnDeleteRun3.Enabled = false;
                    SetSlotShiftButtonsEnabled(3, false, isRaceBox: false);
                    break;
                case 4:
                    btnLoadRaceBox1.Enabled = true;
                    btnLoadRaceBox1.Text = "Load RaceBox 1";
                    btnToggleRaceBox1.Enabled = false;
                    btnToggleRaceBox1.Text = "Hide";
                    btnDeleteRaceBox1.Enabled = false;
                    SetSlotShiftButtonsEnabled(1, false, isRaceBox: true);
                    break;
                case 5:
                    btnLoadRaceBox2.Enabled = true;
                    btnLoadRaceBox2.Text = "Load RaceBox 2";
                    btnToggleRaceBox2.Enabled = false;
                    btnToggleRaceBox2.Text = "Hide";
                    btnDeleteRaceBox2.Enabled = false;
                    SetSlotShiftButtonsEnabled(2, false, isRaceBox: true);
                    break;
                case 6:
                    btnLoadRaceBox3.Enabled = true;
                    btnLoadRaceBox3.Text = "Load RaceBox 3";
                    btnToggleRaceBox3.Enabled = false;
                    btnToggleRaceBox3.Text = "Hide";
                    btnDeleteRaceBox3.Enabled = false;
                    SetSlotShiftButtonsEnabled(3, false, isRaceBox: true);
                    break;
            }
        }

        /// <summary>Update a slot's toggle button label ("Hide"/"Show") after a visibility flip.</summary>
        public void SetSlotToggleText(int slot, bool isVisible)
        {
            string txt = isVisible ? "Hide" : "Show";
            switch (slot)
            {
                case 1: btnToggleRun1.Text = txt; break;
                case 2: btnToggleRun2.Text = txt; break;
                case 3: btnToggleRun3.Text = txt; break;
                case 4: if (!btnToggleRaceBox1.IsDisposed && btnToggleRaceBox1.Visible) btnToggleRaceBox1.Text = txt; break;
                case 5: if (!btnToggleRaceBox2.IsDisposed && btnToggleRaceBox2.Visible) btnToggleRaceBox2.Text = txt; break;
                case 6: if (!btnToggleRaceBox3.IsDisposed && btnToggleRaceBox3.Visible) btnToggleRaceBox3.Text = txt; break;
            }
        }

        /// <summary>Sync the RunType pill switch appearance with current mode + lock state.</summary>
        public void UpdateRunTypeLockState() => SyncRunTypeUI(_presenter?.IsSpeedRunMode ?? false, _presenter?.IsAnyRunLoaded ?? false);

        // =====================================================================
        // Designer-wired button handlers — all 1-line forwarders to the Presenter
        // =====================================================================

        // -- Castle load --
        private async void LoadRun1Button_Click(object sender, EventArgs e) => await _presenter.LoadCastleRunAsync(1);
        private async void LoadRun2Button_Click(object sender, EventArgs e) => await _presenter.LoadCastleRunAsync(2);
        private async void LoadRun3Button_Click(object sender, EventArgs e) => await _presenter.LoadCastleRunAsync(3);

        // -- RaceBox load (UI slots 1..3 → plot slots 4..6) --
        private async void LoadRaceBox1Button_Click(object sender, EventArgs e) => await _presenter.LoadRaceBoxRunAsync(1);
        private async void LoadRaceBox2Button_Click(object sender, EventArgs e) => await _presenter.LoadRaceBoxRunAsync(2);
        private async void LoadRaceBox3Button_Click(object sender, EventArgs e) => await _presenter.LoadRaceBoxRunAsync(3);

        // -- Castle toggle --
        private void ToggleRun1Button_Click(object sender, EventArgs e) => _presenter.ToggleRun(1);
        private void ToggleRun2Button_Click(object sender, EventArgs e) => _presenter.ToggleRun(2);
        private void ToggleRun3Button_Click(object sender, EventArgs e) => _presenter.ToggleRun(3);

        // -- RaceBox toggle --
        private void ToggleRaceBox1Button_Click(object sender, EventArgs e) => _presenter.ToggleRun(4);
        private void ToggleRaceBox2Button_Click(object sender, EventArgs e) => _presenter.ToggleRun(5);
        private void ToggleRaceBox3Button_Click(object sender, EventArgs e) => _presenter.ToggleRun(6);

        // -- Castle delete --
        private void DeleteRun1Button_Click(object sender, EventArgs e) => _presenter.DeleteRun(1);
        private void DeleteRun2Button_Click(object sender, EventArgs e) => _presenter.DeleteRun(2);
        private void DeleteRun3Button_Click(object sender, EventArgs e) => _presenter.DeleteRun(3);

        // -- RaceBox delete --
        private void DeleteRaceBox1Button_Click(object sender, EventArgs e) => _presenter.DeleteRun(4);
        private void DeleteRaceBox2Button_Click(object sender, EventArgs e) => _presenter.DeleteRun(5);
        private void DeleteRaceBox3Button_Click(object sender, EventArgs e) => _presenter.DeleteRun(6);

        // -- Castle shift --
        private void ShiftLeftRun1_Click(object sender, EventArgs e) => _presenter.ApplyShiftDirection(1, -1, ModifierKeys);
        private void ShiftRightRun1_Click(object sender, EventArgs e) => _presenter.ApplyShiftDirection(1, +1, ModifierKeys);
        private void ShiftResetRun1_Click(object sender, EventArgs e) => _presenter.ResetShift(1);
        private void ShiftLeftRun2_Click(object sender, EventArgs e) => _presenter.ApplyShiftDirection(2, -1, ModifierKeys);
        private void ShiftRightRun2_Click(object sender, EventArgs e) => _presenter.ApplyShiftDirection(2, +1, ModifierKeys);
        private void ShiftResetRun2_Click(object sender, EventArgs e) => _presenter.ResetShift(2);
        private void ShiftLeftRun3_Click(object sender, EventArgs e) => _presenter.ApplyShiftDirection(3, -1, ModifierKeys);
        private void ShiftRightRun3_Click(object sender, EventArgs e) => _presenter.ApplyShiftDirection(3, +1, ModifierKeys);
        private void ShiftResetRun3_Click(object sender, EventArgs e) => _presenter.ResetShift(3);

        // -- RaceBox shift (UI slot N maps to plot slot N+3) --
        private void ShiftLeftRB1_Click(object sender, EventArgs e) => _presenter.ApplyShiftDirection(4, -1, ModifierKeys);
        private void ShiftRightRB1_Click(object sender, EventArgs e) => _presenter.ApplyShiftDirection(4, +1, ModifierKeys);
        private void ShiftResetRB1_Click(object sender, EventArgs e) => _presenter.ResetShift(4);
        private void ShiftLeftRB2_Click(object sender, EventArgs e) => _presenter.ApplyShiftDirection(5, -1, ModifierKeys);
        private void ShiftRightRB2_Click(object sender, EventArgs e) => _presenter.ApplyShiftDirection(5, +1, ModifierKeys);
        private void ShiftResetRB2_Click(object sender, EventArgs e) => _presenter.ResetShift(5);
        private void ShiftLeftRB3_Click(object sender, EventArgs e) => _presenter.ApplyShiftDirection(6, -1, ModifierKeys);
        private void ShiftRightRB3_Click(object sender, EventArgs e) => _presenter.ApplyShiftDirection(6, +1, ModifierKeys);
        private void ShiftResetRB3_Click(object sender, EventArgs e) => _presenter.ResetShift(6);

        // -- Ellipsis-menu wrappers (Designer wires these via ConfigureMenu) --
        private void miRun1Toggle_Click(object sender, EventArgs e) => _presenter.ToggleRun(1);
        private void miRun1Remove_Click(object sender, EventArgs e) => _presenter.DeleteRun(1);
        private void miRun1Reset_Click(object sender, EventArgs e) => _presenter.ResetShift(1);
        private void miRun2Toggle_Click(object sender, EventArgs e) => _presenter.ToggleRun(2);
        private void miRun2Remove_Click(object sender, EventArgs e) => _presenter.DeleteRun(2);
        private void miRun2Reset_Click(object sender, EventArgs e) => _presenter.ResetShift(2);
        private void miRun3Toggle_Click(object sender, EventArgs e) => _presenter.ToggleRun(3);
        private void miRun3Remove_Click(object sender, EventArgs e) => _presenter.DeleteRun(3);
        private void miRun3Reset_Click(object sender, EventArgs e) => _presenter.ResetShift(3);
        private void miRB1Toggle_Click(object sender, EventArgs e) => _presenter.ToggleRun(4);
        private void miRB1Remove_Click(object sender, EventArgs e) => _presenter.DeleteRun(4);
        private void miRB1Reset_Click(object sender, EventArgs e) => _presenter.ResetShift(4);
        private void miRB2Toggle_Click(object sender, EventArgs e) => _presenter.ToggleRun(5);
        private void miRB2Remove_Click(object sender, EventArgs e) => _presenter.DeleteRun(5);
        private void miRB2Reset_Click(object sender, EventArgs e) => _presenter.ResetShift(5);
        private void miRB3Toggle_Click(object sender, EventArgs e) => _presenter.ToggleRun(6);
        private void miRB3Remove_Click(object sender, EventArgs e) => _presenter.DeleteRun(6);
        private void miRB3Reset_Click(object sender, EventArgs e) => _presenter.ResetShift(6);

        // -- RunType pill click --
        private void RunTypeSwitch_Click(object sender, EventArgs e) => _presenter.ToggleSpeedRunMode();

        // =====================================================================
        // RunType pill visuals + per-slot button enable plumbing
        //   (Touches Designer-named controls — stays in the view.)
        // =====================================================================

        // RunType colors
        private readonly Color _rtBaseBg = Color.FromArgb(235, 235, 235);

        private void SyncRunTypeUI(bool isSpeedRun, bool locked)
        {
            if (runTypeKnob != null)
                runTypeKnob.Left = isSpeedRun ? 54 : 0;

            if (runTypeSwitch != null)
                runTypeSwitch.BackColor = _rtBaseBg;

            var activeFg = Color.FromArgb(35, 35, 35);
            var inactiveFg = Color.FromArgb(140, 140, 140);

            if (runTypeDrag != null)
            {
                runTypeDrag.ForeColor = isSpeedRun ? inactiveFg : activeFg;
                runTypeDrag.Font = new Font(runTypeDrag.Font, isSpeedRun ? FontStyle.Regular : FontStyle.Bold);
            }
            if (runTypeSpeed != null)
            {
                runTypeSpeed.ForeColor = isSpeedRun ? activeFg : inactiveFg;
                runTypeSpeed.Font = new Font(runTypeSpeed.Font, isSpeedRun ? FontStyle.Bold : FontStyle.Regular);
            }

            if (runTypeSwitch != null)
                runTypeSwitch.Enabled = !locked;

            string tip = locked ? "Clear all loaded logs to change mode." : "Drag | Speed";
            var tt = new ToolTip(this.components);
            if (runTypeSwitch != null) tt.SetToolTip(runTypeSwitch, tip);
            if (runTypeKnob != null) tt.SetToolTip(runTypeKnob, tip);
            if (runTypeDrag != null) tt.SetToolTip(runTypeDrag, "Drag");
            if (runTypeSpeed != null) tt.SetToolTip(runTypeSpeed, "Speed");

            ApplyRunTypeUI(isSpeedRun);
        }

        /// <summary>Speed mode hides all RaceBox rows + Castle Run 3.</summary>
        public void ApplyRunTypeUI(bool isSpeedRun)
        {
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

        private void DisableAllSlotButtons()
        {
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
        }

        private void SetSlotShiftButtonsEnabled(int uiSlot, bool enabled, bool isRaceBox)
        {
            if (!isRaceBox)
            {
                switch (uiSlot)
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
                switch (uiSlot)
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
    }
}
