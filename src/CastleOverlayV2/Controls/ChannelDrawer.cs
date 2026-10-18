// File: src/CastleOverlayV2/Controls/ChannelDrawer.cs
using CastleOverlayV2.Models;
using CastleOverlayV2.Utils;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace CastleOverlayV2.Controls
{
    /// <summary>
    /// Channel list down the left of the Logs page (Docs/UI_Layout_Plan.md).
    /// One row per channel, grouped by source (ESC, GPS, Mini Logger). Each row has:
    ///   tick (show on chart) · colour box (pick a colour) · B (bold lines) ·
    ///   ↕ (line size) · name (click to give it the visible Y axis) · value at the
    ///   cursor for runs 1, 2 and 3. Hovering a row shows each run's max and average.
    /// </summary>
    public class ChannelDrawer : UserControl
    {
        // ---- Theme tokens ---------------------------------------------------
        private static readonly Color SurfacePanel = Color.FromArgb(0x16, 0x1B, 0x23);
        private static readonly Color SurfaceBar = Color.FromArgb(0x1B, 0x21, 0x2B);
        private static readonly Color SurfaceRowFocus = Color.FromArgb(0x17, 0x22, 0x30);
        private static readonly Color TextPrimary = Color.FromArgb(0xE6, 0xE9, 0xEF);
        private static readonly Color TextAccent = Color.FromArgb(0x9A, 0xC7, 0xF2);
        private static readonly Color TextSecond = Color.FromArgb(0x9A, 0xA3, 0xB2);
        private static readonly Color TextDim = Color.FromArgb(0x5C, 0x65, 0x73);
        private static readonly Color BorderDef = Color.FromArgb(0x29, 0x2F, 0x39);
        private static readonly Color BorderControl = Color.FromArgb(0x3A, 0x42, 0x50);
        private static readonly Color AccentBlue = Color.FromArgb(0x2D, 0x6C, 0xB5);

        private const int PanelWidth = 400;
        private const int RowHeight = 30;
        private const int ValueColumnWidth = 66;

        // ---- Public surface ---------------------------------------------------
        public event Action<string, bool>? ChannelVisibilityChanged;
        public event Action<bool>? RpmModeChanged;
        public event Action<string>? ChannelFocused;
        /// <summary>Colour picked for a channel; null means back to the default colour.</summary>
        public event Action<string, Color?>? ChannelColorChanged;
        public event Action<string, bool>? ChannelBoldChanged;
        /// <summary>Line size changed: channel, height (0.1..1), position (0 bottom..1 top).</summary>
        public event Action<string, double, double>? ChannelBandChanged;

        // ---- State ----------------------------------------------------------
        private string _focusedChannel = "RPM"; // matches PlotManager's initial focus
        private bool _isFourPole;
        private readonly Dictionary<string, ChannelRow> _rows = new();
        private readonly Dictionary<ChannelGroup, GroupHeader> _groups = new();
        private readonly Dictionary<string, Color> _colors = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _bold = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, (double Height, double Position)> _bands = new(StringComparer.OrdinalIgnoreCase);

        private readonly Panel _scroll;
        private readonly FlowLayoutPanel _list;
        private readonly Button _poleButton;
        private readonly Panel _title;
        private readonly Button _expandButton;
        private const int CollapsedWidth = 30;
        private readonly RunColumnHeader _columns;
        private readonly ToolTip _toolTip;

        public ChannelDrawer(List<string> channelNames, Dictionary<string, bool> initialStates)
        {
            Dock = DockStyle.Left;
            Width = PanelWidth;
            BackColor = SurfacePanel;
            ForeColor = TextPrimary;
            Padding = new Padding(0, 0, 1, 0); // 1px gap shows the border line on the right

            _toolTip = new ToolTip { AutoPopDelay = 15000, InitialDelay = 500, ReshowDelay = 200, ShowAlways = true };

            _scroll = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = SurfacePanel
            };
            _list = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = SurfacePanel,
                Margin = new Padding(0),
                Padding = new Padding(0),
                Location = new Point(0, 0)
            };
            _scroll.Controls.Add(_list);
            _scroll.Resize += (_, _) => SizeRows();
            Controls.Add(_scroll);

            // Column header: run line styles above the three value columns.
            _columns = new RunColumnHeader { Dock = DockStyle.Top, Height = 24 };
            Controls.Add(_columns);

            // Title bar: "Channels" + RPM pole toggle.
            var title = _title = new Panel { Dock = DockStyle.Top, Height = 36, BackColor = SurfacePanel };
            var titleLbl = new Label
            {
                Text = "Channels",
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0),
                ForeColor = TextPrimary,
                Font = new Font(SystemFonts.DefaultFont.FontFamily, 10f, FontStyle.Bold)
            };
            _poleButton = new Button
            {
                Dock = DockStyle.Right,
                Width = 104,
                FlatStyle = FlatStyle.Flat,
                BackColor = SurfaceBar,
                ForeColor = TextSecond,
                TabStop = false,
                UseVisualStyleBackColor = false
            };
            _poleButton.FlatAppearance.BorderColor = BorderDef;
            _poleButton.Click += (_, _) =>
            {
                SetRpmMode(!_isFourPole);
                RpmModeChanged?.Invoke(_isFourPole);
            };
            _toolTip.SetToolTip(_poleButton, "Motor poles for the RPM channel (4 pole halves the value)");
            var collapse = new Button
            {
                Text = "‹",
                Dock = DockStyle.Right,
                Width = 32,
                FlatStyle = FlatStyle.Flat,
                BackColor = SurfacePanel,
                ForeColor = TextSecond,
                Font = new Font(SystemFonts.DefaultFont.FontFamily, 12f, FontStyle.Bold),
                TabStop = false,
                UseVisualStyleBackColor = false
            };
            collapse.FlatAppearance.BorderSize = 0;
            collapse.Click += (_, _) => SetCollapsed(true);
            _toolTip.SetToolTip(collapse, "Hide the channel list");
            title.Controls.Add(titleLbl);
            title.Controls.Add(_poleButton);
            title.Controls.Add(collapse);
            Controls.Add(title);
            SetRpmMode(false);

            // Collapsed: a thin strip down the left edge; click it to bring the list back.
            _expandButton = new VerticalButton
            {
                Text = "›   CHANNELS",
                Dock = DockStyle.Fill,
                Visible = false
            };
            _expandButton.Click += (_, _) => SetCollapsed(false);
            _toolTip.SetToolTip(_expandButton, "Show the channel list");
            Controls.Add(_expandButton);

            foreach (var ch in channelNames)
            {
                bool visible = initialStates.TryGetValue(ch, out var v) && v;
                AddChannel(ch, visible);
            }
        }

        // ============================================================
        // Public API
        // ============================================================

        public void AddChannel(string channelName, bool initialVisible)
        {
            if (_rows.ContainsKey(channelName)) return;

            var group = GroupOf(channelName);
            if (!_groups.TryGetValue(group, out var header))
            {
                header = new GroupHeader(GroupTitle(group));
                header.Click += (_, _) => ToggleGroup(group);
                _groups[group] = header;
            }

            var row = new ChannelRow(channelName, initialVisible, _toolTip);
            row.SetColor(ColorFor(channelName));
            row.SetBold(_bold.Contains(channelName));
            row.SetBandActive(_bands.ContainsKey(channelName));
            row.SetFocused(channelName == _focusedChannel);
            row.VisibilityToggled += (n, v) => ChannelVisibilityChanged?.Invoke(n, v);
            row.NameClicked += n => ChannelFocused?.Invoke(n);
            row.ColorClicked += PickColor;
            row.ColorResetClicked += n =>
            {
                _colors.Remove(n);
                _rows[n].SetColor(ColorFor(n));
                ChannelColorChanged?.Invoke(n, null);
            };
            row.BoldClicked += n =>
            {
                bool bold = !_bold.Contains(n);
                if (bold) _bold.Add(n); else _bold.Remove(n);
                _rows[n].SetBold(bold);
                ChannelBoldChanged?.Invoke(n, bold);
            };
            row.BandClicked += ShowBandPopup;
            _rows[channelName] = row;

            RebuildList();
        }

        /// <summary>Mark <paramref name="channelName"/> as the channel that owns the Y axis.</summary>
        public void SetFocusedChannel(string channelName)
        {
            if (_focusedChannel == channelName) return;
            if (_rows.TryGetValue(_focusedChannel, out var prev)) prev.SetFocused(false);
            _focusedChannel = channelName;
            if (_rows.TryGetValue(channelName, out var next)) next.SetFocused(true);
        }

        /// <summary>Apply visibility states from a project. Unknown channels are ignored.</summary>
        public void ApplyChannelVisibility(IReadOnlyDictionary<string, bool> visibility)
        {
            foreach (var (name, vis) in visibility)
                if (_rows.TryGetValue(name, out var row))
                    row.SetChannelVisible(vis);
        }

        public Dictionary<string, bool> GetChannelStates() =>
            _rows.ToDictionary(kv => kv.Key, kv => kv.Value.IsChannelVisible);

        /// <summary>Per-run max/avg for a channel, shown in the row's hover tip.</summary>
        public void UpdateChannelStats(string channelName, IReadOnlyList<ChannelRunStats> stats)
        {
            if (_rows.TryGetValue(channelName, out var row))
                row.SetStats(stats.Select(s =>
                    $"{s.DisplayLabel}:  max {Format(channelName, s.Max)}   avg {Format(channelName, s.Avg)}"));
        }

        /// <summary>Per-run values at the cursor. Index 0..2 = run 1..3 (or GPS/Logger 1..3).</summary>
        public void UpdateCursorValues(Dictionary<string, double?[]> values)
        {
            foreach (var (name, vals) in values)
                if (_rows.TryGetValue(name, out var row))
                    row.SetValues(vals.Select(v => v.HasValue ? Format(name, v.Value) : "–").ToArray());
        }

        public bool IsCollapsed => _expandButton.Visible;

        /// <summary>Slide the list in to a thin strip (true) or back out to full width.</summary>
        public void SetCollapsed(bool collapsed)
        {
            SuspendLayout();
            _title.Visible = !collapsed;
            _columns.Visible = !collapsed;
            _scroll.Visible = !collapsed;
            _expandButton.Visible = collapsed;
            Width = collapsed ? CollapsedWidth : PanelWidth;
            ResumeLayout(true);
        }

        public void SetRpmMode(bool isFourPole)
        {
            _isFourPole = isFourPole;
            _poleButton.Text = isFourPole ? "RPM: 4 pole" : "RPM: 2 pole";
        }

        /// <summary>Saved colour / bold / line size choices. Also applied to channels added later.</summary>
        public void ApplyDisplaySettings(
            IReadOnlyDictionary<string, Color> colors,
            IEnumerable<string> boldChannels,
            IReadOnlyDictionary<string, (double Height, double Position)> bands)
        {
            foreach (var (name, color) in colors) _colors[name] = color;
            foreach (var name in boldChannels) _bold.Add(name);
            foreach (var (name, band) in bands) _bands[name] = band;

            foreach (var (name, row) in _rows)
            {
                row.SetColor(ColorFor(name));
                row.SetBold(_bold.Contains(name));
                row.SetBandActive(_bands.ContainsKey(name));
            }
        }

        // ============================================================
        // Helpers
        // ============================================================

        private enum ChannelGroup { Esc, Gps, Logger }

        private static ChannelGroup GroupOf(string channelName) =>
            channelName.StartsWith("RaceBox", StringComparison.Ordinal) ? ChannelGroup.Gps
            : channelName.StartsWith("Logger", StringComparison.Ordinal) ? ChannelGroup.Logger
            : ChannelGroup.Esc;

        private static string GroupTitle(ChannelGroup group) => group switch
        {
            ChannelGroup.Gps => "GPS (RaceBox)",
            ChannelGroup.Logger => "Mini Logger",
            _ => "ESC (Castle)"
        };

        private void ToggleGroup(ChannelGroup group)
        {
            var header = _groups[group];
            header.Expanded = !header.Expanded;
            RebuildList();
        }

        private void RebuildList()
        {
            _list.SuspendLayout();
            _list.Controls.Clear();
            foreach (var group in new[] { ChannelGroup.Esc, ChannelGroup.Gps, ChannelGroup.Logger })
            {
                if (!_groups.TryGetValue(group, out var header)) continue;
                var rows = _rows.Values.Where(r => GroupOf(r.ChannelName) == group).ToList();
                header.SetCount(rows.Count);
                _list.Controls.Add(header);
                if (!header.Expanded) continue;
                foreach (var row in rows)
                    _list.Controls.Add(row);
            }
            _list.ResumeLayout(true);
            SizeRows();
        }

        private void SizeRows()
        {
            int width = _scroll.ClientSize.Width;
            foreach (Control c in _list.Controls)
                c.Width = width;
            // Line the column headings up with the values, scrollbar or not.
            if (_columns != null) _columns.RightInset = _scroll.Width - width;
        }

        private Color ColorFor(string channelName)
        {
            if (_colors.TryGetValue(channelName, out var c)) return c;
            var sc = ChannelColorMap.GetColor(channelName);
            return Color.FromArgb(sc.R, sc.G, sc.B);
        }

        private void PickColor(string channelName)
        {
            using var dlg = new ColorDialog { Color = ColorFor(channelName), FullOpen = true, AnyColor = true };
            if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;
            _colors[channelName] = dlg.Color;
            _rows[channelName].SetColor(dlg.Color);
            ChannelColorChanged?.Invoke(channelName, dlg.Color);
        }

        private void ShowBandPopup(string channelName, Control anchor)
        {
            var (height, position) = _bands.TryGetValue(channelName, out var b) ? b : (1.0, 0.0);
            var popup = new BandPopup(DisplayName(channelName), ColorFor(channelName), height, position);
            popup.BandChanged += (h, p) =>
            {
                if (h >= 1.0) _bands.Remove(channelName);
                else _bands[channelName] = (h, p);
                _rows[channelName].SetBandActive(h < 1.0);
                ChannelBandChanged?.Invoke(channelName, h, p);
            };
            popup.Show(anchor, new Point(anchor.Width + 4, 0));
        }

        internal static string DisplayName(string channelName) => channelName switch
        {
            "PowerOut" => "Power Out",
            "MotorTemp" => "Motor Temp",
            "MotorTiming" => "Motor Timing",
            "RaceBox Speed" => "Speed",
            "RaceBox Distance" => "Distance",
            "RaceBox G-Force X" => "G-Force X",
            "Logger Throttle %" => "Throttle %",
            "Logger RPM" => "RPM",
            "Logger Wheel Speed" => "Wheel Speed",
            "Logger Wheel rev/s" => "Wheel rev/s",
            _ => channelName
        };

        private static string Format(string channelName, double v)
        {
            return channelName switch
            {
                "RPM" => v.ToString("N0"),
                "Throttle %" => v.ToString("F0"),
                "PowerOut" => v.ToString("F0"),
                "ESC Temp" => v.ToString("F0"),
                "MotorTemp" => v.ToString("F0"),
                "MotorTiming" => v.ToString("F0"),
                "Voltage" => v.ToString("F2"),
                "Ripple" => v.ToString("F2"),
                "Current" => v.ToString("F1"),
                "Acceleration" => v.ToString("F2"),
                "RaceBox Speed" => v.ToString("F1"),
                "RaceBox Distance" => v.ToString("F0"),
                "RaceBox G-Force X" => v.ToString("F2"),
                "Logger Throttle %" => v.ToString("F0"),
                "Logger RPM" => v.ToString("N0"),
                "Logger Wheel Speed" => v.ToString("F1"),
                "Logger Wheel rev/s" => v.ToString("F1"),
                _ => v.ToString("F2"),
            };
        }

        // ============================================================
        // Nested: run column header (line style per run above the value columns)
        // ============================================================
        private sealed class RunColumnHeader : Control
        {
            private int _rightInset;

            /// <summary>Width taken on the right by the list's scrollbar, if shown.</summary>
            public int RightInset
            {
                get => _rightInset;
                set { if (_rightInset != value) { _rightInset = value; Invalidate(); } }
            }

            public RunColumnHeader()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
                BackColor = SurfacePanel;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.Clear(BackColor);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var font = new Font(SystemFonts.DefaultFont.FontFamily, 8.5f);
                for (int i = 0; i < 3; i++)
                {
                    int right = Width - _rightInset - 6 - (2 - i) * ValueColumnWidth;
                    int left = right - ValueColumnWidth + 8;
                    using var pen = new Pen(TextSecond, 2f) { DashStyle = RunDash(i) };
                    e.Graphics.DrawLine(pen, left + 8, Height / 2, left + 34, Height / 2);
                    TextRenderer.DrawText(e.Graphics, $"{i + 1}", font,
                        new Rectangle(left + 36, 0, right - left - 36, Height), TextSecond,
                        TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
                }
                TextRenderer.DrawText(e.Graphics, "Value at cursor  ·  run", font,
                    new Rectangle(10, 0, 160, Height), TextDim, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
                using var border = new Pen(BorderDef);
                e.Graphics.DrawLine(border, 0, Height - 1, Width, Height - 1);
            }

            private static DashStyle RunDash(int index) => index switch
            {
                1 => DashStyle.Dash,
                2 => DashStyle.Dot,
                _ => DashStyle.Solid
            };
        }

        // ============================================================
        // Nested: the collapsed strip (vertical text)
        // ============================================================
        private sealed class VerticalButton : Button
        {
            public VerticalButton()
            {
                FlatStyle = FlatStyle.Flat;
                BackColor = SurfaceBar;
                ForeColor = TextSecond;
                Cursor = Cursors.Hand;
                TabStop = false;
                UseVisualStyleBackColor = false;
                FlatAppearance.BorderColor = BorderDef;
                Font = new Font(SystemFonts.DefaultFont.FontFamily, 9f, FontStyle.Bold);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.Clear(BackColor);
                var state = e.Graphics.Save();
                e.Graphics.TranslateTransform(Width / 2f + 6, 14);
                e.Graphics.RotateTransform(90);
                using var brush = new SolidBrush(ForeColor);
                e.Graphics.DrawString(Text, Font, brush, 0, 0);
                e.Graphics.Restore(state);
                using var pen = new Pen(BorderDef);
                e.Graphics.DrawLine(pen, Width - 1, 0, Width - 1, Height);
            }
        }

        // ============================================================
        // Nested: group header
        // ============================================================
        private sealed class GroupHeader : Label
        {
            private readonly string _title;
            private int _count;
            private bool _expanded = true;

            public bool Expanded
            {
                get => _expanded;
                set { _expanded = value; UpdateText(); }
            }

            public GroupHeader(string title)
            {
                _title = title;
                AutoSize = false;
                Height = 30;
                Margin = new Padding(0);
                Padding = new Padding(8, 0, 0, 0);
                TextAlign = ContentAlignment.MiddleLeft;
                BackColor = SurfaceBar;
                ForeColor = TextPrimary;
                Font = new Font(SystemFonts.DefaultFont.FontFamily, 9f, FontStyle.Bold);
                Cursor = Cursors.Hand;
                UpdateText();
            }

            public void SetCount(int count)
            {
                _count = count;
                UpdateText();
            }

            private void UpdateText() =>
                Text = $"{(_expanded ? "▾" : "▸")}  {_title}   ({_count})";
        }

        // ============================================================
        // Nested: one channel row
        // ============================================================
        private sealed class ChannelRow : Panel
        {
            public string ChannelName { get; }
            public bool IsChannelVisible { get; private set; }

            public event Action<string, bool>? VisibilityToggled;
            public event Action<string>? NameClicked;
            public event Action<string>? ColorClicked;
            public event Action<string>? ColorResetClicked;
            public event Action<string>? BoldClicked;
            public event Action<string, Control>? BandClicked;

            private readonly SmallToggle _tick;
            private readonly Swatch _swatch;
            private readonly SmallToggle _bold;
            private readonly SmallToggle _band;
            private readonly Label _name;
            private readonly Label[] _values = new Label[3];
            private readonly ToolTip _toolTip;
            private bool _focused;

            public ChannelRow(string channelName, bool initialVisible, ToolTip toolTip)
            {
                ChannelName = channelName;
                IsChannelVisible = initialVisible;
                _toolTip = toolTip;

                Height = RowHeight;
                Margin = new Padding(0);
                BackColor = SurfacePanel;

                _tick = new SmallToggle("✔", showTextWhenOff: false);
                _tick.Click += (_, _) =>
                {
                    SetChannelVisible(!IsChannelVisible);
                    VisibilityToggled?.Invoke(ChannelName, IsChannelVisible);
                };
                toolTip.SetToolTip(_tick, "Show on the chart");

                _swatch = new Swatch();
                _swatch.MouseUp += (_, e) =>
                {
                    if (e.Button == MouseButtons.Right) ColorResetClicked?.Invoke(ChannelName);
                    else if (e.Button == MouseButtons.Left) ColorClicked?.Invoke(ChannelName);
                };
                toolTip.SetToolTip(_swatch, "Click to pick a colour. Right-click for the default colour.");

                _bold = new SmallToggle("B", showTextWhenOff: true);
                _bold.Click += (_, _) => BoldClicked?.Invoke(ChannelName);
                toolTip.SetToolTip(_bold, "Bold line");

                _band = new SmallToggle("↕", showTextWhenOff: true);
                _band.Click += (_, _) => BandClicked?.Invoke(ChannelName, _band);
                toolTip.SetToolTip(_band, "Line size: squeeze this channel into part of the chart");

                _name = new Label
                {
                    Text = DisplayName(channelName),
                    AutoSize = false,
                    AutoEllipsis = true,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Font = new Font(SystemFonts.DefaultFont.FontFamily, 9f),
                    Cursor = Cursors.Hand
                };
                _name.Click += (_, _) => NameClicked?.Invoke(ChannelName);

                for (int i = 0; i < 3; i++)
                {
                    _values[i] = new Label
                    {
                        Text = "",
                        AutoSize = false,
                        TextAlign = ContentAlignment.MiddleRight,
                        Font = new Font(SystemFonts.DefaultFont.FontFamily, 9f)
                    };
                    Controls.Add(_values[i]);
                }

                Controls.Add(_tick);
                Controls.Add(_swatch);
                Controls.Add(_bold);
                Controls.Add(_band);
                Controls.Add(_name);

                SetStats(Array.Empty<string>());
                ApplyVisibleState();
            }

            protected override void OnLayout(LayoutEventArgs levent)
            {
                base.OnLayout(levent);
                if (_name == null || _values[2] == null) return; // laid out mid-constructor, before every child exists
                int y = (Height - 20) / 2;
                _tick.SetBounds(10, y + 1, 18, 18);
                _swatch.SetBounds(34, y + 2, 16, 16);
                _bold.SetBounds(56, y, 22, 20);
                _band.SetBounds(82, y, 22, 20);

                int right = Width - 6;
                for (int i = 2; i >= 0; i--)
                {
                    _values[i].SetBounds(right - ValueColumnWidth, 0, ValueColumnWidth, Height);
                    right -= ValueColumnWidth;
                }
                _name.SetBounds(110, 0, Math.Max(0, right - 112), Height);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                using var pen = new Pen(SurfaceBar);
                e.Graphics.DrawLine(pen, 0, Height - 1, Width, Height - 1);
            }

            public void SetChannelVisible(bool visible)
            {
                IsChannelVisible = visible;
                ApplyVisibleState();
            }

            public void SetFocused(bool focused)
            {
                _focused = focused;
                BackColor = focused ? SurfaceRowFocus : SurfacePanel;
                _name.Font = new Font(SystemFonts.DefaultFont.FontFamily, 9f, focused ? FontStyle.Bold : FontStyle.Regular);
                ApplyVisibleState();
            }

            public void SetColor(Color color) => _swatch.SwatchColor = color;
            public void SetBold(bool bold) => _bold.On = bold;
            public void SetBandActive(bool active) => _band.On = active;

            public void SetValues(string[] values)
            {
                for (int i = 0; i < 3; i++)
                {
                    string text = i < values.Length ? values[i] : "";
                    if (_values[i].Text != text)
                        _values[i].Text = text;
                }
            }

            public void SetStats(IEnumerable<string> lines)
            {
                string tip = DisplayName(ChannelName) + "  (click the name to put its scale on the chart)";
                var list = lines.ToList();
                if (list.Count > 0)
                    tip += Environment.NewLine + string.Join(Environment.NewLine, list);
                _toolTip.SetToolTip(_name, tip);
                foreach (var v in _values) _toolTip.SetToolTip(v, tip);
            }

            private void ApplyVisibleState()
            {
                _tick.On = IsChannelVisible;
                _name.ForeColor = !IsChannelVisible ? TextDim : _focused ? TextAccent : TextPrimary;
                foreach (var v in _values)
                    v.ForeColor = IsChannelVisible ? TextPrimary : TextDim;
            }
        }

        // ============================================================
        // Nested: small square toggle (tick, B, ↕)
        // ============================================================
        private sealed class SmallToggle : Control
        {
            private readonly bool _showTextWhenOff;
            private bool _on;

            public bool On
            {
                get => _on;
                set { _on = value; Invalidate(); }
            }

            public SmallToggle(string text, bool showTextWhenOff)
            {
                Text = text;
                _showTextWhenOff = showTextWhenOff;
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                         ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
                SetStyle(ControlStyles.Selectable, false);
                BackColor = Color.Transparent;
                Cursor = Cursors.Hand;
                Font = new Font(SystemFonts.DefaultFont.FontFamily, 8.5f, FontStyle.Bold);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var rect = new Rectangle(0, 0, Width - 1, Height - 1);
                using var path = RoundedRect(rect, 3);
                if (_on && _showTextWhenOff)
                {
                    using var fill = new SolidBrush(AccentBlue);
                    e.Graphics.FillPath(fill, path);
                }
                using (var pen = new Pen(_on && _showTextWhenOff ? AccentBlue : BorderControl))
                    e.Graphics.DrawPath(pen, path);

                if (_on || _showTextWhenOff)
                {
                    Color fg = _on ? (_showTextWhenOff ? Color.White : TextAccent) : TextDim;
                    TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, fg,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                }
            }
        }

        // ============================================================
        // Nested: colour box
        // ============================================================
        private sealed class Swatch : Control
        {
            private Color _color = Color.Gray;

            public Color SwatchColor
            {
                get => _color;
                set { _color = value; Invalidate(); }
            }

            public Swatch()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                         ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
                SetStyle(ControlStyles.Selectable, false);
                BackColor = Color.Transparent;
                Cursor = Cursors.Hand;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                using var fill = new SolidBrush(_color);
                e.Graphics.FillRectangle(fill, 2, 2, Width - 4, Height - 4);
                using var pen = new Pen(TextDim);
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            }
        }

        private static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        // ============================================================
        // Nested: line size popup (Height + Position sliders)
        // ============================================================
        private sealed class BandPopup : ToolStripDropDown
        {
            public event Action<double, double>? BandChanged;

            private readonly TrackBar _height;
            private readonly TrackBar _position;
            private readonly Label _heightValue;
            private readonly Label _positionValue;

            public BandPopup(string channelTitle, Color channelColor, double height, double position)
            {
                var panel = new Panel { Width = 300, Height = 150, BackColor = SurfaceBar, ForeColor = TextPrimary };

                var title = new Label
                {
                    Text = $"{channelTitle}: line size",
                    AutoSize = false,
                    Bounds = new Rectangle(34, 8, 250, 22),
                    Font = new Font(SystemFonts.DefaultFont.FontFamily, 9.5f, FontStyle.Bold),
                    ForeColor = TextPrimary
                };
                var swatch = new Panel { Bounds = new Rectangle(12, 12, 14, 14), BackColor = channelColor };

                _height = MakeSlider(10, 100, (int)Math.Round(height * 100), new Rectangle(76, 36, 160, 30));
                _position = MakeSlider(0, 100, (int)Math.Round(position * 100), new Rectangle(76, 72, 160, 30));
                _heightValue = MakeValueLabel(new Rectangle(238, 38, 56, 22));
                _positionValue = MakeValueLabel(new Rectangle(238, 74, 56, 22));

                var reset = new Button
                {
                    Text = "Reset to full",
                    Bounds = new Rectangle(188, 112, 100, 28),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = SurfacePanel,
                    ForeColor = TextPrimary,
                    UseVisualStyleBackColor = false
                };
                reset.FlatAppearance.BorderColor = BorderControl;
                reset.Click += (_, _) =>
                {
                    _height.Value = 100;
                    _position.Value = 0;
                };

                panel.Controls.Add(swatch);
                panel.Controls.Add(title);
                panel.Controls.Add(MakeCaption("Height", new Rectangle(12, 40, 64, 22)));
                panel.Controls.Add(MakeCaption("Position", new Rectangle(12, 76, 64, 22)));
                panel.Controls.Add(_height);
                panel.Controls.Add(_position);
                panel.Controls.Add(_heightValue);
                panel.Controls.Add(_positionValue);
                panel.Controls.Add(reset);

                _height.ValueChanged += (_, _) => Changed();
                _position.ValueChanged += (_, _) => Changed();
                UpdateLabels();

                Padding = new Padding(1);
                BackColor = BorderControl;
                Items.Add(new ToolStripControlHost(panel) { Margin = Padding.Empty, Padding = Padding.Empty, AutoSize = false, Size = panel.Size });
            }

            private void Changed()
            {
                UpdateLabels();
                BandChanged?.Invoke(_height.Value / 100.0, _position.Value / 100.0);
            }

            private void UpdateLabels()
            {
                _heightValue.Text = $"{_height.Value}%";
                _position.Enabled = _height.Value < 100;
                _positionValue.Text = _height.Value >= 100 ? "–"
                    : _position.Value <= 5 ? "Bottom"
                    : _position.Value >= 95 ? "Top"
                    : _position.Value is >= 45 and <= 55 ? "Middle"
                    : $"{_position.Value}%";
            }

            private static TrackBar MakeSlider(int min, int max, int value, Rectangle bounds) => new()
            {
                Minimum = min,
                Maximum = max,
                Value = Math.Clamp(value, min, max),
                TickStyle = TickStyle.None,
                SmallChange = 5,
                LargeChange = 10,
                Bounds = bounds,
                BackColor = SurfaceBar
            };

            private static Label MakeCaption(string text, Rectangle bounds) => new()
            {
                Text = text,
                AutoSize = false,
                Bounds = bounds,
                ForeColor = TextSecond,
                TextAlign = ContentAlignment.MiddleLeft
            };

            private static Label MakeValueLabel(Rectangle bounds) => new()
            {
                AutoSize = false,
                Bounds = bounds,
                ForeColor = TextPrimary,
                TextAlign = ContentAlignment.MiddleLeft
            };
        }
    }
}
