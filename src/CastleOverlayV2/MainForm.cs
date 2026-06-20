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
    public partial class MainForm : Form, IMainView
    {
        private readonly ConfigService _configService;
        private readonly PlotManager _plotManager;
        private ChannelDrawer _channelDrawer;
        private RunStrip _runStrip;
        private AlignmentBar _alignmentBar;
        private TunePanel _tunePanel;
        private MainFormPresenter _presenter;

        public MainForm(ConfigService configService)
        {
            _configService = configService ?? new ConfigService();

            InitializeComponent();
            HandleCreated += (_, _) => Utils.WindowsDarkTitleBar.Apply(Handle);

            // Dark theme — surface.window (chrome around the plot).
            // Phase 1 minimum: form + plot are dark; the run strip / drawer get full
            // theme treatment in their own phases.
            this.BackColor = Color.FromArgb(0x13, 0x17, 0x1E);
            this.ForeColor = Color.FromArgb(0xE6, 0xE9, 0xEF);

            // App icon + title
            var iconStream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("CastleOverlayV2.Resources.DragOverlay.ico");
            if (iconStream != null)
                this.Icon = new Icon(iconStream);
            this.Text = $"DragOverlay — Build {_configService.GetBuildNumber()}";

            Logger.Log("MainForm initialized");
            Logger.Log($"🟢 New Session Started — {DateTime.Now}");

            this.WindowState = FormWindowState.Maximized;
            this.KeyPreview = true;

            // PlotManager needs formsPlot1, which exists after InitializeComponent().
            _plotManager = new PlotManager(formsPlot1);
            formsPlot1.Dock = DockStyle.Fill;
            _plotManager.SetupAllAxes();
            _plotManager.SetVoltageSmoothing(
                _configService.Config.VoltageSmoothingEnabled,
                _configService.Config.VoltageSmoothingWindow);

            // Build canonical channel list + normalize saved visibility keys.
            var channelNames = new List<string>
            {
                "RPM", "Throttle %", "Voltage", "Current", "Ripple",
                "PowerOut", "MotorTemp", "ESC Temp", "MotorTiming", "Acceleration"
            };
            var rawStates = _configService.Config.ChannelVisibility ?? new Dictionary<string, bool>();
            var initialStates = new Dictionary<string, bool>(rawStates, StringComparer.OrdinalIgnoreCase);
            _configService.Config.ChannelVisibility = initialStates;
            _plotManager.SetInitialChannelVisibility(initialStates);

            _channelDrawer = new ChannelDrawer(channelNames, initialStates);
            Controls.Add(_channelDrawer);

            _tunePanel = new TunePanel();
            Controls.Add(_tunePanel);

            // Inject any extra known channels found in config but missing from the default list.
            var allowed = new HashSet<string>(channelNames, StringComparer.OrdinalIgnoreCase);
            foreach (var kv in initialStates)
            {
                if (!allowed.Contains(kv.Key)) continue;
                if (!_channelDrawer.GetChannelStates().ContainsKey(kv.Key))
                    _channelDrawer.AddChannel(kv.Key, kv.Value);
            }

            _plotManager.ResetEmptyPlot();

            // Phase 4: the legacy top button panel is replaced by the new RunStrip
            // (chip-per-slot). The Designer-named buttons inside still exist but are
            // hidden via topButtonPanel.Visible = false; the runTypeSwitch (Drag/Speed
            // pill) lives in headerRow's column 1 and stays visible.
            topButtonPanel.Visible = false;
            // Dark-theme every container the Designer leaves at default light gray, so
            // there are no white strips behind the run strip, plot, or pill.
            var surfaceBar    = Color.FromArgb(0x1B, 0x21, 0x2B);
            var surfaceWindow = Color.FromArgb(0x13, 0x17, 0x1E);
            mainContainer.BackColor = surfaceWindow;
            headerRow.BackColor = surfaceBar;
            topButtonPanel.BackColor = surfaceBar;

            _runStrip = new RunStrip();
            Controls.Add(_runStrip); // Dock = Top
            _runStrip.LoadRequested += slot =>
            {
                if (slot <= 3) _ = _presenter.LoadCastleRunAsync(slot);
                else _ = _presenter.LoadRaceBoxRunAsync(slot - 3);
            };
            _runStrip.ToggleRequested += slot => _presenter.ToggleRun(slot);
            _runStrip.DeleteRequested += slot => _presenter.DeleteRun(slot);
            _runStrip.ArmRequested += slot => _presenter.ToggleAlignmentArm(slot);
            _runStrip.SettingsRequested += ShowSettingsDialog;

            _alignmentBar = new AlignmentBar();
            Controls.Add(_alignmentBar);
            _alignmentBar.AutoRequested += () => _presenter.AutoAlignArmedRun();
            _alignmentBar.FineNudgeRequested += direction =>
                _presenter.NudgeArmedRun(direction, coarse: false);
            _alignmentBar.CoarseNudgeRequested += direction =>
                _presenter.NudgeArmedRun(direction, coarse: true);
            _alignmentBar.ResetRequested += () => _presenter.ResetArmedRun();
            _alignmentBar.CloseRequested += () => _presenter.DisarmAlignment();

            Controls.SetChildIndex(_runStrip, 0);
            Controls.SetChildIndex(_alignmentBar, 1);

            // Presenter owns all run state + business logic. It subscribes to plot/toggle events.
            _presenter = new MainFormPresenter(this, _configService, _plotManager, _channelDrawer, _tunePanel);

            // RunType pill: initial appearance (Drag mode, no runs loaded → not locked).
            ApplyRunTypeUI(_presenter.IsSpeedRunMode);
            UpdateRunTypeLockState();
        }

        // =====================================================================
        // Public view methods called by the Presenter
        // =====================================================================

        public string? PickCsvFile() => PickCastleCsvFile();

        public string? PickCastleCsvFile()
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Select Castle CSV file",
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                InitialDirectory = ExistingDirOrEmpty(_configService.Config.CastleLogDirectory)
            };
            return ofd.ShowDialog() == DialogResult.OK ? ofd.FileName : null;
        }

        public string? PickRaceBoxCsvFile()
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Select RaceBox CSV file",
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                InitialDirectory = ExistingDirOrEmpty(_configService.Config.RaceBoxLogDirectory)
            };
            return ofd.ShowDialog() == DialogResult.OK ? ofd.FileName : null;
        }

        public string? PickTuneFile()
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Select Castle Link tune file",
                Filter = "Castle tune files (*.dat)|*.dat|All files (*.*)|*.*",
                InitialDirectory = ExistingDirOrEmpty(_configService.Config.TuneDirectory)
            };
            return ofd.ShowDialog() == DialogResult.OK ? ofd.FileName : null;
        }

        private static string ExistingDirOrEmpty(string? path) =>
            !string.IsNullOrWhiteSpace(path) && Directory.Exists(path) ? path : "";

        public string? PickProjectFileToOpen()
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Open DragOverlay project",
                Filter = "DragOverlay project (*.dragoverlay)|*.dragoverlay|All files (*.*)|*.*"
            };
            return ofd.ShowDialog() == DialogResult.OK ? ofd.FileName : null;
        }

        public string? PickProjectFileToSave()
        {
            using var sfd = new SaveFileDialog
            {
                Title = "Save DragOverlay project",
                Filter = "DragOverlay project (*.dragoverlay)|*.dragoverlay",
                DefaultExt = "dragoverlay",
                AddExtension = true,
                OverwritePrompt = true
            };
            return sfd.ShowDialog() == DialogResult.OK ? sfd.FileName : null;
        }

        public void ShowSettingsDialog()
        {
            using var dlg = new SettingsForm(_configService);
            if (dlg.ShowDialog(this) == DialogResult.OK)
                _plotManager?.SetVoltageSmoothing(
                    _configService.Config.VoltageSmoothingEnabled,
                    _configService.Config.VoltageSmoothingWindow);
        }

        public void ShowError(string title, string message) =>
            MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);

        public void ShowInfo(string title, string message) =>
            MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);

        /// <summary>A run was successfully loaded into <paramref name="slot"/>. Updates the run-strip chip.</summary>
        public void SetSlotLoadedUI(int slot, string fullPath, bool isVisible) =>
            _runStrip.SetSlotLoaded(slot, fullPath, isVisible);

        /// <summary>Reset a slot's chip back to the empty (Load …) state.</summary>
        public void ResetSlotUI(int slot) => _runStrip.SetSlotEmpty(slot);

        /// <summary>Update a slot's master eye toggle after a visibility flip.</summary>
        public void SetSlotToggleText(int slot, bool isVisible) =>
            _runStrip.SetSlotToggleState(slot, isVisible);

        public void SetSlotArmedUI(int slot, bool armed) =>
            _runStrip.SetSlotArmed(slot, armed);

        public void ShowAlignmentUI(string fileName, double offsetMs) =>
            _alignmentBar.ShowFor(fileName, offsetMs);

        public void SetAlignmentOffsetUI(double offsetMs) =>
            _alignmentBar.SetOffset(offsetMs);

        public void HideAlignmentUI() => _alignmentBar.HideBar();

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

        // RunType colors (dark theme — match the rest of the app).
        private readonly Color _rtBaseBg = Color.FromArgb(0x1A, 0x1F, 0x27);   // surface.card
        private readonly Color _rtKnobBg = Color.FromArgb(0x17, 0x22, 0x30);   // surface.card.focus

        private void SyncRunTypeUI(bool isSpeedRun, bool locked)
        {
            if (runTypeKnob != null)
            {
                runTypeKnob.Left = isSpeedRun ? 54 : 0;
                runTypeKnob.BackColor = _rtKnobBg;
            }

            if (runTypeSwitch != null)
                runTypeSwitch.BackColor = _rtBaseBg;

            var activeFg = Color.FromArgb(0xE6, 0xE9, 0xEF);    // text.primary
            var inactiveFg = Color.FromArgb(0x5C, 0x65, 0x73);  // text.dim

            if (runTypeDrag != null)
            {
                runTypeDrag.ForeColor = isSpeedRun ? inactiveFg : activeFg;
                runTypeDrag.BackColor = _rtBaseBg;
                runTypeDrag.Font = new Font(runTypeDrag.Font, isSpeedRun ? FontStyle.Regular : FontStyle.Bold);
            }
            if (runTypeSpeed != null)
            {
                runTypeSpeed.ForeColor = isSpeedRun ? activeFg : inactiveFg;
                runTypeSpeed.BackColor = _rtBaseBg;
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

        /// <summary>Speed mode hides Castle Run 3 + all RaceBox chips in the run strip.</summary>
        public void ApplyRunTypeUI(bool isSpeedRun)
        {
            // Castle Run 3 + all RaceBox slots hide in Speed-Run mode.
            _runStrip?.SetSlotVisible(3, !isSpeedRun);
            _runStrip?.SetSlotVisible(4, !isSpeedRun);
            _runStrip?.SetSlotVisible(5, !isSpeedRun);
            _runStrip?.SetSlotVisible(6, !isSpeedRun);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (_presenter?.IsAlignmentArmed == true)
            {
                Keys keyCode = keyData & Keys.KeyCode;
                if (keyCode == Keys.Escape)
                {
                    _presenter.DisarmAlignment();
                    return true;
                }

                if (keyCode == Keys.Left || keyCode == Keys.Right)
                {
                    int direction = keyCode == Keys.Left ? -1 : 1;
                    bool coarse = (keyData & Keys.Shift) == Keys.Shift;
                    _presenter.NudgeArmedRun(direction, coarse);
                    return true;
                }
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

    }
}
