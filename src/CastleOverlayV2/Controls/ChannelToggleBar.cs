using CastleOverlayV2.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using CastleOverlayV2.Utils;

namespace CastleOverlayV2.Controls
{
    public partial class ChannelToggleBar : UserControl
    {
        public event Action<string, bool> ChannelVisibilityChanged;
        public event Action<bool> RpmModeChanged;

        private readonly Dictionary<string, ChannelRow> _channelRows = new();
        private TableLayoutPanel _layout;

        public ChannelToggleBar(List<string> channelNames, Dictionary<string, bool> initialStates)
        {
            Dock = DockStyle.Bottom;

            // ⬇️ Let the control shrink to content height (frees vertical space for the plot)
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;

            BuildLayout(channelNames, initialStates);
        }

        private void BuildLayout(List<string> channelNames, Dictionary<string, bool> initialStates)
        {
            Controls.Clear();

            _layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = channelNames.Count,
                RowCount = 1, // single row (buttons + values inside each ChannelRow)
                Margin = new Padding(0),
                Padding = new Padding(0),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };

            _channelRows.Clear();

            foreach (var _ in channelNames)
                _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / channelNames.Count));

            foreach (var channel in channelNames)
            {
                bool initialVisible = initialStates.ContainsKey(channel) && initialStates[channel];
                var row = new ChannelRow(channel, initialVisible);
                row.ToggleChanged += OnToggleChanged;

                if (channel == "RPM")
                    row.RpmModeChanged += (is4P) => RpmModeChanged?.Invoke(is4P);

                row.Margin = new Padding(8, 0, 8, 0);
                _layout.Controls.Add(row);
                _channelRows[channel] = row;
            }

            Controls.Add(_layout);
            PerformLayout();
            Refresh();
        }

        private void OnToggleChanged(string channelName, bool isVisible)
        {
            ChannelVisibilityChanged?.Invoke(channelName, isVisible);
        }

        public void UpdateMousePositionValues(Dictionary<string, double?[]> channelValuesAtCursor)
        {
            foreach (var kvp in channelValuesAtCursor)
            {
                if (_channelRows.TryGetValue(kvp.Key, out var row))
                    row.UpdateValues(kvp.Value);
            }
        }

        public Dictionary<string, bool> GetChannelStates()
        {
            return _channelRows.ToDictionary(r => r.Key, r => r.Value.IsVisible);
        }

        public void AddChannel(string channelName, bool initialState)
        {
            Logger.Log($"📛 AddChannel called: {channelName}");

            if (_channelRows.ContainsKey(channelName))
                return;

            Logger.Log($"🆕 ChannelToggleBar.AddChannel(): Injecting {channelName}");

            var row = new ChannelRow(channelName, initialState);
            row.ToggleChanged += OnToggleChanged;

            if (channelName == "RPM")
                row.RpmModeChanged += (is4P) => RpmModeChanged?.Invoke(is4P);

            _channelRows[channelName] = row;

            _layout.ColumnCount += 1;
            _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / _layout.ColumnCount));

            for (int i = 0; i < _layout.ColumnCount; i++)
                _layout.ColumnStyles[i].Width = 100f / _layout.ColumnCount;

            _layout.Controls.Add(row, _layout.ColumnCount - 1, 0);
            _layout.PerformLayout();
            _layout.Refresh();
            PerformLayout();
            Refresh();
        }

        /// <summary>
        /// One vertical toggle block with [ChannelName Button], 3 log values, optional 2P/4P
        /// </summary>
        private class ChannelRow : UserControl
        {
            public string ChannelName { get; }
            public bool IsVisible { get; private set; }

            public event Action<string, bool> ToggleChanged;
            public event Action<bool> RpmModeChanged;

            private readonly Label[] _valueLabels = new Label[3];
            private readonly Button _toggleButton;
            private bool _isFourPole = false;

            public ChannelRow(string channelName, bool initialState)
            {
                ChannelName = channelName;
                IsVisible = initialState;

                AutoSize = true;
                AutoSizeMode = AutoSizeMode.GrowAndShrink;
                Padding = new Padding(2, 2, 5, 2);

                var layout = new TableLayoutPanel
                {
                    ColumnCount = 1,
                    RowCount = 5, // Button + 3 values (+ RPM button optionally bumps to 6)
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    CellBorderStyle = TableLayoutPanelCellBorderStyle.None,
                    Margin = new Padding(0),
                    Padding = new Padding(0)
                };

                // 🔁 Toggle button now shows the CHANNEL NAME (not "Show/Hide")
                _toggleButton = new Button
                {
                    Text = channelName,
                    AutoSize = true,
                    Anchor = AnchorStyles.Top,
                    BackColor = SystemColors.ControlLight,
                    FlatStyle = FlatStyle.Standard,
                    Margin = new Padding(5, 0, 5, 2)
                };
                _toggleButton.Click += (s, e) =>
                {
                    IsVisible = !IsVisible;
                    ApplyChannelColor(ChannelName, IsVisible);
                    ToggleChanged?.Invoke(ChannelName, IsVisible);
                };
                layout.Controls.Add(_toggleButton, 0, 0);

                // 🗑️ Removed the separate name label row to save height

                // 📊 3 value rows (now directly under the button)
                for (int i = 0; i < 3; i++)
                {
                    var lbl = new Label
                    {
                        Text = "—",
                        TextAlign = ContentAlignment.MiddleCenter,
                        AutoSize = true,
                        Anchor = AnchorStyles.None,
                        Font = new Font(SystemFonts.DefaultFont.FontFamily, 10, FontStyle.Bold)
                    };
                    _valueLabels[i] = lbl;
                    layout.Controls.Add(lbl, 0, i + 1); // was i + 2 (due to removed name label)
                }

                // ⚙️ RPM mode toggle (2P/4P) stays, but moves up one row index
                if (channelName == "RPM")
                {
                    layout.RowCount = 6;
                    var rpmModeButton = new Button
                    {
                        Text = "2 Pole",
                        AutoSize = true,
                        Anchor = AnchorStyles.Top,
                        Margin = new Padding(5, 5, 5, 2)
                    };
                    rpmModeButton.Click += (s, e) =>
                    {
                        _isFourPole = !_isFourPole;
                        rpmModeButton.Text = _isFourPole ? "4 Pole" : "2 Pole";
                        RpmModeChanged?.Invoke(_isFourPole);
                    };
                    layout.Controls.Add(rpmModeButton, 0, 4); // was row 5
                }

                ApplyChannelColor(ChannelName, IsVisible);

                Controls.Add(layout);
            }

            private void ApplyChannelColor(string channel, bool visible)
            {
                try
                {
                    var spColor = ChannelColorMap.GetColor(channel);
                    var baseColor = Color.FromArgb(spColor.R, spColor.G, spColor.B);
                    var buttonColor = visible ? baseColor : ControlPaint.Dark(baseColor);
                    var textColor = UseWhiteText(buttonColor) ? Color.White : Color.Black;

                    _toggleButton.BackColor = buttonColor;
                    _toggleButton.ForeColor = textColor;
                }
                catch (Exception ex)
                {
                    Logger.Log($"❌ Color mapping failed for channel: {channel} → {ex.Message}");
                }
            }

            private bool UseWhiteText(Color bg)
            {
                int brightness = (bg.R * 299 + bg.G * 587 + bg.B * 114) / 1000;
                return brightness < 130;
            }

            public void UpdateValues(double?[] values)
            {
                for (int i = 0; i < 3; i++)
                {
                    if (i < values.Length && values[i].HasValue)
                    {
                        if (ChannelName == "RPM")
                            _valueLabels[i].Text = values[i].Value.ToString("N0");
                        else
                            _valueLabels[i].Text = values[i].Value.ToString("F2");
                    }
                    else
                    {
                        _valueLabels[i].Text = "—";
                    }
                }
            }
        }
    }
}
