using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace CastleOverlayV2.Controls
{
    public partial class ChannelToggleBar : UserControl
    {
        public event Action<string, bool> ChannelVisibilityChanged;
        public event Action<bool> RpmModeChanged;

        private readonly Dictionary<string, ChannelRow> _channelRows = new();

        public ChannelToggleBar(List<string> channelNames, Dictionary<string, bool> initialStates)
        {
            Dock = DockStyle.Bottom;
            AutoSize = false;
            Height = 130;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = channelNames.Count,
                RowCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };

            for (int i = 0; i < channelNames.Count; i++)
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / channelNames.Count));


            foreach (var channel in channelNames)
            {
                bool initialVisible = initialStates.ContainsKey(channel) && initialStates[channel];
                var row = new ChannelRow(channel, initialVisible);
                row.ToggleChanged += OnToggleChanged;

                if (channel == "RPM")
                    row.RpmModeChanged += (is4P) => RpmModeChanged?.Invoke(is4P);

                row.Margin = new Padding(8, 0, 8, 0);
                layout.Controls.Add(row);
                _channelRows[channel] = row;
            }

            Controls.Add(layout);
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

        /// <summary>
        /// One vertical toggle block with Show/Hide, 3 log values, optional 2P/4P
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
                    RowCount = 6,
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    CellBorderStyle = TableLayoutPanelCellBorderStyle.None,
                    Margin = new Padding(0),
                    Padding = new Padding(0)
                };

                // Row 0: Show/Hide button
                _toggleButton = new Button
                {
                    Text = IsVisible ? "Hide" : "Show",
                    AutoSize = true,
                    Anchor = AnchorStyles.Top,
                    BackColor = SystemColors.ControlLight,
                    FlatStyle = FlatStyle.Standard,
                    Margin = new Padding(5, 0, 5, 2)
                };
                _toggleButton.Click += (s, e) =>
                {
                    IsVisible = !IsVisible;
                    _toggleButton.Text = IsVisible ? "Hide" : "Show";
                    ToggleChanged?.Invoke(ChannelName, IsVisible);
                };
                layout.Controls.Add(_toggleButton, 0, 0);

                // Row 1: Channel name
                var nameLabel = new Label
                {
                    Text = channelName,
                    TextAlign = ContentAlignment.MiddleCenter,
                    AutoSize = true,
                    Anchor = AnchorStyles.None,
                    Font = new Font(SystemFonts.DefaultFont.FontFamily, 10, FontStyle.Bold)
                };
                layout.Controls.Add(nameLabel, 0, 1);

                // Rows 2–4: Log 1–3 values
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
                    layout.Controls.Add(lbl, 0, i + 2);
                }

                // Row 5: RPM toggle only
                if (channelName == "RPM")
                {
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
                    layout.Controls.Add(rpmModeButton, 0, 5);

                    Controls.Add(layout);
                }
            }
            public void UpdateValues(double?[] values)
            {
                for (int i = 0; i < 3; i++)
                {
                    if (i < values.Length && values[i].HasValue)
                    {
                        if (ChannelName == "RPM")
                            _valueLabels[i].Text = values[i].Value.ToString("N0"); // e.g. 64,800
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
