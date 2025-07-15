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
            // 👉 Dock at bottom: keeps your toggle bar stuck to the bottom of the form.
            // No need to change this — to adjust gap ABOVE the bar, you tweak the plot.

            AutoSize = false; // ✅ TableLayoutPanel handles sizing

            // ✅ Outer TableLayoutPanel: single row, N columns, equal percent widths
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,  // 👉 This makes the TableLayout fill the ChannelToggleBar area.
                RowCount = 1,
                ColumnCount = channelNames.Count,
                Margin = new Padding(0),
                Padding = new Padding(0)
                // 👉 Keep Padding = 0 to avoid extra space above your channel rows.
                // To create a larger gap BETWEEN the toggle bar and the plot, adjust the plot's Layout(bottom).
            };

            for (int i = 0; i < channelNames.Count; i++)
            {
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / channelNames.Count));
            }

            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            for (int i = 0; i < channelNames.Count; i++)
            {
                var channel = channelNames[i];
                var row = new ChannelRow(channel, initialStates.ContainsKey(channel) && initialStates[channel]);
                row.ToggleChanged += OnToggleChanged;

                // 👉 This controls how each ChannelRow block sits in its TableLayout cell:
                row.Anchor = AnchorStyles.Top;
                // Use AnchorStyles.Top to pin the block to the top of its cell, reducing vertical gap.
                // Use AnchorStyles.None to center vertically instead.

                row.Margin = new Padding(4, 10, 4, 0);
                // 👉 This margin controls space AROUND each block.
                // - Reduce top margin to pull blocks up.
                // - Add bottom margin if you want more gap below each block.

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
                {
                    row.UpdateValues(kvp.Value);
                }
            }
        }

        public Dictionary<string, bool> GetChannelStates()
        {
            return _channelRows.ToDictionary(r => r.Key, r => r.Value.IsVisible);
        }

        /// <summary>
        /// ✅ One channel block with tight vertical stack, bold text, and left spacing
        /// </summary>
        private class ChannelRow : UserControl
        {
            public string ChannelName { get; }
            public bool IsVisible => _toggle.Checked;

            public event Action<string, bool> ToggleChanged;

            private readonly Label[] _valueLabels = new Label[3];
            private readonly CheckBox _toggle;

            public ChannelRow(string channelName, bool initialState)
            {
                ChannelName = channelName;

                AutoSize = true;
                AutoSizeMode = AutoSizeMode.GrowAndShrink;

                Padding = new Padding(2, 2, 5, 2);
                // 👉 This controls INNER padding of each ChannelRow block.
                // - Reduce top Padding to move content up inside its block.
                // - Increase left Padding to shift content right.

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

                // Row 0: Show checkbox
                _toggle = new CheckBox
                {
                    Checked = initialState,
                    Text = "Show",
                    AutoSize = true,
                    Anchor = AnchorStyles.Top,
                    Margin = new Padding(5, 0, 0, 0)
                    // 👉 This margin moves just the checkbox right, so it doesn't hug the left edge.
                };
                _toggle.CheckedChanged += (s, e) => ToggleChanged?.Invoke(ChannelName, _toggle.Checked);
                layout.Controls.Add(_toggle, 0, 0);

                // Row 1: Channel name — bold
                var nameLabel = new Label
                {
                    Text = channelName,
                    TextAlign = ContentAlignment.MiddleCenter,
                    AutoSize = true,
                    Anchor = AnchorStyles.None,
                    Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold)
                };
                layout.Controls.Add(nameLabel, 0, 1);

                // Rows 2–4: Log 1–3 values — bold
                for (int i = 0; i < 3; i++)
                {
                    var lbl = new Label
                    {
                        Text = "—",
                        TextAlign = ContentAlignment.MiddleCenter,
                        AutoSize = true,
                        Anchor = AnchorStyles.None,
                        Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold)
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
