// File: src/CastleOverlayV2/Controls/RunStrip.cs
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace CastleOverlayV2.Controls
{
    /// <summary>
    /// Thin top strip of one chip per slot (Docs/DragOverlay_UI_Spec.md §6.2).
    /// Empty slots show a "Load …" button; loaded slots show a chip
    /// (master eye toggle + source tag + truncated name + delete ×).
    ///
    /// Slot map: 1..3 = Castle (ESC), 4..6 = RaceBox (GPS).
    /// </summary>
    public class RunStrip : UserControl
    {
        // ---- Theme tokens ---------------------------------------------------
        private static readonly Color SurfaceBar    = Color.FromArgb(0x1B, 0x21, 0x2B);
        private static readonly Color SurfaceCard   = Color.FromArgb(0x1A, 0x1F, 0x27);
        private static readonly Color SurfaceCardDim= Color.FromArgb(0x13, 0x17, 0x1E);
        private static readonly Color TextPrimary   = Color.FromArgb(0xE6, 0xE9, 0xEF);
        private static readonly Color TextSecond    = Color.FromArgb(0x9A, 0xA3, 0xB2);
        private static readonly Color TextDim       = Color.FromArgb(0x5C, 0x65, 0x73);
        private static readonly Color TextAccent    = Color.FromArgb(0x9A, 0xC7, 0xF2);
        private static readonly Color BorderDef     = Color.FromArgb(0x29, 0x2F, 0x39);

        // ---- Public events --------------------------------------------------
        public event Action<int>? LoadRequested;   // slot 1..6
        public event Action<int>? ToggleRequested; // slot 1..6
        public event Action<int>? DeleteRequested; // slot 1..6

        // ---- State ----------------------------------------------------------
        private readonly SlotChip[] _chips = new SlotChip[7]; // index 1..6
        private readonly FlowLayoutPanel _flow;
        private readonly ToolTip _toolTip;

        public RunStrip()
        {
            Dock = DockStyle.Top;
            Height = 44;
            BackColor = SurfaceBar;
            ForeColor = TextPrimary;
            Padding = new Padding(4, 4, 4, 4);

            _toolTip = new ToolTip { AutoPopDelay = 8000, InitialDelay = 400, ReshowDelay = 200, ShowAlways = true };

            _flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoScroll = false,
                BackColor = SurfaceBar,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            Controls.Add(_flow);

            for (int slot = 1; slot <= 6; slot++)
            {
                bool isCastle = slot <= 3;
                string emptyLabel = isCastle ? $"Load Castle {slot}" : $"Load RaceBox {slot - 3}";
                string sourceTag = isCastle ? "ESC" : "GPS";

                var chip = new SlotChip(slot, emptyLabel, sourceTag, _toolTip);
                chip.LoadClicked += () => LoadRequested?.Invoke(slot);
                chip.ToggleClicked += () => ToggleRequested?.Invoke(slot);
                chip.DeleteClicked += () => DeleteRequested?.Invoke(slot);
                _chips[slot] = chip;
                _flow.Controls.Add(chip);
            }
        }

        // ---- Public API -----------------------------------------------------
        public void SetSlotEmpty(int slot) => _chips[slot].SetEmpty();
        public void SetSlotLoaded(int slot, string fullPath, bool isVisible) =>
            _chips[slot].SetLoaded(fullPath, isVisible);
        public void SetSlotToggleState(int slot, bool isVisible) =>
            _chips[slot].SetToggleState(isVisible);
        public void SetSlotVisible(int slot, bool visible) => _chips[slot].Visible = visible;

        // ====================================================================
        // Nested: one chip per slot
        // ====================================================================
        private class SlotChip : Panel
        {
            public int Slot { get; }

            public event Action? LoadClicked;
            public event Action? ToggleClicked;
            public event Action? DeleteClicked;

            private readonly Button _loadBtn;
            private readonly Panel _loadedPanel;
            private readonly Button _eyeBtn;
            private readonly Label _tagLbl;
            private readonly Label _nameLbl;
            private readonly Button _deleteBtn;
            private readonly ToolTip _toolTip;
            private readonly string _emptyLabel;
            private bool _isVisible = true;
            private bool _isLoaded;

            public SlotChip(int slot, string emptyLabel, string sourceTag, ToolTip toolTip)
            {
                Slot = slot;
                _toolTip = toolTip;
                _emptyLabel = emptyLabel;

                Width = 188;
                Height = 32;
                BackColor = SurfaceCardDim;
                Padding = new Padding(0);
                Margin = new Padding(4, 2, 4, 2);

                // --- "Load …" button (visible when slot empty) ---
                _loadBtn = new Button
                {
                    Dock = DockStyle.Fill,
                    Text = emptyLabel,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = SurfaceCardDim,
                    ForeColor = TextSecond,
                    Font = new Font(SystemFonts.DefaultFont.FontFamily, 8.5f),
                    TabStop = false
                };
                _loadBtn.FlatAppearance.BorderColor = BorderDef;
                _loadBtn.FlatAppearance.BorderSize = 1;
                _loadBtn.Click += (_, _) => LoadClicked?.Invoke();
                Controls.Add(_loadBtn);

                // --- Chip panel (visible when slot loaded) ---
                _loadedPanel = new Panel
                {
                    Dock = DockStyle.Fill,
                    BackColor = SurfaceCard,
                    Padding = new Padding(0)
                };

                var layout = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 4,
                    RowCount = 1,
                    Margin = new Padding(0),
                    Padding = new Padding(0)
                };
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 26)); // eye
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 32)); // tag
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); // name
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 22)); // ×

                _eyeBtn = new Button
                {
                    Text = "●",
                    Dock = DockStyle.Fill,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = SurfaceCard,
                    ForeColor = TextAccent,
                    Margin = new Padding(0),
                    Padding = new Padding(0),
                    Font = new Font(SystemFonts.DefaultFont.FontFamily, 10f),
                    TabStop = false
                };
                _eyeBtn.FlatAppearance.BorderSize = 0;
                _eyeBtn.Click += (_, _) => ToggleClicked?.Invoke();
                layout.Controls.Add(_eyeBtn, 0, 0);

                _tagLbl = new Label
                {
                    Text = sourceTag,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    ForeColor = TextSecond,
                    Font = new Font(SystemFonts.DefaultFont.FontFamily, 7.5f, FontStyle.Bold),
                    Margin = new Padding(0),
                    Padding = new Padding(0)
                };
                layout.Controls.Add(_tagLbl, 1, 0);

                _nameLbl = new Label
                {
                    Text = "",
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft,
                    ForeColor = TextPrimary,
                    Font = new Font(SystemFonts.DefaultFont.FontFamily, 9f),
                    AutoEllipsis = true,
                    Margin = new Padding(0),
                    Padding = new Padding(2, 0, 0, 0)
                };
                layout.Controls.Add(_nameLbl, 2, 0);

                _deleteBtn = new Button
                {
                    Text = "×",
                    Dock = DockStyle.Fill,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = SurfaceCard,
                    ForeColor = TextDim,
                    Margin = new Padding(0),
                    Padding = new Padding(0),
                    Font = new Font(SystemFonts.DefaultFont.FontFamily, 12f, FontStyle.Bold),
                    TabStop = false
                };
                _deleteBtn.FlatAppearance.BorderSize = 0;
                _deleteBtn.Click += (_, _) => DeleteClicked?.Invoke();
                layout.Controls.Add(_deleteBtn, 3, 0);

                _loadedPanel.Controls.Add(layout);
                Controls.Add(_loadedPanel);

                ApplyLoadedState();
            }

            public void SetEmpty()
            {
                _isLoaded = false;
                _isVisible = true;
                _toolTip.SetToolTip(_nameLbl, string.Empty);
                _nameLbl.Text = string.Empty;
                ApplyLoadedState();
            }

            public void SetLoaded(string fullPath, bool isVisible)
            {
                _isLoaded = true;
                _isVisible = isVisible;
                _nameLbl.Text = Truncate(Path.GetFileName(fullPath) ?? string.Empty, 22);
                _toolTip.SetToolTip(_nameLbl, fullPath);
                UpdateEyeAppearance();
                ApplyLoadedState();
            }

            public void SetToggleState(bool isVisible)
            {
                if (_isVisible == isVisible) return;
                _isVisible = isVisible;
                UpdateEyeAppearance();
            }

            private void ApplyLoadedState()
            {
                _loadBtn.Visible = !_isLoaded;
                _loadedPanel.Visible = _isLoaded;
            }

            private void UpdateEyeAppearance()
            {
                _eyeBtn.Text = _isVisible ? "●" : "○";
                _eyeBtn.ForeColor = _isVisible ? TextAccent : TextDim;
            }

            private static string Truncate(string s, int max)
            {
                if (s.Length <= max) return s;
                if (max <= 3) return s.Substring(0, max);
                return s.Substring(0, max - 3) + "...";
            }
        }
    }
}
