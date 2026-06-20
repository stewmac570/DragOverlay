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
        private readonly VerticalTextButton _collapseButton;
        private readonly UnifiedScrollPanel _content;
        private readonly FlowLayoutPanel _layout;
        private readonly TableLayoutPanel _curveLayout;
        private readonly ComboBox _runCombo;
        private readonly Button _attachButton;
        private readonly Label _statusLabel;
        private readonly ComboBox _radioModeCombo;
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
        private bool _performingLayout;
        private string _radioSpeedType = "Normal";
        private int _resizeStartMouseX;
        private int _resizeStartWidth;
        private int _lastExpandedWidth = ExpandedWidth;

        public event Action<int?>? AttachTuneRequested;
        public event Action<int, RadioTuneSettings>? RadioSettingsChanged;
        public event Action<int>? SelectedRunChanged;
        public event Action? OpenProjectRequested;
        public event Action? SaveProjectRequested;

        private Button? _openProjectButton;
        private Button? _saveProjectButton;

        public void SetSaveProjectEnabled(bool enabled)
        {
            if (_saveProjectButton != null) _saveProjectButton.Enabled = enabled;
        }

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
            _resizeGrip.MouseCaptureChanged += (_, _) =>
            {
                if (_isResizing && !_resizeGrip.Capture)
                    CompletePanelResize();
            };
            Controls.Add(_resizeGrip);

            _collapseButton = new VerticalTextButton
            {
                Dock = DockStyle.None,
                Width = CollapsedWidth,
                Text = "Tune",
                Arrow = "‹",
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0x1D, 0x23, 0x2D),
                ForeColor = ForeColor
            };
            _collapseButton.FlatAppearance.BorderColor = Color.FromArgb(0x33, 0x3B, 0x48);
            _collapseButton.Click += (_, _) => ToggleCollapsed();
            Controls.Add(_collapseButton);

            _content = new UnifiedScrollPanel
            {
                Dock = DockStyle.None,
                Padding = new Padding(12),
                AutoScroll = true,
                BackColor = BackColor
            };
            Controls.Add(_content);

            _layout = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = BackColor
            };
            _content.Controls.Add(_layout);
            _content.Resize += (_, _) =>
            {
                if (!_isResizing)
                    ResizeContent();
            };

            _layout.Controls.Add(CreateTitle("Tune"));

            var projectRow = new TableLayoutPanel
            {
                ColumnCount = 2,
                RowCount = 1,
                Width = 360,
                Height = 34,
                Margin = new Padding(0, 4, 0, 6),
                BackColor = BackColor
            };
            projectRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            projectRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            _openProjectButton = CreateButton("Open project");
            _openProjectButton.Dock = DockStyle.Fill;
            _openProjectButton.Click += (_, _) => OpenProjectRequested?.Invoke();
            _saveProjectButton = CreateButton("Save project");
            _saveProjectButton.Dock = DockStyle.Fill;
            _saveProjectButton.Enabled = false;
            _saveProjectButton.Click += (_, _) => SaveProjectRequested?.Invoke();
            projectRow.Controls.Add(_openProjectButton, 0, 0);
            projectRow.Controls.Add(_saveProjectButton, 1, 0);
            _layout.Controls.Add(projectRow);

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
            AddField(radioHeader, "Mode", _radioModeCombo);
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
            _boostLabel.Height = 104;
            _layout.Controls.Add(_boostLabel);

            _layout.Controls.Add(CreateSection("Turbo"));
            _turboLabel = CreateValueLabel("-");
            _turboLabel.Height = 72;
            _layout.Controls.Add(_turboLabel);

            _throttleCurve = new TuneCurvePreview();
            _boostCurve = new TuneCurvePreview();
            _throttleCurve.Dock = DockStyle.Fill;
            _boostCurve.Dock = DockStyle.Fill;
            _throttleCurve.Margin = new Padding(0, 0, 0, 5);
            _boostCurve.Margin = new Padding(0, 5, 0, 0);

            _curveLayout = new TableLayoutPanel
            {
                ColumnCount = 1,
                RowCount = 2,
                BackColor = BackColor,
                Margin = Padding.Empty
            };
            _curveLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _curveLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            _curveLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            _curveLayout.Controls.Add(_throttleCurve, 0, 0);
            _curveLayout.Controls.Add(_boostCurve, 0, 1);
            _content.Controls.Add(_curveLayout);

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

                bool boostEnabled = tune?.BoostEnabled == true;
                _boostLabel.Text = tune == null ? "-" : BuildBoostText(tune);
                _boostLabel.Visible = tune == null || boostEnabled;
                _boostLabel.Height = 72;
                _turboLabel.Text = tune == null ? "-" : BuildTurboText(tune);

                ApplyRadio(tune?.Radio);

                _throttleCurve.SetCurve(
                    "Throttle curve",
                    tune != null ? tune.ThrottleCurve : Array.Empty<TuneCurvePoint>(),
                    "No throttle curve");
                _boostCurve.SetCurve(
                    "Boost by throttle",
                    tune != null ? tune.BoostByThrottleCurve : Array.Empty<TuneCurvePoint>(),
                    "No boost curve");
                _boostCurve.Visible = true;
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
            _collapseButton.Arrow = _isCollapsed ? "›" : "‹";
            _collapseButton.Invalidate();
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

            if (!_isResizing)
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
            _content.BeginInteractiveResize();
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

            int contentLeft = ResizeGripWidth + CollapsedWidth;
            _content.SetBounds(
                contentLeft,
                0,
                Math.Max(0, ClientSize.Width - contentLeft),
                ClientSize.Height);
            _content.Invalidate();
        }

        private void ResizeGrip_MouseUp(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;

            _resizeGrip.Capture = false;
            CompletePanelResize();
        }

        private void CompletePanelResize()
        {
            if (!_isResizing)
                return;

            _isResizing = false;
            ArrangePanelRegions();
            ResizeContent();
            _content.EndInteractiveResize();
        }

        private void ResizeContent()
        {
            if (_content == null || _layout == null || _curveLayout == null || _performingLayout)
                return;

            _performingLayout = true;
            int verticalScroll = _content.VerticalScrollPosition;
            _content.SuspendLayout();
            _layout.SuspendLayout();
            _radioModeFields.SuspendLayout();
            _curveLayout.SuspendLayout();
            try
            {
                _content.ResetHorizontalScroll();

                int innerLeft = _content.Padding.Left;
                int innerTop = _content.Padding.Top;
                int innerWidth = Math.Max(0, _content.Width - _content.Padding.Horizontal - 6);
                int innerHeight = Math.Max(0, _content.ClientSize.Height - _content.Padding.Vertical);
                int curveHeight = Math.Clamp(
                    (int)Math.Round(innerHeight * 0.52),
                    Math.Min(260, innerHeight),
                    Math.Min(560, innerHeight));
                int contentWidth = innerWidth;
                _layout.Width = contentWidth;

                foreach (Control control in _layout.Controls)
                    control.Width = Math.Max(0, contentWidth - control.Margin.Horizontal);

                _radioModeFields.Width = contentWidth;
                foreach (Control control in _radioModeFields.Controls)
                    control.Width = Math.Max(0, contentWidth - control.Margin.Horizontal);

                _layout.Location = new Point(innerLeft, innerTop);
                int minimumContentHeight = _layout.Bottom + 10 + curveHeight + _content.Padding.Bottom;
                int totalContentHeight = Math.Max(innerHeight + _content.Padding.Vertical, minimumContentHeight);
                int curveTop = Math.Max(_layout.Bottom + 10, totalContentHeight - _content.Padding.Bottom - curveHeight);
                _curveLayout.AutoSize = false;
                _curveLayout.MinimumSize = Size.Empty;
                _curveLayout.MaximumSize = Size.Empty;
                _curveLayout.SetBounds(innerLeft, curveTop, innerWidth, curveHeight);
                _throttleCurve.MinimumSize = Size.Empty;
                _boostCurve.MinimumSize = Size.Empty;
                _throttleCurve.SetBounds(
                    0,
                    0,
                    innerWidth,
                    Math.Max(0, curveHeight / 2 - 5));
                _boostCurve.SetBounds(
                    0,
                    curveHeight / 2 + 5,
                    innerWidth,
                    Math.Max(0, curveHeight - curveHeight / 2 - 5));
                _content.AutoScrollMinSize = new Size(1, totalContentHeight);
            }
            finally
            {
                _curveLayout.ResumeLayout(false);
                _curveLayout.PerformLayout();
                _radioModeFields.ResumeLayout(false);
                _layout.ResumeLayout(false);
                _content.ResumeLayout(true);
                _throttleCurve.Invalidate();
                _boostCurve.Invalidate();
                _content.SetVerticalScroll(verticalScroll);
                _content.RefreshScrollIndicator();
                _performingLayout = false;
            }
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
                SpeedType = _radioSpeedType,
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
            _radioSpeedType = string.IsNullOrWhiteSpace(radio?.SpeedType)
                ? "Normal"
                : radio.SpeedType;

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

            var grid = new TableLayoutPanel
            {
                ColumnCount = 4,
                AutoSize = false,
                Width = Math.Max(0, _radioModeFields.ClientSize.Width),
                Margin = new Padding(0),
                BackColor = BackColor,
                GrowStyle = TableLayoutPanelGrowStyle.AddRows
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            AddRadioHeader(grid);

            switch (GetRadioMode())
            {
                case 2:
                    AddZoneRow(grid, null, _radioHighTurn, _radioHighReturn, "High");
                    AddZoneRow(grid, _radioPoint1, _radioLowTurn, _radioLowReturn, "Low");
                    break;
                case 3:
                    AddZoneRow(grid, null, _radioHighTurn, _radioHighReturn, "High");
                    AddZoneRow(grid, _radioPoint2, _radioMiddleTurn, _radioMiddleReturn, "Middle");
                    AddZoneRow(grid, _radioPoint1, _radioLowTurn, _radioLowReturn, "Low");
                    break;
                default:
                    AddZoneRow(grid, null, _radioAllTurn, _radioAllReturn, "All");
                    break;
            }

            grid.Height = grid.RowStyles
                .Cast<RowStyle>()
                .Sum(style => (int)style.Height);
            _radioModeFields.Controls.Add(grid);
            _radioModeFields.ResumeLayout(true);
            ResizeContent();
        }

        private static void AddRadioHeader(TableLayoutPanel grid)
        {
            int row = grid.RowCount++;
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
            grid.Controls.Add(CreateSmallLabel("Point"), 0, row);
            grid.Controls.Add(CreateSmallLabel("Turn"), 1, row);
            grid.Controls.Add(CreateSmallLabel("Return"), 2, row);
            grid.Controls.Add(CreateSmallLabel(""), 3, row);
        }

        private static void AddZoneRow(
            TableLayoutPanel grid,
            NumericUpDown? pointValue,
            NumericUpDown turn,
            NumericUpDown returnValue,
            string zoneLabel)
        {
            int row = grid.RowCount++;
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            int tabIndex = row * 3;
            if (pointValue != null)
                pointValue.TabIndex = tabIndex++;
            turn.TabIndex = tabIndex++;
            returnValue.TabIndex = tabIndex;
            grid.Controls.Add(pointValue ?? CreateSpacer(), 0, row);
            grid.Controls.Add(turn, 1, row);
            grid.Controls.Add(returnValue, 2, row);
            grid.Controls.Add(CreateSmallLabel(zoneLabel), 3, row);
        }

        private static Control CreateSpacer() => new Panel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            BackColor = Color.Transparent
        };

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

        private static NumericUpDown CreateNumeric()
        {
            var numeric = new NumericUpDown
            {
                Dock = DockStyle.Fill,
                Minimum = 1,
                Maximum = 100,
                DecimalPlaces = 0,
                Increment = 1,
                BackColor = Color.FromArgb(0x20, 0x26, 0x31),
                ForeColor = Color.FromArgb(0xE6, 0xE9, 0xEF)
            };
            numeric.Enter += (_, _) => SelectNumericText(numeric);
            numeric.MouseUp += (_, e) =>
            {
                if (e.Button == MouseButtons.Left)
                    SelectNumericText(numeric);
            };
            return numeric;
        }

        private static void SelectNumericText(NumericUpDown numeric)
        {
            if (!numeric.IsHandleCreated)
                return;

            numeric.BeginInvoke(() => numeric.Select(0, numeric.Text.Length));
        }

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
            bool throttleBased = tune.BoostTimingMode.Contains(
                "Throttle",
                StringComparison.OrdinalIgnoreCase);
            string activationText = throttleBased
                ? $"Start throttle: {Format(tune.BoostStartThrottlePct, "%")}\n" +
                  $"End throttle: {Format(tune.BoostEndThrottlePct, "%")}"
                : $"Start RPM: {Format(tune.BoostStartRpm, "")}\n" +
                  $"End RPM: {Format(tune.BoostEndRpm, "")}";

            return
                "Enabled: YES\n" +
                $"Amount: {Format(tune.BoostTimingAmountDeg, " deg")}\n" +
                activationText;
        }

        private static string BuildTurboText(TuneSettings tune)
        {
            string turbo = tune.TurboEnabled ? "YES" : EmptyDash(tune.TurboEnableText);
            return
                $"Enabled: {turbo}\n" +
                $"Amount: {Format(tune.TurboTimingAmountDeg, " deg")}\n" +
                $"Activation throttle: {Format(tune.TurboActivationThrottlePct, "%")}\n" +
                $"Acceleration: {Format(tune.TurboAccelDegPerSecond, " deg/s")}";
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
            (double)decimal.Round(control.Value, 0, MidpointRounding.AwayFromZero);

        private static void SetNumeric(NumericUpDown control, double? value)
        {
            control.Value = value.HasValue
                ? Math.Min(
                    control.Maximum,
                    Math.Max(
                        control.Minimum,
                        decimal.Round((decimal)value.Value, 0, MidpointRounding.AwayFromZero)))
                : control.Minimum;
        }

        private sealed class UnifiedScrollPanel : Panel
        {
            private const int ScrollBarHorizontal = 0;
            private const int ScrollBarVertical = 1;
            private const int TrackWidth = 3;
            private const int TrackRightMargin = 2;
            private bool _draggingThumb;
            private bool _resettingHorizontalScroll;
            private int _dragStartY;
            private int _dragStartScroll;

            public UnifiedScrollPanel()
            {
                DoubleBuffered = true;
                SetStyle(ControlStyles.ResizeRedraw, true);
                HorizontalScroll.Enabled = false;
                HorizontalScroll.Visible = false;
                Scroll += (_, e) =>
                {
                    if (e.ScrollOrientation == ScrollOrientation.HorizontalScroll)
                        ResetHorizontalScroll();
                    InvalidateScrollTrack();
                };
            }

            public int VerticalScrollPosition => Math.Max(0, -AutoScrollPosition.Y);

            public void BeginInteractiveResize()
            {
                ResetHorizontalScroll();
                HideNativeScrollBar();
            }

            public void EndInteractiveResize()
            {
                ResetHorizontalScroll();
                HideNativeScrollBar();
                Invalidate();
            }

            public void ResetHorizontalScroll()
            {
                if (_resettingHorizontalScroll || AutoScrollPosition.X == 0)
                    return;

                _resettingHorizontalScroll = true;
                int vertical = VerticalScrollPosition;
                try
                {
                    AutoScrollPosition = new Point(0, vertical);
                }
                finally
                {
                    _resettingHorizontalScroll = false;
                }
            }

            public void SetVerticalScroll(int position)
            {
                int maximum = GetMaximumScroll();
                AutoScrollPosition = new Point(0, Math.Clamp(position, 0, maximum));
            }

            public void RefreshScrollIndicator()
            {
                ResetHorizontalScroll();
                HideNativeScrollBar();
                InvalidateScrollTrack();
            }

            protected override void OnHandleCreated(EventArgs e)
            {
                base.OnHandleCreated(e);
                HideNativeScrollBar();
            }

            protected override void OnLayout(LayoutEventArgs levent)
            {
                base.OnLayout(levent);
                ResetHorizontalScroll();
                HideNativeScrollBar();
                InvalidateScrollTrack();
            }

            protected override Point ScrollToControl(Control activeControl)
            {
                Point target = base.ScrollToControl(activeControl);
                return new Point(0, target.Y);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                Rectangle thumb = GetThumbRectangle();
                if (thumb.IsEmpty)
                    return;

                using var trackBrush = new SolidBrush(Color.FromArgb(0x18, 0x1D, 0x25));
                using var thumbBrush = new SolidBrush(Color.FromArgb(0x3B, 0x43, 0x50));
                int trackX = ClientSize.Width - TrackWidth - TrackRightMargin;
                e.Graphics.FillRectangle(trackBrush, trackX, 0, TrackWidth, ClientSize.Height);
                e.Graphics.FillRectangle(thumbBrush, thumb);
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                Rectangle thumb = GetThumbRectangle();
                if (!thumb.IsEmpty && thumb.Contains(e.Location))
                {
                    _draggingThumb = true;
                    _dragStartY = e.Y;
                    _dragStartScroll = -AutoScrollPosition.Y;
                    Capture = true;
                    return;
                }

                base.OnMouseDown(e);
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                if (!_draggingThumb)
                {
                    base.OnMouseMove(e);
                    return;
                }

                Rectangle thumb = GetThumbRectangle();
                int maximumScroll = GetMaximumScroll();
                int travel = Math.Max(1, ClientSize.Height - thumb.Height);
                int scroll = _dragStartScroll +
                    (int)Math.Round((e.Y - _dragStartY) * maximumScroll / (double)travel);
                AutoScrollPosition = new Point(0, Math.Clamp(scroll, 0, maximumScroll));
                InvalidateScrollTrack();
            }

            protected override void OnMouseUp(MouseEventArgs e)
            {
                if (_draggingThumb)
                {
                    _draggingThumb = false;
                    Capture = false;
                }

                base.OnMouseUp(e);
            }

            protected override void OnMouseWheel(MouseEventArgs e)
            {
                int maximumScroll = GetMaximumScroll();
                int scroll = Math.Clamp(
                    -AutoScrollPosition.Y - Math.Sign(e.Delta) * 56,
                    0,
                    maximumScroll);
                AutoScrollPosition = new Point(0, scroll);
                InvalidateScrollTrack();
            }

            private Rectangle GetThumbRectangle()
            {
                int contentHeight = DisplayRectangle.Height;
                if (contentHeight <= ClientSize.Height || ClientSize.Height <= 0)
                    return Rectangle.Empty;

                int thumbHeight = Math.Max(
                    28,
                    (int)Math.Round(ClientSize.Height * ClientSize.Height / (double)contentHeight));
                int maximumScroll = GetMaximumScroll();
                int travel = Math.Max(0, ClientSize.Height - thumbHeight);
                int thumbY = maximumScroll == 0
                    ? 0
                    : (int)Math.Round(-AutoScrollPosition.Y * travel / (double)maximumScroll);
                return new Rectangle(
                    ClientSize.Width - TrackWidth - TrackRightMargin,
                    thumbY,
                    TrackWidth,
                    thumbHeight);
            }

            private int GetMaximumScroll() =>
                Math.Max(0, DisplayRectangle.Height - ClientSize.Height);

            private void HideNativeScrollBar()
            {
                if (IsHandleCreated)
                {
                    ShowScrollBar(Handle, ScrollBarHorizontal, false);
                    ShowScrollBar(Handle, ScrollBarVertical, false);
                }
            }

            private void InvalidateScrollTrack()
            {
                Invalidate(new Rectangle(
                    Math.Max(0, ClientSize.Width - TrackWidth - TrackRightMargin),
                    0,
                    TrackWidth + TrackRightMargin,
                    ClientSize.Height));
            }

            [System.Runtime.InteropServices.DllImport("user32.dll")]
            private static extern bool ShowScrollBar(IntPtr hWnd, int wBar, bool bShow);
        }

        private sealed class VerticalTextButton : Button
        {
            public string Arrow { get; set; } = "‹";

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.Clear(BackColor);
                string verticalText = string.Join(
                    Environment.NewLine,
                    Text.ToUpperInvariant().ToCharArray());
                Size textSize = TextRenderer.MeasureText(
                    verticalText,
                    Font,
                    new Size(Width, Height),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
                int textTop = Math.Max(0, (Height - textSize.Height) / 2);
                TextRenderer.DrawText(
                    e.Graphics,
                    verticalText,
                    Font,
                    new Rectangle(0, textTop, Width, textSize.Height),
                    ForeColor,
                    TextFormatFlags.HorizontalCenter |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPadding);

                ControlPaint.DrawBorder(e.Graphics, ClientRectangle, FlatAppearance.BorderColor, ButtonBorderStyle.Solid);
            }
        }
    }
}
