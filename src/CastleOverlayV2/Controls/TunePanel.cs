using CastleOverlayV2.Models;
using System.Drawing.Drawing2D;

namespace CastleOverlayV2.Controls
{
    /// <summary>One column of the tune comparison: a loaded Castle run and its tune.</summary>
    /// <param name="Slot">Castle slot 1..3, or 0 for a tune previewed without a run.</param>
    public sealed record TuneRunColumn(int Slot, string LogName, TuneSettings? Tune);

    /// <summary>
    /// The Tune page (Docs/UI_Layout_Plan.md): every loaded run's tune side by side.
    /// Left: a settings table, one column per run, rows that differ highlighted, with
    /// "Show only differences". The radio throttle speed rows are typed in per run.
    /// Right: the throttle and boost-by-throttle curves of every tune on one chart each.
    /// </summary>
    public sealed class TunePanel : UserControl
    {
        private static readonly Color SurfaceWindow = Color.FromArgb(0x13, 0x17, 0x1E);
        private static readonly Color SurfacePanel = Color.FromArgb(0x16, 0x1B, 0x23);
        private static readonly Color SurfaceBar = Color.FromArgb(0x1B, 0x21, 0x2B);
        private static readonly Color SurfaceInput = Color.FromArgb(0x20, 0x26, 0x31);
        private static readonly Color DiffRow = Color.FromArgb(0x22, 0x1D, 0x10);
        private static readonly Color TextPrimary = Color.FromArgb(0xE6, 0xE9, 0xEF);
        private static readonly Color TextSecond = Color.FromArgb(0x9A, 0xA3, 0xB2);
        private static readonly Color TextDim = Color.FromArgb(0x5C, 0x65, 0x73);
        private static readonly Color TextAlert = Color.FromArgb(0xF5, 0xC5, 0x42);
        private static readonly Color BorderDef = Color.FromArgb(0x29, 0x2F, 0x39);
        private static readonly Color AccentBlue = Color.FromArgb(0x2D, 0x6C, 0xB5);

        private const int SettingColumnWidth = 250;
        private const int RowHeight = 26;
        private const int MaxValueColumnWidth = 260;

        public event Action<int?>? AttachTuneRequested;
        public event Action<int, RadioTuneSettings>? RadioSettingsChanged;

        private readonly Label _diffBadge;
        private readonly CheckBox _onlyDiffs;
        private readonly Panel _scroll;
        private readonly FlowLayoutPanel _table;
        private readonly TuneCurvePreview _throttleCurve;
        private readonly TuneCurvePreview _boostCurve;
        private readonly ToolTip _toolTip = new() { ShowAlways = true };

        private IReadOnlyList<TuneRunColumn> _columns = Array.Empty<TuneRunColumn>();
        private readonly List<TableRow> _rows = new();
        // Working copy of each run's radio settings (slot -> settings), edited in place.
        private readonly Dictionary<int, RadioTuneSettings> _radio = new();

        // ---- Which .dat settings to show, grouped. Anything not listed lands in "Other". ----
        private static readonly (string Group, (string Key, string Label)[] Rows)[] SettingGroups =
        {
            ("Boost", new[]
            {
                ("Boost Mode Enable", "Boost on"),
                ("Boost Timing Mode", "Boost mode"),
                ("Boost Timing Amount", "Boost timing"),
                ("Boost RPM Activation Range - Start RPM", "Boost start RPM"),
                ("Boost RPM Activation Range - End RPM", "Boost end RPM"),
                ("Boost Throttle Activation Range - Start RPM", "Boost start throttle"),
                ("Boost Throttle Activation Range - End RPM", "Boost end throttle"),
            }),
            ("Turbo", new[]
            {
                ("Turbo Enable", "Turbo on"),
                ("Turbo Timing Amount", "Turbo timing"),
                ("Activation Throttle Percent", "Turbo activation throttle"),
                ("Acceleration Degrees Per Second", "Turbo ramp up (deg/s)"),
                ("Deceleration Degrees Per Second", "Turbo ramp down (deg/s)"),
            }),
            ("Power and throttle", new[]
            {
                ("Max Power", "Max power"),
                ("Punch Control", "Punch control"),
                ("Start Power", "Start power"),
                ("Start Power Hold Time", "Start power hold time"),
                ("Start Power Ramp Time", "Start power ramp time"),
                ("Throttle Dead Band", "Throttle dead band"),
                ("Current Limiting", "Current limiting"),
                ("RPM Limit", "RPM limit"),
                ("Torque Control Value", "Torque control"),
            }),
            ("Brake", new[]
            {
                ("Drag Brake", "Drag brake"),
                ("Drag Brake Ramp", "Drag brake ramp"),
                ("Brake Amount", "Brake amount"),
                ("Brake Lockout Time", "Brake lockout time"),
                ("Reverse Type", "Reverse type"),
            }),
            ("Device", new[]
            {
                ("Device Name", "Device"),
                ("Current Firmware", "Firmware"),
                ("Sample Frequency", "Sample frequency"),
                ("Motor Direction", "Motor direction"),
                ("Motor Type", "Motor type"),
                ("Cutoff Voltage", "Cutoff voltage"),
                ("Auto-Lipo Volts/Cell", "Auto-LiPo volts/cell"),
                ("BEC Voltage", "BEC voltage"),
            }),
        };

        private enum RadioField { Mode, Point1Position, Point1Speed, Point2Position, Point2Speed, Point3Speed }

        private static readonly (RadioField Field, string Label)[] RadioRows =
        {
            (RadioField.Mode, "Radio mode"),
            (RadioField.Point1Position, "Point 1 position"),
            (RadioField.Point1Speed, "Point 1 speed"),
            (RadioField.Point2Position, "Point 2 position"),
            (RadioField.Point2Speed, "Point 2 speed"),
            (RadioField.Point3Speed, "Point 3 speed"),
        };

        public TunePanel()
        {
            Dock = DockStyle.Fill;
            BackColor = SurfaceWindow;
            ForeColor = TextPrimary;

            // ---- Curves on the right ----
            _throttleCurve = new TuneCurvePreview { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 8) };
            _boostCurve = new TuneCurvePreview { Dock = DockStyle.Fill, Margin = new Padding(0, 8, 0, 0) };
            var curves = new TableLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 520,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(12, 12, 16, 12),
                BackColor = SurfaceWindow
            };
            curves.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            curves.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            curves.Controls.Add(_throttleCurve, 0, 0);
            curves.Controls.Add(_boostCurve, 0, 1);

            // ---- Settings table on the left ----
            _scroll = new DarkScrollPanel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = SurfaceWindow };
            _table = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = SurfacePanel,
                Margin = new Padding(0),
                Padding = new Padding(0),
                Location = new Point(16, 0)
            };
            _scroll.Controls.Add(_table);
            _scroll.Resize += (_, _) => SizeRows();

            // ---- Bar above the table ----
            var bar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 52,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(16, 12, 0, 0),
                BackColor = SurfaceWindow
            };
            bar.Controls.Add(new Label
            {
                Text = "Tune comparison",
                AutoSize = true,
                Font = new Font(SystemFonts.DefaultFont.FontFamily, 12f, FontStyle.Bold),
                ForeColor = TextPrimary,
                Margin = new Padding(0, 2, 14, 0)
            });
            _diffBadge = new Label
            {
                AutoSize = true,
                Font = new Font(SystemFonts.DefaultFont.FontFamily, 9f, FontStyle.Bold),
                ForeColor = TextAlert,
                BackColor = Color.FromArgb(0x4A, 0x3A, 0x12),
                Padding = new Padding(8, 4, 8, 4),
                Margin = new Padding(0, 2, 18, 0)
            };
            bar.Controls.Add(_diffBadge);
            _onlyDiffs = new CheckBox
            {
                Text = "Show only differences",
                AutoSize = true,
                ForeColor = TextSecond,
                Margin = new Padding(0, 6, 0, 0)
            };
            _onlyDiffs.CheckedChanged += (_, _) => ApplyRowFilter();
            bar.Controls.Add(_onlyDiffs);

            Controls.Add(_scroll);
            Controls.Add(bar);
            Controls.Add(curves);
            _scroll.BringToFront();

            SetRuns(Array.Empty<TuneRunColumn>());
        }

        /// <summary>Show these runs side by side. Called whenever runs or tunes change.</summary>
        public void SetRuns(IReadOnlyList<TuneRunColumn> columns)
        {
            _columns = columns;
            _radio.Clear();
            foreach (var col in columns.Where(c => c.Slot > 0))
                _radio[col.Slot] = (col.Tune?.Radio ?? new RadioTuneSettings()).Clone();
            RebuildTable();
            UpdateCurves();
        }

        // ============================================================
        // Table
        // ============================================================

        private void RebuildTable()
        {
            _table.SuspendLayout();
            var old = _table.Controls.Cast<Control>().ToList();
            _table.Controls.Clear();
            foreach (var c in old) c.Dispose();
            _rows.Clear();

            if (_columns.Count == 0)
            {
                _table.Controls.Add(MakeEmptyState());
                _table.ResumeLayout(true);
                _diffBadge.Visible = false;
                SizeRows();
                return;
            }

            _table.Controls.Add(MakeHeaderRow());

            if (_columns.Any(c => c.Slot > 0))
            {
                AddRow(TableRow.Group("Radio throttle speed (typed in, saved with the project)"));
                foreach (var (field, label) in RadioRows)
                    AddRow(MakeRadioRow(field, label));
            }

            var tunes = _columns.Select(c => c.Tune).ToList();
            var shownKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (group, rows) in SettingGroups)
            {
                var present = rows.Where(r => tunes.Any(t => t?.Fields.ContainsKey(r.Key) == true)).ToList();
                if (present.Count == 0) continue;
                AddRow(TableRow.Group(group));
                foreach (var (key, label) in present)
                {
                    shownKeys.Add(key);
                    AddRow(MakeValueRow(label, key));
                }
            }

            // Everything else in the .dat file (skips the raw curve tables, which are charted).
            var otherKeys = tunes
                .Where(t => t != null)
                .SelectMany(t => t!.Fields.Keys)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(k => !shownKeys.Contains(k) && !IsRawTable(k))
                .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (otherKeys.Count > 0)
            {
                AddRow(TableRow.Group("Other settings"));
                foreach (var key in otherKeys)
                    AddRow(MakeValueRow(key, key));
            }

            _table.ResumeLayout(true);
            RefreshDiffs();
        }

        private static bool IsRawTable(string key) =>
            key.Contains("65535") || key.Contains("65534");

        private void AddRow(TableRow row)
        {
            _rows.Add(row);
            _table.Controls.Add(row);
        }

        private Control MakeEmptyState()
        {
            var panel = new Panel { Height = 120, BackColor = SurfaceWindow, Margin = new Padding(0) };
            var text = new Label
            {
                Text = "Load a Castle log, then attach its Castle Link .dat tune to compare it here.",
                AutoSize = true,
                ForeColor = TextSecond,
                Location = new Point(0, 12)
            };
            var preview = MakeButton("Preview a tune without a log…");
            preview.Location = new Point(0, 44);
            preview.Size = new Size(220, 30);
            preview.Click += (_, _) => AttachTuneRequested?.Invoke(null);
            panel.Controls.Add(text);
            panel.Controls.Add(preview);
            return panel;
        }

        private Control MakeHeaderRow()
        {
            var header = new TableRow(isHeader: true) { Height = 100 };
            header.SetSetting("Setting");
            foreach (var col in _columns)
            {
                var cell = new Panel { BackColor = SurfaceBar };
                var line = new RunLineSample(Math.Max(0, col.Slot - 1)) { Bounds = new Rectangle(0, 10, 28, 10) };
                var title = new Label
                {
                    Text = col.Slot > 0 ? $"Run {col.Slot}" : "Preview",
                    AutoSize = true,
                    Font = new Font(SystemFonts.DefaultFont.FontFamily, 9f, FontStyle.Bold),
                    ForeColor = TextPrimary,
                    Location = new Point(32, 6)
                };
                string fileText = (string.IsNullOrEmpty(col.LogName) ? "" : col.LogName + "\n")
                    + (col.Tune?.SourceFileName is { Length: > 0 } tuneName ? tuneName : "no tune file");
                var files = new Label
                {
                    Text = fileText,
                    AutoSize = false,
                    AutoEllipsis = true,
                    ForeColor = TextSecond,
                    Location = new Point(0, 28),
                    Size = new Size(200, 32)
                };
                var attach = MakeButton(col.Tune?.SourceFileName is { Length: > 0 } ? "Change tune…" : "Attach tune…");
                attach.Size = new Size(110, 26);
                attach.Location = new Point(0, 64);
                int slot = col.Slot;
                attach.Click += (_, _) => AttachTuneRequested?.Invoke(slot > 0 ? slot : null);
                _toolTip.SetToolTip(files, fileText.Replace("\n", "  ·  "));

                cell.Controls.Add(line);
                cell.Controls.Add(title);
                cell.Controls.Add(files);
                cell.Controls.Add(attach);
                cell.Resize += (_, _) => files.Width = Math.Max(0, cell.Width - 4);
                header.AddCell(cell);
            }
            return header;
        }

        private TableRow MakeValueRow(string label, string key)
        {
            var row = new TableRow(isHeader: false);
            row.SetSetting(label);
            foreach (var col in _columns)
            {
                string value = col.Tune != null && col.Tune.Fields.TryGetValue(key, out var v) ? v : "–";
                var cell = new Label
                {
                    Text = value,
                    AutoSize = false,
                    AutoEllipsis = true,
                    TextAlign = ContentAlignment.MiddleLeft,
                    ForeColor = TextPrimary
                };
                _toolTip.SetToolTip(cell, value);
                row.AddCell(cell, () => cell.Text);
            }
            return row;
        }

        private TableRow MakeRadioRow(RadioField field, string label)
        {
            var row = new TableRow(isHeader: false);
            row.SetSetting(label);
            foreach (var col in _columns)
            {
                if (col.Slot <= 0)
                {
                    row.AddCell(MakeDash(null), () => "–");
                    continue;
                }

                var radio = _radio[col.Slot];
                int mode = Math.Clamp(radio.Mode, 1, 3);
                int slot = col.Slot;

                if (field == RadioField.Mode)
                {
                    var combo = new ComboBox
                    {
                        DropDownStyle = ComboBoxStyle.DropDownList,
                        FlatStyle = FlatStyle.Flat,
                        BackColor = SurfaceInput,
                        ForeColor = TextPrimary,
                        Width = 110
                    };
                    combo.Items.AddRange(new object[] { "Mode 1", "Mode 2", "Mode 3" });
                    combo.SelectedIndex = mode - 1;
                    combo.SelectedIndexChanged += (_, _) => RadioEdited(slot, field, combo.SelectedIndex + 1);
                    row.AddCell(combo, () => combo.Text, fixedWidth: 110);
                    continue;
                }

                if (!RadioFieldApplies(field, mode))
                {
                    row.AddCell(MakeDash($"Not used in Mode {mode}"), () => "–");
                    continue;
                }

                var number = new NumericUpDown
                {
                    Minimum = 0,
                    Maximum = 100,
                    DecimalPlaces = 0,
                    BackColor = SurfaceInput,
                    ForeColor = TextPrimary,
                    BorderStyle = BorderStyle.FixedSingle,
                    Width = 80
                };
                double? current = GetRadio(radio, field, mode);
                number.Value = current.HasValue ? (decimal)Math.Clamp(Math.Round(current.Value), 0, 100) : 0;
                number.ValueChanged += (_, _) => RadioEdited(slot, field, (double)number.Value);
                number.Enter += (_, _) => number.Select(0, number.Text.Length);
                row.AddCell(number, () => $"{number.Value}%", fixedWidth: 80);
            }
            return row;
        }

        private Label MakeDash(string? tooltip)
        {
            var dash = new Label { Text = "–", ForeColor = TextDim, TextAlign = ContentAlignment.MiddleLeft };
            if (tooltip != null) _toolTip.SetToolTip(dash, tooltip);
            return dash;
        }

        private void RadioEdited(int slot, RadioField field, double value)
        {
            if (!_radio.TryGetValue(slot, out var radio)) return;

            if (field == RadioField.Mode)
            {
                radio.Mode = (int)value;
                radio.ThrottleSpeedMode = $"Mode {(int)value}";
            }
            else
            {
                SetRadio(radio, field, Math.Clamp(radio.Mode, 1, 3), value);
            }

            RadioSettingsChanged?.Invoke(slot, radio.Clone());

            // A mode change switches which point rows apply. Rebuild after the combo's own
            // event has finished, since the rebuild disposes the combo.
            if (field != RadioField.Mode)
                RefreshDiffs();
            else if (IsHandleCreated)
                BeginInvoke(new Action(RebuildTable));
            else
                RebuildTable();
        }

        // Points count up from the bottom of the stick: Mode 1 has one speed for the whole
        // travel; Mode 2 adds Point 1 (low zone, high zone); Mode 3 adds Point 2 (low,
        // middle, high). The top zone has no position of its own.
        private static bool RadioFieldApplies(RadioField field, int mode) => field switch
        {
            RadioField.Point1Position => mode >= 2,
            RadioField.Point1Speed => true,
            RadioField.Point2Position => mode == 3,
            RadioField.Point2Speed => mode >= 2,
            RadioField.Point3Speed => mode == 3,
            _ => true
        };

        private static double? GetRadio(RadioTuneSettings r, RadioField field, int mode) => field switch
        {
            RadioField.Point1Position => r.Point1Percent,
            RadioField.Point1Speed => mode == 1 ? r.AllTurnPercent ?? r.AllPercent : r.LowTurnPercent ?? r.LowPercent,
            RadioField.Point2Position => r.Point2Percent,
            RadioField.Point2Speed => mode == 2 ? r.HighTurnPercent ?? r.HighPercent : r.MiddleTurnPercent ?? r.MiddlePercent,
            RadioField.Point3Speed => r.HighTurnPercent ?? r.HighPercent,
            _ => null
        };

        private static void SetRadio(RadioTuneSettings r, RadioField field, int mode, double value)
        {
            switch (field)
            {
                case RadioField.Point1Position: r.Point1Percent = value; break;
                case RadioField.Point1Speed:
                    if (mode == 1) r.AllTurnPercent = value; else r.LowTurnPercent = value;
                    break;
                case RadioField.Point2Position: r.Point2Percent = value; break;
                case RadioField.Point2Speed:
                    if (mode == 2) r.HighTurnPercent = value; else r.MiddleTurnPercent = value;
                    break;
                case RadioField.Point3Speed: r.HighTurnPercent = value; break;
            }
        }

        private void RefreshDiffs()
        {
            int differing = 0;
            foreach (var row in _rows)
                if (row.UpdateDiff()) differing++;

            _diffBadge.Visible = _columns.Count >= 2;
            _diffBadge.Text = differing == 1 ? "1 setting differs" : $"{differing} settings differ";
            ApplyRowFilter();
        }

        private void ApplyRowFilter()
        {
            bool onlyDiffs = _onlyDiffs.Checked;
            _table.SuspendLayout();
            TableRow? currentGroup = null;
            bool groupHasVisible = false;
            foreach (var row in _rows)
            {
                if (row.IsGroup)
                {
                    if (currentGroup != null) currentGroup.Visible = groupHasVisible;
                    currentGroup = row;
                    groupHasVisible = false;
                    continue;
                }
                bool show = !onlyDiffs || row.IsDifferent;
                row.Visible = show;
                groupHasVisible |= show;
            }
            if (currentGroup != null) currentGroup.Visible = groupHasVisible;
            _table.ResumeLayout(true);
            SizeRows();
        }

        private void SizeRows()
        {
            int available = _scroll.ClientSize.Width - 32;
            int columns = Math.Max(1, _columns.Count);
            int valueWidth = Math.Clamp((available - SettingColumnWidth) / columns, 120, MaxValueColumnWidth);
            int width = _columns.Count == 0 ? Math.Max(400, available) : SettingColumnWidth + valueWidth * columns;
            foreach (Control c in _table.Controls)
            {
                if (c is TableRow row) row.ValueColumnWidth = valueWidth;
                c.Width = width;
            }
        }

        // ============================================================
        // Curves
        // ============================================================

        private void UpdateCurves()
        {
            var throttle = new (IReadOnlyList<TuneCurvePoint>? Points, bool Disabled)[3];
            var boost = new (IReadOnlyList<TuneCurvePoint>? Points, bool Disabled)[3];
            foreach (var col in _columns)
            {
                if (col.Tune == null) continue;
                int i = Math.Clamp(col.Slot - 1, 0, 2);
                throttle[i] = (col.Tune.ThrottleCurve, false);
                boost[i] = (col.Tune.BoostByThrottleCurve, !IsYes(col.Tune.BoostModeEnableText, col.Tune.BoostEnabled));
            }
            _throttleCurve.SetCurves("Throttle curve", throttle, "No throttle curve");
            _boostCurve.SetCurves("Boost by throttle (faded = boost off in that tune)", boost, "No boost curve");
        }

        private static bool IsYes(string text, bool fallback) =>
            string.IsNullOrWhiteSpace(text) ? fallback : text.Trim().StartsWith("Y", StringComparison.OrdinalIgnoreCase);

        private static Button MakeButton(string text)
        {
            var b = new Button
            {
                Text = text,
                FlatStyle = FlatStyle.Flat,
                BackColor = AccentBlue,
                ForeColor = Color.White,
                UseVisualStyleBackColor = false,
                TabStop = false
            };
            b.FlatAppearance.BorderColor = AccentBlue;
            return b;
        }

        // ============================================================
        // Nested: one table row (setting name + one cell per run)
        // ============================================================
        private sealed class TableRow : Panel
        {
            private readonly Label _setting;
            private readonly List<(Control Cell, Func<string>? Value, int FixedWidth)> _cells = new();
            private string _settingText = "";
            private int _valueColumnWidth = 160;

            public bool IsGroup { get; private set; }
            public bool IsHeader { get; }
            public bool IsDifferent { get; private set; }

            public int ValueColumnWidth
            {
                get => _valueColumnWidth;
                set { _valueColumnWidth = value; PerformLayout(); }
            }

            public TableRow(bool isHeader)
            {
                IsHeader = isHeader;
                Height = RowHeight;
                Margin = new Padding(0);
                BackColor = isHeader ? SurfaceBar : SurfacePanel;
                _setting = new Label
                {
                    AutoSize = false,
                    AutoEllipsis = true,
                    TextAlign = isHeader ? ContentAlignment.TopLeft : ContentAlignment.MiddleLeft,
                    ForeColor = isHeader ? TextPrimary : TextSecond,
                    Font = new Font(SystemFonts.DefaultFont.FontFamily, 9f, isHeader ? FontStyle.Bold : FontStyle.Regular),
                    Padding = new Padding(10, isHeader ? 8 : 0, 0, 0)
                };
                Controls.Add(_setting);
            }

            public static TableRow Group(string title)
            {
                var row = new TableRow(isHeader: false) { IsGroup = true, Height = 30, BackColor = SurfaceBar };
                row.SetSetting(title);
                row._setting.ForeColor = TextPrimary;
                row._setting.Font = new Font(SystemFonts.DefaultFont.FontFamily, 9f, FontStyle.Bold);
                return row;
            }

            public void SetSetting(string text)
            {
                _settingText = text;
                _setting.Text = text;
            }

            public void AddCell(Control cell, Func<string>? value = null, int fixedWidth = 0)
            {
                _cells.Add((cell, value, fixedWidth));
                Controls.Add(cell);
            }

            /// <summary>Compare this row's cells; tint the row and mark values that differ from run 1.</summary>
            public bool UpdateDiff()
            {
                if (IsGroup || IsHeader) return false;
                var values = _cells.Select(c => c.Value?.Invoke() ?? "").ToList();
                var real = values.Where(v => v != "–").ToList();
                IsDifferent = real.Count >= 2 && real.Distinct().Count() > 1;

                BackColor = IsDifferent ? DiffRow : SurfacePanel;
                _setting.ForeColor = IsDifferent ? TextAlert : TextSecond;
                _setting.Text = (IsDifferent ? "● " : "") + _settingText;
                string first = values.Count > 0 ? values[0] : "";
                for (int i = 0; i < _cells.Count; i++)
                {
                    if (_cells[i].Cell is not Label label || label.Text == "–") continue;
                    bool changed = IsDifferent && i > 0 && values[i] != first;
                    label.ForeColor = changed ? TextAlert : TextPrimary;
                    label.Font = new Font(SystemFonts.DefaultFont.FontFamily, 9f, changed ? FontStyle.Bold : FontStyle.Regular);
                }
                return IsDifferent;
            }

            protected override void OnLayout(LayoutEventArgs levent)
            {
                base.OnLayout(levent);
                if (_setting == null || _cells == null) return; // laid out mid-constructor
                _setting.SetBounds(0, 0, IsGroup ? Width : SettingColumnWidth, Height);
                int x = SettingColumnWidth;
                foreach (var (cell, _, fixedWidth) in _cells)
                {
                    int w = fixedWidth > 0 ? Math.Min(fixedWidth, _valueColumnWidth - 12) : _valueColumnWidth - 12;
                    if (IsHeader)
                        cell.SetBounds(x, 0, _valueColumnWidth - 8, Height);
                    else if (cell is Label)
                        cell.SetBounds(x, 0, w, Height);
                    else
                        cell.SetBounds(x, (Height - cell.Height) / 2, w, cell.Height);
                    x += _valueColumnWidth;
                }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                using var pen = new Pen(IsGroup ? BorderDef : SurfaceBar);
                e.Graphics.DrawLine(pen, 0, Height - 1, Width, Height - 1);
            }
        }

        // ============================================================
        // Nested: run line-style sample (solid / dashed / dotted)
        // ============================================================
        private sealed class RunLineSample : Control
        {
            private readonly int _runIndex;

            public RunLineSample(int runIndex)
            {
                _runIndex = runIndex;
                SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
                BackColor = Color.Transparent;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                using var pen = new Pen(TextPrimary, 2f)
                {
                    DashStyle = (_runIndex % 3) switch { 1 => DashStyle.Dash, 2 => DashStyle.Dot, _ => DashStyle.Solid }
                };
                e.Graphics.DrawLine(pen, 0, Height / 2, Width, Height / 2);
            }
        }

        private sealed class DarkScrollPanel : Panel
        {
            protected override void OnHandleCreated(EventArgs e)
            {
                base.OnHandleCreated(e);
                try { SetWindowTheme(Handle, "DarkMode_Explorer", null); } catch { }
            }

            [System.Runtime.InteropServices.DllImport("uxtheme.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
            private static extern int SetWindowTheme(IntPtr hWnd, string? pszSubAppName, string? pszSubIdList);
        }
    }
}
