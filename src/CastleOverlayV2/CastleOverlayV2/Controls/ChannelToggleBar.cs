using CastleOverlayV2.Controls;
using CastleOverlayV2.Models;
using CastleOverlayV2.Plot;
using CastleOverlayV2.Services;
using ScottPlot.WinForms;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CastleOverlayV2.Controls
{
    public partial class ChannelToggleBar : UserControl
    {
        public class ChannelToggleState
        {
            public string ChannelName { get; set; }
            public bool IsVisible { get; set; }
        }

        public event Action<string, bool> ChannelVisibilityChanged;

        private readonly Dictionary<string, ChannelRow> _channelRows = new();

        public ChannelToggleBar(List<string> channelNames, Dictionary<string, bool> initialStates)
        {
            Dock = DockStyle.Bottom;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;

            var layout = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.TopDown, // ✅ Stack rows vertically
                WrapContents = false // ✅ Do NOT wrap horizontally
            };

            foreach (var channel in channelNames)
            {
                Console.WriteLine($"Building toggle for: {channel}"); // Debug!
                var row = new ChannelRow(channel, initialStates.ContainsKey(channel) && initialStates[channel]);
                row.ToggleChanged += OnToggleChanged;
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
                {
                    row.UpdateValues(kvp.Value);
                }
            }
        }

        public Dictionary<string, bool> GetChannelStates()
        {
            return _channelRows.ToDictionary(r => r.Key, r => r.Value.IsVisible);
        }

        private class ChannelRow : UserControl
        {
            public string ChannelName { get; }
            public bool IsVisible => _toggle.Checked;

            public event Action<string, bool> ToggleChanged;

            private readonly Label _nameLabel;
            private readonly Label[] _valueLabels = new Label[3];
            private readonly CheckBox _toggle;

            public ChannelRow(string channelName, bool initialState)
            {
                ChannelName = channelName;

                AutoSize = true;
                AutoSizeMode = AutoSizeMode.GrowAndShrink;

                var layout = new FlowLayoutPanel
                {
                    AutoSize = true,
                    FlowDirection = FlowDirection.LeftToRight
                };

                _nameLabel = new Label
                {
                    Text = channelName,
                    Width = 100,
                    TextAlign = ContentAlignment.MiddleLeft
                };
                layout.Controls.Add(_nameLabel);

                for (int i = 0; i < 3; i++)
                {
                    var lbl = new Label
                    {
                        Text = "—",
                        Width = 70,
                        TextAlign = ContentAlignment.MiddleCenter
                    };
                    _valueLabels[i] = lbl;
                    layout.Controls.Add(lbl);
                }

                _toggle = new CheckBox
                {
                    Checked = initialState,
                    Text = "Show",
                    AutoSize = true
                };
                _toggle.CheckedChanged += (s, e) => ToggleChanged?.Invoke(ChannelName, _toggle.Checked);
                layout.Controls.Add(_toggle);

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
