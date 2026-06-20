// File: src/CastleOverlayV2/Controls/ChannelDrawer.cs
using CastleOverlayV2.Models;
using CastleOverlayV2.Services;
using CastleOverlayV2.Utils;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace CastleOverlayV2.Controls
{
    /// <summary>
    /// Collapsible channel drawer (Docs/DragOverlay_UI_Spec.md §6.4, §6.5).
    /// Two states:
    ///   - Collapsed: a thin row with a chevron handle + one coloured dot per channel.
    ///   - Expanded:  a wrapping grid of stat cards (●, name, eye toggle, per-run max/avg, @cursor).
    /// Replaces the legacy ChannelToggleBar — same public surface for visibility events.
    /// </summary>
    public class ChannelDrawer : UserControl
    {
        // ---- Theme tokens (Spec §5) -----------------------------------------
        private static readonly Color SurfacePanel    = Color.FromArgb(0x16, 0x1B, 0x23);
        private static readonly Color SurfaceBar      = Color.FromArgb(0x1B, 0x21, 0x2B);
        private static readonly Color SurfaceCard     = Color.FromArgb(0x1A, 0x1F, 0x27);
        private static readonly Color SurfaceCardFocus= Color.FromArgb(0x17, 0x22, 0x30);
        private static readonly Color TextPrimary     = Color.FromArgb(0xE6, 0xE9, 0xEF);
        private static readonly Color TextAccent      = Color.FromArgb(0x9A, 0xC7, 0xF2);
        private static readonly Color TextSecond      = Color.FromArgb(0x9A, 0xA3, 0xB2);
        private static readonly Color TextDim         = Color.FromArgb(0x5C, 0x65, 0x73);
        private static readonly Color BorderDef       = Color.FromArgb(0x29, 0x2F, 0x39);
        private static readonly Color BorderFocus     = Color.FromArgb(0x2C, 0x5A, 0x86);

        // ---- Public surface (matches old ChannelToggleBar) ------------------
        public event Action<string, bool>? ChannelVisibilityChanged;
        public event Action<bool>? RpmModeChanged;
        public event Action<string>? ChannelFocused;

        // ---- State ----------------------------------------------------------
        private bool _expanded = false;
        private string _focusedChannel = "RPM"; // matches PlotManager's initial focus
        private readonly Dictionary<string, ChannelCard> _cards = new();
        private readonly Dictionary<string, ChannelDot> _dots = new();
        private bool _racBoxSeparatorAdded; // visual gap between Castle and RaceBox channels

        // ---- Layout containers ---------------------------------------------
        private readonly Panel _stripRow;       // collapsed view: chevron + dots
        private readonly Panel _expandedHeader; // expanded view: chevron at top
        private readonly FlowLayoutPanel _cardFlow;
        private readonly FlowLayoutPanel _dotFlow;
        private readonly Button _chevronStrip;
        private readonly Button _chevronExpanded;

        public ChannelDrawer(List<string> channelNames, Dictionary<string, bool> initialStates)
        {
            Dock = DockStyle.Bottom;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            BackColor = SurfacePanel;
            ForeColor = TextPrimary;
            Padding = new Padding(0);

            // --- Collapsed strip ---
            _stripRow = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 32,
                BackColor = SurfacePanel,
                Padding = new Padding(0)
            };
            _chevronStrip = MakeChevronButton(expandedNow: false);
            _chevronStrip.Dock = DockStyle.Left;

            _dotFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = true,
                BackColor = SurfacePanel,
                Padding = new Padding(36, 4, 4, 4),
                Margin = new Padding(0)
            };
            _stripRow.Controls.Add(_dotFlow);
            _stripRow.Controls.Add(_chevronStrip);
            _chevronStrip.BringToFront();

            // --- Expanded panel: header bar + wrapping card flow ---
            _expandedHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 24,
                BackColor = SurfaceBar,
                Padding = new Padding(0)
            };
            _chevronExpanded = MakeChevronButton(expandedNow: true);
            _chevronExpanded.Dock = DockStyle.Left;
            _expandedHeader.Controls.Add(_chevronExpanded);

            _cardFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = SurfacePanel,
                Padding = new Padding(8)
            };

            foreach (var ch in channelNames)
            {
                bool visible = initialStates.TryGetValue(ch, out var v) && v;
                AddChannel(ch, visible);
            }

            // Build the initial view (expanded by default) — controls are added in
            // ApplyExpandedState, not here, so the swap is consistent on every toggle.
            ApplyExpandedState();
        }

        // ============================================================
        // Public API (kept compatible with the old ChannelToggleBar)
        // ============================================================

        public bool IsExpanded => _expanded;

        public void AddChannel(string channelName, bool initialVisible)
        {
            if (_cards.ContainsKey(channelName)) return;

            // Group RaceBox channels visually after Castle (spec §6.4).
            bool isRaceBox = channelName.StartsWith("RaceBox", StringComparison.Ordinal);
            if (isRaceBox && !_racBoxSeparatorAdded)
            {
                AddSourceGroupSeparator();
                _racBoxSeparatorAdded = true;
            }

            var card = new ChannelCard(channelName, initialVisible);
            card.VisibilityChanged += (n, v) =>
            {
                _dots[n].SetVisible(v);
                ChannelVisibilityChanged?.Invoke(n, v);
            };
            card.RpmModeToggled += isFourPole => RpmModeChanged?.Invoke(isFourPole);
            card.Focused += n => ChannelFocused?.Invoke(n);
            _cards[channelName] = card;
            _cardFlow.Controls.Add(card);
            if (channelName == _focusedChannel)
                card.SetFocused(true);

            var dot = new ChannelDot(channelName, initialVisible);
            dot.Toggled += (n, v) =>
            {
                _cards[n].SetVisible(v);
                ChannelVisibilityChanged?.Invoke(n, v);
            };
            _dots[channelName] = dot;
            _dotFlow.Controls.Add(dot);
        }

        /// <summary>
        /// Mark <paramref name="channelName"/> as the focused card. Idempotent.
        /// </summary>
        public void SetFocusedChannel(string channelName)
        {
            if (_focusedChannel == channelName) return;
            string previous = _focusedChannel;
            _focusedChannel = channelName;

            if (_cards.TryGetValue(previous, out var prev))
                prev.SetFocused(false);
            if (_cards.TryGetValue(channelName, out var next))
                next.SetFocused(true);
        }

        /// <summary>
        /// Apply visibility states from a project (issue #87 restore path). Only channels
        /// already present in the drawer are updated; unknown keys are ignored safely.
        /// </summary>
        public void ApplyChannelVisibility(IReadOnlyDictionary<string, bool> visibility)
        {
            foreach (var (name, vis) in visibility)
            {
                if (_cards.TryGetValue(name, out var card))
                    card.SetVisible(vis);
                if (_dots.TryGetValue(name, out var dot))
                    dot.SetVisible(vis);
            }
        }

        public Dictionary<string, bool> GetChannelStates() =>
            _cards.ToDictionary(kv => kv.Key, kv => kv.Value.IsChannelVisible);

        /// <summary>Update per-run max/avg numbers on a channel card. No-op if the card doesn't exist.</summary>
        public void UpdateChannelStats(string channelName, IReadOnlyList<ChannelRunStats> stats)
        {
            if (_cards.TryGetValue(channelName, out var card))
                card.SetStats(stats);
        }

        /// <summary>Update per-slot @cursor values on every card.</summary>
        public void UpdateCursorValues(Dictionary<string, double?[]> values)
        {
            if (!_expanded) return;

            foreach (var (name, vals) in values)
                if (_cards.TryGetValue(name, out var card))
                    card.SetCursorValues(vals);
        }

        public void ToggleExpanded()
        {
            _expanded = !_expanded;
            ApplyExpandedState();
        }

        // ============================================================
        // Helpers
        // ============================================================

        private void ApplyExpandedState()
        {
            SuspendLayout();
            Controls.Clear();
            if (_expanded)
            {
                // Card flow first (Dock = Top, AutoSize), then header on top of it.
                Controls.Add(_cardFlow);
                Controls.Add(_expandedHeader);
            }
            else
            {
                Controls.Add(_stripRow);
            }
            ResumeLayout(performLayout: true);
            PerformLayout();
        }

        /// <summary>
        /// Insert a slim vertical separator into both the card grid and the dot strip so the
        /// RaceBox channels read as a set distinct from the Castle channels.
        /// </summary>
        private void AddSourceGroupSeparator()
        {
            var cardSep = new Panel
            {
                Width = 2,
                Height = 80,
                BackColor = BorderDef,
                Margin = new Padding(8, 6, 8, 6)
            };
            _cardFlow.Controls.Add(cardSep);

            var dotSep = new Panel
            {
                Width = 2,
                Height = 18,
                BackColor = BorderDef,
                Margin = new Padding(8, 7, 8, 7)
            };
            _dotFlow.Controls.Add(dotSep);
        }

        private Button MakeChevronButton(bool expandedNow)
        {
            var b = new Button
            {
                Width = 32,
                Text = expandedNow ? "▼" : "▶",
                FlatStyle = FlatStyle.Flat,
                BackColor = SurfaceBar,
                ForeColor = TextSecond,
                Margin = new Padding(0),
                Padding = new Padding(0),
                TabStop = false
            };
            b.FlatAppearance.BorderSize = 0;
            b.Click += (_, _) => ToggleExpanded();
            return b;
        }

        private static Color ChannelHueColor(string channelName)
        {
            var c = ChannelColorMap.GetColor(channelName);
            return Color.FromArgb(c.R, c.G, c.B);
        }

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
                _ => v.ToString("F2"),
            };
        }

        // ============================================================
        // Nested: ChannelCard (expanded view)
        // ============================================================
        private class ChannelCard : Panel
        {
            public string ChannelName { get; }
            public bool IsChannelVisible { get; private set; }
            public bool IsFocused { get; private set; }

            public event Action<string, bool>? VisibilityChanged;
            public event Action<bool>? RpmModeToggled;
            public event Action<string>? Focused;

            private readonly TableLayoutPanel _layout;
            private readonly Label _nameLbl;
            private readonly Button _eyeBtn;
            private readonly Button? _rpmModeBtn;
            private bool _isFourPole;

            // Stats + cursor labels per slot. The cardlets are added as the slots first appear.
            private readonly Dictionary<int, Label> _statLabels = new();
            private readonly Dictionary<int, Label> _cursorLabels = new();
            private double?[] _lastCursor = Array.Empty<double?>();
            private IReadOnlyList<ChannelRunStats> _lastStats = Array.Empty<ChannelRunStats>();

            public ChannelCard(string channelName, bool initialVisible)
            {
                ChannelName = channelName;
                IsChannelVisible = initialVisible;

                Width = 168;
                AutoSize = true;
                AutoSizeMode = AutoSizeMode.GrowOnly;
                MinimumSize = new Size(168, 72);
                MaximumSize = new Size(168, 0);
                BackColor = SurfaceCard;
                ForeColor = TextPrimary;
                Padding = new Padding(8, 6, 8, 6);
                Margin = new Padding(4);
                BorderStyle = BorderStyle.FixedSingle;

                _layout = new TableLayoutPanel
                {
                    Dock = DockStyle.Top,
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    MinimumSize = new Size(150, 0),
                    MaximumSize = new Size(150, 0),
                    ColumnCount = 1,
                    Margin = new Padding(0),
                    Padding = new Padding(0)
                };

                // --- Header row (dot + name + eye toggle) ---
                var header = new TableLayoutPanel
                {
                    Dock = DockStyle.Top,
                    Width = 150,
                    MinimumSize = new Size(150, 24),
                    MaximumSize = new Size(150, 0),
                    ColumnCount = 3,
                    RowCount = 1,
                    Margin = new Padding(0),
                    Padding = new Padding(0),
                    AutoSize = true
                };
                header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 14));
                header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 28));

                var dot = new Panel
                {
                    Width = 10,
                    Height = 10,
                    Margin = new Padding(0, 6, 0, 0),
                    BackColor = ChannelHueColor(channelName)
                };
                header.Controls.Add(dot, 0, 0);
                dot.Click += (_, _) => ToggleVisibility();

                _nameLbl = new Label
                {
                    Text = channelName,
                    AutoSize = true,
                    ForeColor = TextPrimary,
                    Font = new Font(SystemFonts.DefaultFont.FontFamily, 9.5f, FontStyle.Bold),
                    Margin = new Padding(2, 4, 0, 0)
                };
                header.Controls.Add(_nameLbl, 1, 0);

                _eyeBtn = new Button
                {
                    Text = initialVisible ? "●" : "○",
                    Width = 24,
                    Height = 20,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = SurfaceCard,
                    ForeColor = initialVisible ? ChannelHueColor(channelName) : TextDim,
                    Margin = new Padding(0, 2, 0, 0),
                    TabStop = false
                };
                _eyeBtn.FlatAppearance.BorderSize = 0;
                _eyeBtn.Click += (_, _) => ToggleVisibility();
                header.Controls.Add(_eyeBtn, 2, 0);
                header.Click += (_, _) => ToggleVisibility();

                _layout.Controls.Add(header);

                // --- RPM 2P/4P toggle on the RPM card only ---
                if (channelName == "RPM")
                {
                    _rpmModeBtn = new Button
                    {
                        Text = "2 Pole",
                        AutoSize = true,
                        FlatStyle = FlatStyle.Flat,
                        BackColor = SurfaceBar,
                        ForeColor = TextSecond,
                        Margin = new Padding(0, 4, 0, 0),
                        TabStop = false
                    };
                    _rpmModeBtn.FlatAppearance.BorderColor = BorderDef;
                    _rpmModeBtn.Click += (_, _) =>
                    {
                        _isFourPole = !_isFourPole;
                        _rpmModeBtn.Text = _isFourPole ? "4 Pole" : "2 Pole";
                        RpmModeToggled?.Invoke(_isFourPole);
                    };
                    _layout.Controls.Add(_rpmModeBtn);
                }

                Controls.Add(_layout);
                UpdateEyeAppearance();

                // Card body (and the name label) → click focuses this channel.
                // The eye button and RPM-mode button keep their own click handlers
                // (they do NOT propagate to the body click).
                this.Click += (_, _) => ToggleVisibility();
                _nameLbl.Click += (_, _) => ToggleVisibility();
                _layout.Click += (_, _) => ToggleVisibility();
            }

            public void SetFocused(bool focused)
            {
                if (IsFocused == focused) return;
                IsFocused = focused;
                BackColor = focused ? SurfaceCardFocus : SurfaceCard;
                _nameLbl.ForeColor = focused ? TextAccent : TextPrimary;
            }

            public void SetVisible(bool visible)
            {
                if (IsChannelVisible == visible) return;
                IsChannelVisible = visible;
                UpdateEyeAppearance();
            }

            private void ToggleVisibility()
            {
                IsChannelVisible = !IsChannelVisible;
                UpdateEyeAppearance();
                VisibilityChanged?.Invoke(ChannelName, IsChannelVisible);
            }

            private void UpdateEyeAppearance()
            {
                _eyeBtn.Text = IsChannelVisible ? "●" : "○";
                _eyeBtn.ForeColor = IsChannelVisible ? ChannelHueColor(ChannelName) : TextDim;
            }

            public void SetStats(IReadOnlyList<ChannelRunStats> stats)
            {
                _lastStats = stats;
                RebuildStatRows();
            }

            public void SetCursorValues(double?[] values)
            {
                _lastCursor = values;
                RebuildCursorRow();
            }

            private void RebuildStatRows()
            {
                // Remove old stat rows; recreate based on current _lastStats.
                foreach (var lbl in _statLabels.Values)
                    _layout.Controls.Remove(lbl);
                _statLabels.Clear();

                foreach (var s in _lastStats)
                {
                    string txt = $"{s.DisplayLabel}  max {Format(ChannelName, s.Max)}  · avg {Format(ChannelName, s.Avg)}";
                    var lbl = new Label
                    {
                        Text = txt,
                        AutoSize = true,
                        ForeColor = TextSecond,
                        Font = new Font(SystemFonts.DefaultFont.FontFamily, 8.5f),
                        Margin = new Padding(0, 2, 0, 0)
                    };
                    _statLabels[s.Slot] = lbl;
                    lbl.Click += (_, _) => ToggleVisibility();
                    _layout.Controls.Add(lbl);
                }
            }

            private void RebuildCursorRow()
            {
                if (_lastCursor.Length > 0 &&
                    _cursorLabels.Count == _lastCursor.Length + 1)
                {
                    for (int i = 0; i < _lastCursor.Length; i++)
                    {
                        string slotLabel = i switch
                        {
                            0 => "A",
                            1 => "B",
                            2 => "C",
                            _ => $"{i + 1}"
                        };
                        string text = _lastCursor[i].HasValue
                            ? $"{slotLabel}  {Format(ChannelName, _lastCursor[i]!.Value)}"
                            : $"{slotLabel}  -";

                        if (_cursorLabels[i].Text != text)
                            _cursorLabels[i].Text = text;
                    }
                    return;
                }

                // Remove old cursor labels; recreate.
                foreach (var lbl in _cursorLabels.Values)
                    _layout.Controls.Remove(lbl);
                _cursorLabels.Clear();

                if (_lastCursor.Length == 0) return;

                // Header line
                var head = new Label
                {
                    Text = "@cursor",
                    AutoSize = true,
                    ForeColor = TextDim,
                    Font = new Font(SystemFonts.DefaultFont.FontFamily, 8.5f, FontStyle.Italic),
                    Margin = new Padding(0, 6, 0, 0)
                };
                _cursorLabels[-1] = head;
                head.Click += (_, _) => ToggleVisibility();
                _layout.Controls.Add(head);

                // Per-slot values (legacy 3-slot array — A/B/C labelling matches Run 1/2/3 conceptually).
                for (int i = 0; i < _lastCursor.Length; i++)
                {
                    string slotLabel = i switch { 0 => "A", 1 => "B", 2 => "C", _ => $"{i + 1}" };
                    string txt = _lastCursor[i].HasValue
                        ? $"{slotLabel}  {Format(ChannelName, _lastCursor[i]!.Value)}"
                        : $"{slotLabel}  —";
                    var lbl = new Label
                    {
                        Text = txt,
                        AutoSize = true,
                        ForeColor = TextPrimary,
                        Font = new Font(SystemFonts.DefaultFont.FontFamily, 9f),
                        Margin = new Padding(0, 0, 0, 0)
                    };
                    _cursorLabels[i] = lbl;
                    lbl.Click += (_, _) => ToggleVisibility();
                    _layout.Controls.Add(lbl);
                }
            }
        }

        // ============================================================
        // Nested: ChannelDot (collapsed view)
        // ============================================================
        private class ChannelDot : Button
        {
            public string ChannelName { get; }
            public bool IsChannelVisible { get; private set; }

            public event Action<string, bool>? Toggled;

            public ChannelDot(string channelName, bool initialVisible)
            {
                ChannelName = channelName;
                IsChannelVisible = initialVisible;

                AutoSize = false;
                Width = ButtonWidth(channelName);
                Height = 24;
                FlatStyle = FlatStyle.Flat;
                Margin = new Padding(2, 4, 2, 4);
                TabStop = false;
                Text = DisplayLabel(channelName);
                Font = new Font(SystemFonts.DefaultFont.FontFamily, 8f, FontStyle.Bold);
                FlatAppearance.BorderColor = BorderDef;
                FlatAppearance.BorderSize = 1;

                ApplyVisualState();
                Click += (_, _) =>
                {
                    IsChannelVisible = !IsChannelVisible;
                    ApplyVisualState();
                    Toggled?.Invoke(ChannelName, IsChannelVisible);
                };
            }

            public void SetVisible(bool visible)
            {
                if (IsChannelVisible == visible) return;
                IsChannelVisible = visible;
                ApplyVisualState();
            }

            private void ApplyVisualState()
            {
                if (IsChannelVisible)
                {
                    BackColor = ChannelHueColor(ChannelName);
                    ForeColor = UseLightTextOn(BackColor) ? Color.White : Color.Black;
                }
                else
                {
                    BackColor = SurfacePanel;
                    ForeColor = TextDim;
                }
            }

            private static string DisplayLabel(string channelName) => channelName switch
            {
                "RPM" => "RPM",
                "Throttle %" => "Throttle",
                "PowerOut" => "Power Out",
                "MotorTemp" => "Motor Temp",
                "MotorTiming" => "Timing",
                "RaceBox Speed" => "GPS Speed",
                "RaceBox Distance" => "GPS Distance",
                "RaceBox G-Force X" => "GPS G-Force",
                _ => channelName
            };

            private static int ButtonWidth(string channelName)
            {
                string text = DisplayLabel(channelName);
                using var font = new Font(
                    SystemFonts.DefaultFont.FontFamily,
                    8f,
                    FontStyle.Bold);
                return Math.Max(48, TextRenderer.MeasureText(text, font).Width + 18);
            }

            private static bool UseLightTextOn(Color bg)
            {
                int brightness = (bg.R * 299 + bg.G * 587 + bg.B * 114) / 1000;
                return brightness < 130;
            }
        }
    }
}
