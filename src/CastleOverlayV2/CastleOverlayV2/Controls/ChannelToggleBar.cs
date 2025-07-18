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

        private readonly Dictionary<string, ChannelRow> _channelRows = new();

        public ChannelToggleBar(List<string> channelNames, Dictionary<string, bool> initialStates)
        {
            Dock = DockStyle.Bottom;

            AutoSize = false;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 1,
                ColumnCount = channelNames.Count,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };

            for (int i = 0; i < channelNames.Count; i++)
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / channelNames.Count));

            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            for (int i = 0; i < channelNames.Count; i++)
            {
                var channel = channelNames[i];
                var row = new ChannelRow(channel, initialStates.ContainsKey(channel) && initialStates[channel]);
                row.ToggleChanged += OnToggleChanged;

                row.Anchor = AnchorStyles.Top;
                row.Margin = new Padding(4, 10, 4, 0);

                layout.Controls.Add(row, i, 0);
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
        /// One vertical channel block with Show/Hide button, bold labels
        /// </summary>
        private class ChannelRow : UserControl
        {
            public string ChannelName { get; }
            public bool IsVisible { get; private set; }

            public event Action<string, bool> ToggleChanged;

            private readonly Label[] _valueLabels = new Label[3];
            private readonly Button _toggleButton;

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
                    RowCount = 5,
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

                // Row 1: Channel name — bold, large
                var nameLabel = new Label
                {
                    Text = channelName,
                    TextAlign = ContentAlignment.MiddleCenter,
                    AutoSize = true,
                    Anchor = AnchorStyles.None,
                    Font = new Font(SystemFonts.DefaultFont.FontFamily, 10, FontStyle.Bold)
                };
                layout.Controls.Add(nameLabel, 0, 1);

                // Rows 2–4: Log 1–3 values — bold, larger
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

                Controls.Add(layout);
            }

            public void UpdateValues(double?[] values)
            {
                for (int i = 0; i < 3; i++)
                {
                    if (i < values.Length && values[i].HasValue)
                        _valueLabels[i].Text = values[i].Value.ToString("F2");
                    else
                        _valueLabels[i].Text = "—";
                }
            }
        }
    }
}
