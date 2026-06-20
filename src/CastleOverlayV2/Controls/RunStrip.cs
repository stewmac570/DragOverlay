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
        private static readonly Color SurfaceArmed  = Color.FromArgb(0x15, 0x31, 0x4E);

        // ---- Public events --------------------------------------------------
        public event Action<int>? LoadRequested;   // slot 1..6
        public event Action<int>? ToggleRequested; // slot 1..6
        public event Action<int>? DeleteRequested; // slot 1..6
        public event Action<int>? ArmRequested;    // slot 1..6 (click on chip body)
        public event Action? SettingsRequested;    // gear button

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

            // Gear button docks Right so the chips shift over to make room.
            var gear = new Button
            {
                Text = "⚙",
                Dock = DockStyle.Right,
                Width = 36,
                FlatStyle = FlatStyle.Flat,
                BackColor = SurfaceCard,
                ForeColor = TextPrimary,
                Margin = new Padding(0),
                Padding = new Padding(0),
                Font = new Font(SystemFonts.DefaultFont.FontFamily, 12f, FontStyle.Bold),
                TabStop = false,
                UseVisualStyleBackColor = false
            };
            gear.FlatAppearance.BorderColor = BorderDef;
            gear.FlatAppearance.BorderSize = 1;
            gear.FlatAppearance.MouseOverBackColor = SurfaceBar;
            gear.FlatAppearance.MouseDownBackColor = SurfaceBar;
            gear.Click += (_, _) => SettingsRequested?.Invoke();
            _toolTip.SetToolTip(gear, "Settings");
            Controls.Add(gear);

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
                int capturedSlot = slot;
                bool isCastle = capturedSlot <= 3;
                string emptyLabel = isCastle
                    ? $"Load Castle {capturedSlot}"
                    : $"Load RaceBox {capturedSlot - 3}";
                string sourceTag = isCastle ? "ESC" : "GPS";

                var chip = new SlotChip(capturedSlot, emptyLabel, sourceTag, _toolTip);
                chip.LoadClicked += () => LoadRequested?.Invoke(capturedSlot);
                chip.ToggleClicked += () => ToggleRequested?.Invoke(capturedSlot);
                chip.DeleteClicked += () => DeleteRequested?.Invoke(capturedSlot);
                chip.BodyClicked += () => ArmRequested?.Invoke(capturedSlot);
                _chips[capturedSlot] = chip;
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
        public void SetSlotArmed(int slot, bool armed) => _chips[slot].SetArmed(armed);

        // ====================================================================
        // Nested: one chip per slot
        // ====================================================================
        private class SlotChip : Panel
        {
            public int Slot { get; }

            public event Action? LoadClicked;
            public event Action? ToggleClicked;
            public event Action? DeleteClicked;
            public event Action? BodyClicked;

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
            private bool _isArmed;

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
                    TabStop = false,
                    UseVisualStyleBackColor = false   // honour BackColor on Win11 dark systems
                };
                _loadBtn.FlatAppearance.BorderColor = BorderDef;
                _loadBtn.FlatAppearance.BorderSize = 1;
                _loadBtn.FlatAppearance.MouseOverBackColor = SurfaceCard;
                _loadBtn.FlatAppearance.MouseDownBackColor = SurfaceCard;
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
                    TabStop = false,
                    UseVisualStyleBackColor = false
                };
                _eyeBtn.FlatAppearance.BorderSize = 0;
                _eyeBtn.FlatAppearance.MouseOverBackColor = SurfaceCardDim;
                _eyeBtn.FlatAppearance.MouseDownBackColor = SurfaceCardDim;
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
                    TabStop = false,
                    UseVisualStyleBackColor = false
                };
                _deleteBtn.FlatAppearance.BorderSize = 0;
                _deleteBtn.FlatAppearance.MouseOverBackColor = SurfaceCardDim;
                _deleteBtn.FlatAppearance.MouseDownBackColor = SurfaceCardDim;
                _deleteBtn.Click += (_, _) => DeleteClicked?.Invoke();
                layout.Controls.Add(_deleteBtn, 3, 0);

                _loadedPanel.Controls.Add(layout);
                Controls.Add(_loadedPanel);

                WireBodyClick(_loadedPanel);
                WireBodyClick(layout);
                WireBodyClick(_tagLbl);
                WireBodyClick(_nameLbl);

                ApplyLoadedState();
            }

            public void SetEmpty()
            {
                _isLoaded = false;
                _isVisible = true;
                _isArmed = false;
                _toolTip.SetToolTip(_nameLbl, string.Empty);
                _nameLbl.Text = string.Empty;
                ApplyLoadedState();
                UpdateArmedAppearance();
            }

            public void SetLoaded(string fullPath, bool isVisible)
            {
                _isLoaded = true;
                _isVisible = isVisible;
                _nameLbl.Text = Truncate(Path.GetFileName(fullPath) ?? string.Empty, 22);
                _toolTip.SetToolTip(_nameLbl, fullPath);
                UpdateEyeAppearance();
                ApplyLoadedState();
                UpdateArmedAppearance();
            }

            public void SetToggleState(bool isVisible)
            {
                if (_isVisible == isVisible) return;
                _isVisible = isVisible;
                UpdateEyeAppearance();
            }

            public void SetArmed(bool armed)
            {
                if (_isArmed == armed) return;
                _isArmed = armed;
                UpdateArmedAppearance();
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

            private void UpdateArmedAppearance()
            {
                Color background = _isArmed ? SurfaceArmed : SurfaceCard;
                BackColor = background;
                _loadedPanel.BackColor = background;
                _tagLbl.BackColor = background;
                _nameLbl.BackColor = background;
                _eyeBtn.BackColor = background;
                _deleteBtn.BackColor = background;
                _nameLbl.ForeColor = _isArmed ? TextAccent : TextPrimary;
                BorderStyle = _isArmed ? BorderStyle.FixedSingle : BorderStyle.None;
            }

            private void WireBodyClick(Control control)
            {
                control.Cursor = Cursors.Hand;
                control.Click += (_, _) =>
                {
                    if (_isLoaded)
                        BodyClicked?.Invoke();
                };
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
