using CastleOverlayV2.Models;

namespace CastleOverlayV2.Controls
{
    public sealed class TunePanel : UserControl
    {
        private const int ExpandedWidth = 460;
        private const int CollapsedWidth = 36;
        private const int MinimumExpandedWidth = 340;
        private const int MaximumExpandedWidth = 760;
        private const int ResizeGripWidth = 6;

        private readonly Panel _resizeGrip;
        private readonly Button _collapseButton;
        private readonly Panel _content;
        private readonly FlowLayoutPanel _layout;
        private readonly ComboBox _runCombo;
        private readonly Button _attachButton;
        private readonly Label _statusLabel;
        private readonly ComboBox _radioModeCombo;
        private readonly ComboBox _radioSpeedTypeCombo;
        private readonly FlowLayoutPanel _radioModeFields;
        private readonly NumericUpDown _radioAllTurn;
        private readonly NumericUpDown _radioAllReturn;
        private readonly NumericUpDown _radioHighTurn;
        private readonly NumericUpDown _radioHighReturn;
        private readonly NumericUpDown _radioPoint1;
        private readonly NumericUpDown _radioMiddleTurn;
        private readonly NumericUpDown _radioMiddleReturn;
        private readonly NumericUpDown _radioPoint2;
        private readonly NumericUpDown _radioLowTurn;
        private readonly NumericUpDown _radioLowReturn;
        private readonly Label _boostLabel;
        private readonly Label _turboLabel;
        private readonly TuneCurvePreview _throttleCurve;
        private readonly TuneCurvePreview _boostCurve;

        private readonly Dictionary<int, string> _availableRuns = new();
        private bool _isCollapsed;
        private bool _isUpdating;
        private bool _isResizing;
        private int _resizeStartMouseX;
        private int _resizeStartWidth;
        private int _lastExpandedWidth = ExpandedWidth;

        public event Action<int?>? AttachTuneRequested;
        public event Action<int, RadioTuneSettings>? RadioSettingsChanged;
        public event Action<int>? SelectedRunChanged;

        public TunePanel()
        {
            Dock = DockStyle.Right;
            Width = ExpandedWidth;
            MinimumSize = new Size(CollapsedWidth, 0);
            BackColor = Color.FromArgb(0x13, 0x17, 0x1E);
            ForeColor = Color.FromArgb(0xE6, 0xE9, 0xEF);

            _resizeGrip = new Panel
            {
                Dock = DockStyle.None,
                Width = ResizeGripWidth,
                Cursor = Cursors.VSplit,
                BackColor = Color.FromArgb(0x2C, 0x5A, 0x86)
            };
            _resizeGrip.MouseDown += ResizeGrip_MouseDown;
            _resizeGrip.MouseMove += ResizeGrip_MouseMove;
            _resizeGrip.MouseUp += ResizeGrip_MouseUp;
            Controls.Add(_resizeGrip);

            _collapseButton = new Button
            {
                Dock = DockStyle.None,
                Width = CollapsedWidth,
                Text = "Tune\n‹",
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0x1D, 0x23, 0x2D),
                ForeColor = ForeColor
            };
            _collapseButton.FlatAppearance.BorderColor = Color.FromArgb(0x33, 0x3B, 0x48);
            _collapseButton.Click += (_, _) => ToggleCollapsed();
            Controls.Add(_collapseButton);

            _content = new Panel
            {
                Dock = DockStyle.None,
                Padding = new Padding(12),
                AutoScroll = true,
                BackColor = BackColor
            };
            _content.HorizontalScroll.Enabled = false;
            _content.HorizontalScroll.Visible = false;
            Controls.Add(_content);

            _layout = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = BackColor
            };
            _content.Controls.Add(_layout);
            _content.Resize += (_, _) => ResizeContent();

            _layout.Controls.Add(CreateTitle("Tune"));

            var attachRow = new TableLayoutPanel
            {
                ColumnCount = 2,
                RowCount = 1,
                Width = 360,
                Height = 34,
                Margin = new Padding(0, 8, 0, 6),
                BackColor = BackColor
            };
            attachRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            attachRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            _runCombo = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(0x20, 0x26, 0x31),
                ForeColor = ForeColor,
                FlatStyle = FlatStyle.Flat
            };
            _runCombo.SelectedIndexChanged += (_, _) =>
            {
                if (!_isUpdating && TryGetSelectedSlot(out int slot))
                    SelectedRunChanged?.Invoke(slot);
            };
            _attachButton = CreateButton("Attach tune");
            _attachButton.Dock = DockStyle.Fill;
            _attachButton.Click += (_, _) => AttachTuneRequested?.Invoke(GetSelectedSlot());
            attachRow.Controls.Add(_runCombo, 0, 0);
            attachRow.Controls.Add(_attachButton, 1, 0);
            _layout.Controls.Add(attachRow);

            _statusLabel = CreateValueLabel("Load a Castle run, then attach a Castle Link .dat tune.");
            _statusLabel.Height = 26;
            _layout.Controls.Add(_statusLabel);

            _layout.Controls.Add(CreateSection("Radio throttle speed"));
            var radioHeader = CreateGrid(2);
            _radioModeCombo = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(0x20, 0x26, 0x31),
                ForeColor = ForeColor,
                FlatStyle = FlatStyle.Flat
            };
            _radioModeCombo.Items.AddRange(new object[] { "Mode 1", "Mode 2", "Mode 3" });
            _radioSpeedTypeCombo = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(0x20, 0x26, 0x31),
                ForeColor = ForeColor,
                FlatStyle = FlatStyle.Flat
            };
            _radioSpeedTypeCombo.Items.AddRange(new object[] { "Normal", "Curve" });
            AddField(radioHeader, "Mode", _radioModeCombo);
            AddField(radioHeader, "Speed type", _radioSpeedTypeCombo);
            _layout.Controls.Add(radioHeader);

            _radioModeFields = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0, 2, 0, 0),
                BackColor = BackColor
            };
            _layout.Controls.Add(_radioModeFields);

            _radioAllTurn = CreateNumeric();
            _radioAllReturn = CreateNumeric();
            _radioHighTurn = CreateNumeric();
            _radioHighReturn = CreateNumeric();
            _radioPoint1 = CreateNumeric();
            _radioMiddleTurn = CreateNumeric();
            _radioMiddleReturn = CreateNumeric();
            _radioPoint2 = CreateNumeric();
            _radioLowTurn = CreateNumeric();
            _radioLowReturn = CreateNumeric();

            _radioModeCombo.SelectedIndexChanged += (_, _) =>
            {
                RebuildRadioModeFields();
                RaiseRadioChanged();
            };
            _radioSpeedTypeCombo.SelectedIndexChanged += (_, _) => RaiseRadioChanged();
            foreach (var numeric in new[]
            {
                _radioAllTurn, _radioAllReturn,
                _radioHighTurn, _radioHighReturn, _radioPoint1,
                _radioMiddleTurn, _radioMiddleReturn, _radioPoint2,
                _radioLowTurn, _radioLowReturn
            })
                numeric.ValueChanged += (_, _) => RaiseRadioChanged();

            _layout.Controls.Add(CreateSection("Boost"));
            _boostLabel = CreateValueLabel("-");
            _boostLabel.Height = 66;
            _layout.Controls.Add(_boostLabel);

            _layout.Controls.Add(CreateSection("Turbo"));
            _turboLabel = CreateValueLabel("-");
            _turboLabel.Height = 66;
            _layout.Controls.Add(_turboLabel);

            _throttleCurve = new TuneCurvePreview();
            _boostCurve = new TuneCurvePreview();
            _throttleCurve.Height = 170;
            _boostCurve.Height = 170;
            _layout.Controls.Add(_throttleCurve);
            _layout.Controls.Add(_boostCurve);

            SetAvailableRuns(Array.Empty<int>(), preferredSlot: null);
            SetTune(null, null);
            ArrangePanelRegions();
            ResizeContent();
        }

        public void SetAvailableRuns(IEnumerable<int> slots, int? preferredSlot)
        {
            _isUpdating = true;
            try
            {
                _availableRuns.Clear();
                _runCombo.Items.Clear();

                foreach (int slot in slots.OrderBy(s => s))
                {
                    string label = $"Run {slot}";
                    _availableRuns[slot] = label;
                    _runCombo.Items.Add(label);
                }

                if (_runCombo.Items.Count == 0)
                {
                    _attachButton.Enabled = true;
                    _runCombo.Enabled = false;
                    _statusLabel.Text = "No Castle run loaded. You can still load a tune to preview it.";
                }
                else
                {
                    _attachButton.Enabled = true;
                    _runCombo.Enabled = true;

                    string? selectedLabel = preferredSlot.HasValue && _availableRuns.TryGetValue(preferredSlot.Value, out var preferred)
                        ? preferred
                        : _runCombo.Items[0]?.ToString();

                    if (selectedLabel != null)
                        _runCombo.SelectedItem = selectedLabel;
                }
            }
            finally
            {
                _isUpdating = false;
            }
        }

        public void SetTune(int? slot, TuneSettings? tune)
        {
            _isUpdating = true;
            try
            {
                if (slot.HasValue && _availableRuns.TryGetValue(slot.Value, out var label))
                    _runCombo.SelectedItem = label;

                _statusLabel.Text = slot.HasValue
                    ? tune == null ? $"Run {slot.Value}: no tune attached." : $"Run {slot.Value}: tune attached."
                    : tune == null
                        ? "No Castle run selected. Load a tune to preview it."
                        : "Tune preview loaded. Load a Castle run to attach it.";

                _boostLabel.Text = tune == null ? "-" : BuildBoostText(tune);
                _turboLabel.Text = tune == null ? "-" : BuildTurboText(tune);

                ApplyRadio(tune?.Radio);

                _throttleCurve.SetCurve(
                    "Throttle curve",
                    tune != null ? tune.ThrottleCurve : Array.Empty<TuneCurvePoint>(),
                    "No throttle curve");
                _boostCurve.SetCurve(
                    "Boost by throttle",
                    tune != null ? tune.BoostByThrottleCurve : Array.Empty<TuneCurvePoint>(),
                    "No boost curve",
                    tune is { BoostByThrottleCurve.Count: > 0, BoostEnabled: false });
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private void ToggleCollapsed()
        {
            _isCollapsed = !_isCollapsed;
            _content.Visible = !_isCollapsed;
            _resizeGrip.Visible = !_isCollapsed;
            Width = _isCollapsed ? CollapsedWidth : _lastExpandedWidth;
            _collapseButton.Text = _isCollapsed ? "Tune\n›" : "Tune\n‹";
            ArrangePanelRegions();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ArrangePanelRegions();
        }

        private void ArrangePanelRegions()
        {
            if (_resizeGrip == null || _collapseButton == null || _content == null)
                return;

            if (_isCollapsed)
            {
                _collapseButton.SetBounds(0, 0, CollapsedWidth, ClientSize.Height);
                return;
            }

            int contentLeft = ResizeGripWidth + CollapsedWidth;
            _resizeGrip.SetBounds(0, 0, ResizeGripWidth, ClientSize.Height);
            _collapseButton.SetBounds(
                ResizeGripWidth,
                0,
                CollapsedWidth,
                ClientSize.Height);
            _content.SetBounds(
                contentLeft,
                0,
                Math.Max(0, ClientSize.Width - contentLeft),
                ClientSize.Height);

            _content.AutoScrollPosition = Point.Empty;
            ResizeContent();
        }

        private void ResizeGrip_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || _isCollapsed)
                return;

            _isResizing = true;
            _resizeStartMouseX = Cursor.Position.X;
            _resizeStartWidth = Width;
            _resizeGrip.Capture = true;
        }

        private void ResizeGrip_MouseMove(object? sender, MouseEventArgs e)
        {
            if (!_isResizing)
                return;

            int delta = _resizeStartMouseX - Cursor.Position.X;
            int availableWidth = Parent?.ClientSize.Width ?? MaximumExpandedWidth;
            int maximum = Math.Min(
                MaximumExpandedWidth,
                Math.Max(MinimumExpandedWidth, availableWidth - 420));

            Width = Math.Clamp(_resizeStartWidth + delta, MinimumExpandedWidth, maximum);
            _lastExpandedWidth = Width;
        }

        private void ResizeGrip_MouseUp(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;

            _isResizing = false;
            _resizeGrip.Capture = false;
        }

        private void ResizeContent()
        {
            if (_content == null || _layout == null)
                return;

            int contentWidth = Math.Max(
                260,
                _content.ClientSize.Width - _content.Padding.Horizontal - 2);
            _layout.Width = contentWidth;

            foreach (Control control in _layout.Controls)
                control.Width = Math.Max(240, contentWidth - control.Margin.Horizontal);

            _radioModeFields.Width = contentWidth;
            foreach (Control control in _radioModeFields.Controls)
                control.Width = Math.Max(240, contentWidth - control.Margin.Horizontal);
        }

        private bool TryGetSelectedSlot(out int slot)
        {
            slot = 0;
            string? selected = _runCombo.SelectedItem?.ToString();
            if (selected == null)
                return false;

            foreach (var kvp in _availableRuns)
            {
                if (kvp.Value == selected)
                {
                    slot = kvp.Key;
                    return true;
                }
            }
            return false;
        }

        private int? GetSelectedSlot() =>
            TryGetSelectedSlot(out int slot) ? slot : null;

        private void RaiseRadioChanged()
        {
            if (_isUpdating || !TryGetSelectedSlot(out int slot))
                return;

            RadioSettingsChanged?.Invoke(slot, new RadioTuneSettings
            {
                Mode = GetRadioMode(),
                ThrottleSpeedMode = _radioModeCombo.Text,
                SpeedType = _radioSpeedTypeCombo.Text,
                AllTurnPercent = ValueOrNull(_radioAllTurn),
                AllReturnPercent = ValueOrNull(_radioAllReturn),
                HighTurnPercent = ValueOrNull(_radioHighTurn),
                HighReturnPercent = ValueOrNull(_radioHighReturn),
                Point1Percent = ValueOrNull(_radioPoint1),
                MiddleTurnPercent = ValueOrNull(_radioMiddleTurn),
                MiddleReturnPercent = ValueOrNull(_radioMiddleReturn),
                Point2Percent = ValueOrNull(_radioPoint2),
                LowTurnPercent = ValueOrNull(_radioLowTurn),
                LowReturnPercent = ValueOrNull(_radioLowReturn)
            });
        }

        private void ApplyRadio(RadioTuneSettings? radio)
        {
            int mode = Math.Clamp(radio?.Mode ?? ParseLegacyMode(radio?.ThrottleSpeedMode), 1, 3);
            _radioModeCombo.SelectedIndex = mode - 1;
            _radioSpeedTypeCombo.SelectedItem =
                string.IsNullOrWhiteSpace(radio?.SpeedType) ? "Normal" : radio.SpeedType;
            if (_radioSpeedTypeCombo.SelectedIndex < 0)
                _radioSpeedTypeCombo.SelectedIndex = 0;

            SetNumeric(_radioAllTurn, radio?.AllTurnPercent ?? radio?.AllPercent);
            SetNumeric(_radioAllReturn, radio?.AllReturnPercent ?? 100);
            SetNumeric(_radioHighTurn, radio?.HighTurnPercent ?? radio?.HighPercent);
            SetNumeric(_radioHighReturn, radio?.HighReturnPercent ?? 100);
            SetNumeric(_radioPoint1, radio?.Point1Percent);
            SetNumeric(_radioMiddleTurn, radio?.MiddleTurnPercent ?? radio?.MiddlePercent);
            SetNumeric(_radioMiddleReturn, radio?.MiddleReturnPercent ?? 100);
            SetNumeric(_radioPoint2, radio?.Point2Percent);
            SetNumeric(_radioLowTurn, radio?.LowTurnPercent ?? radio?.LowPercent);
            SetNumeric(_radioLowReturn, radio?.LowReturnPercent ?? 100);
            RebuildRadioModeFields();
        }

        private int GetRadioMode() => _radioModeCombo.SelectedIndex + 1;

        private static int ParseLegacyMode(string? value)
        {
            if (value?.Contains('3') == true || value?.Contains("Middle", StringComparison.OrdinalIgnoreCase) == true)
                return 3;
            if (value?.Contains('2') == true || value?.Contains("High", StringComparison.OrdinalIgnoreCase) == true)
                return 2;
            return 1;
        }

        private void RebuildRadioModeFields()
        {
            if (_radioModeFields == null)
                return;

            _radioModeFields.SuspendLayout();
            _radioModeFields.Controls.Clear();

            switch (GetRadioMode())
            {
                case 2:
                    _radioModeFields.Controls.Add(CreateZoneRow("High", _radioHighTurn, _radioHighReturn));
                    _radioModeFields.Controls.Add(CreatePointRow("Point 1", _radioPoint1));
                    _radioModeFields.Controls.Add(CreateZoneRow("Low", _radioLowTurn, _radioLowReturn));
                    break;
                case 3:
                    _radioModeFields.Controls.Add(CreateZoneRow("High", _radioHighTurn, _radioHighReturn));
                    _radioModeFields.Controls.Add(CreatePointRow("Point 2", _radioPoint2));
                    _radioModeFields.Controls.Add(CreateZoneRow("Middle", _radioMiddleTurn, _radioMiddleReturn));
                    _radioModeFields.Controls.Add(CreatePointRow("Point 1", _radioPoint1));
                    _radioModeFields.Controls.Add(CreateZoneRow("Low", _radioLowTurn, _radioLowReturn));
                    break;
                default:
                    _radioModeFields.Controls.Add(CreateZoneRow("All", _radioAllTurn, _radioAllReturn));
                    break;
            }

            _radioModeFields.ResumeLayout(true);
            ResizeContent();
        }

        private TableLayoutPanel CreateZoneRow(
            string label,
            NumericUpDown turn,
            NumericUpDown returnValue)
        {
            var row = new TableLayoutPanel
            {
                ColumnCount = 3,
                RowCount = 2,
                Height = 52,
                Margin = new Padding(0, 2, 0, 2),
                BackColor = BackColor
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
            row.Controls.Add(CreateSmallLabel(label), 0, 1);
            row.Controls.Add(CreateSmallLabel("Turn"), 1, 0);
            row.Controls.Add(CreateSmallLabel("Return"), 2, 0);
            row.Controls.Add(turn, 1, 1);
            row.Controls.Add(returnValue, 2, 1);
            return row;
        }

        private TableLayoutPanel CreatePointRow(string label, NumericUpDown value)
        {
            var row = new TableLayoutPanel
            {
                ColumnCount = 2,
                RowCount = 1,
                Height = 30,
                Margin = new Padding(0),
                BackColor = BackColor
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            row.Controls.Add(CreateSmallLabel(label), 0, 0);
            row.Controls.Add(value, 1, 0);
            return row;
        }

        private static Label CreateSmallLabel(string text) => new()
        {
            Text = text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(0xBF, 0xC7, 0xD5)
        };

        private static Label CreateTitle(string text)
        {
            var baseFont = SystemFonts.MessageBoxFont ?? Control.DefaultFont;
            return new Label
            {
                Text = text,
                AutoSize = false,
                Width = 360,
                Height = 30,
                Font = new Font(baseFont.FontFamily, 15, FontStyle.Bold),
                ForeColor = Color.FromArgb(0xF6, 0xF8, 0xFB),
                Margin = new Padding(0, 0, 0, 2)
            };
        }

        private static Label CreateSection(string text)
        {
            var baseFont = SystemFonts.MessageBoxFont ?? Control.DefaultFont;
            return new Label
            {
                Text = text,
                AutoSize = false,
                Width = 360,
                Height = 24,
                Font = new Font(baseFont, FontStyle.Bold),
                ForeColor = Color.FromArgb(0xF6, 0xF8, 0xFB),
                Margin = new Padding(0, 12, 0, 2)
            };
        }

        private static Label CreateValueLabel(string text) => new()
        {
            Text = text,
            AutoSize = false,
            Width = 360,
            Height = 22,
            ForeColor = Color.FromArgb(0xBF, 0xC7, 0xD5),
            Margin = new Padding(0, 1, 0, 1)
        };

        private Button CreateButton(string text)
        {
            var button = new Button
            {
                Text = text,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0x2A, 0x66, 0xB8),
                ForeColor = Color.White,
                Margin = new Padding(4, 0, 0, 0)
            };
            button.FlatAppearance.BorderColor = Color.FromArgb(0x3D, 0x7D, 0xD7);
            return button;
        }

        private TableLayoutPanel CreateGrid(int columns) => new()
        {
            ColumnCount = columns,
            AutoSize = true,
            Width = 360,
            BackColor = BackColor,
            Margin = new Padding(0, 0, 0, 0)
        };

        private static TextBox CreateTextBox() => new()
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(0x20, 0x26, 0x31),
            ForeColor = Color.FromArgb(0xE6, 0xE9, 0xEF),
            BorderStyle = BorderStyle.FixedSingle
        };

        private static NumericUpDown CreateNumeric() => new()
        {
            Dock = DockStyle.Fill,
            Minimum = 0,
            Maximum = 100,
            DecimalPlaces = 1,
            Increment = 0.5M,
            BackColor = Color.FromArgb(0x20, 0x26, 0x31),
            ForeColor = Color.FromArgb(0xE6, 0xE9, 0xEF)
        };

        private static void AddField(TableLayoutPanel grid, string label, Control input)
        {
            int row = grid.RowCount++;
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            var labelControl = new Label
            {
                Text = label,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(0xBF, 0xC7, 0xD5)
            };
            grid.Controls.Add(labelControl, 0, row);
            grid.Controls.Add(input, 1, row);

            if (grid.ColumnStyles.Count == 0)
            {
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));
            }
        }

        private static string BuildBoostText(TuneSettings tune)
        {
            string boost = tune.BoostEnabled ? "YES" : EmptyDash(tune.BoostModeEnableText);
            return
                $"Enabled: {boost}   Amount: {Format(tune.BoostTimingAmountDeg, " deg")}\n" +
                $"RPM: {Format(tune.BoostStartRpm, "")} - {Format(tune.BoostEndRpm, "")}\n" +
                $"Throttle: {Format(tune.BoostStartThrottlePct, "%")} - {Format(tune.BoostEndThrottlePct, "%")}";
        }

        private static string BuildTurboText(TuneSettings tune)
        {
            string turbo = tune.TurboEnabled ? "YES" : EmptyDash(tune.TurboEnableText);
            return
                $"Enabled: {turbo}   Amount: {Format(tune.TurboTimingAmountDeg, " deg")}\n" +
                $"Activation throttle: {Format(tune.TurboActivationThrottlePct, "%")}\n" +
                $"Acceleration / Deceleration: {Format(tune.TurboAccelDegPerSecond, " deg/s")} / {Format(tune.TurboDecelDegPerSecond, " deg/s")}";
        }

        private static string BuildLegacyTimingText(TuneSettings tune)
        {
            string turbo = tune.TurboEnabled ? "YES" : EmptyDash(tune.TurboEnableText);
            string boost = tune.BoostEnabled ? "YES" : EmptyDash(tune.BoostModeEnableText);
            return
                $"Turbo: {turbo}\n" +
                $"Turbo amount: {Format(tune.TurboTimingAmountDeg, "°")}\n" +
                $"Activation throttle: {Format(tune.TurboActivationThrottlePct, "%")}\n" +
                $"Accel / Decel: {Format(tune.TurboAccelDegPerSecond, "°/s")} / {Format(tune.TurboDecelDegPerSecond, "°/s")}\n" +
                $"Boost: {boost}\n" +
                $"Boost amount: {Format(tune.BoostTimingAmountDeg, "°")}\n" +
                $"Boost RPM: {Format(tune.BoostStartRpm, "")} - {Format(tune.BoostEndRpm, "")}\n" +
                $"Boost throttle: {Format(tune.BoostStartThrottlePct, "%")} - {Format(tune.BoostEndThrottlePct, "%")}\n" +
                $"Max power / Drag brake: {EmptyDash(tune.MaxPower)} / {EmptyDash(tune.DragBrake)}";
        }

        private static string Format(double? value, string suffix) =>
            value.HasValue ? $"{value.Value:0.##}{suffix}" : "-";

        private static string EmptyDash(string? value) =>
            string.IsNullOrWhiteSpace(value) ? "-" : value;

        private static double? ValueOrNull(NumericUpDown control) =>
            control.Value == 0 ? null : (double)control.Value;

        private static void SetNumeric(NumericUpDown control, double? value)
        {
            control.Value = value.HasValue
                ? Math.Min(control.Maximum, Math.Max(control.Minimum, (decimal)value.Value))
                : 0;
        }
    }
}
