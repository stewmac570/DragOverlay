using System;
using System.Drawing;
using System.Windows.Forms;

namespace CastleOverlayV2.Controls
{
    /// <summary>Transient toolbar shown while a run is armed for time alignment.</summary>
    public sealed class AlignmentBar : UserControl
    {
        private static readonly Color SurfaceArmed = Color.FromArgb(0x15, 0x31, 0x4E);
        private static readonly Color TextPrimary = Color.FromArgb(0xE6, 0xE9, 0xEF);
        private static readonly Color TextAccent = Color.FromArgb(0x9A, 0xC7, 0xF2);
        private static readonly Color BorderFocus = Color.FromArgb(0x2C, 0x5A, 0x86);

        private readonly Label _title;
        private readonly Label _offset;
        private readonly ToolTip _toolTip = new() { ShowAlways = true };
        private System.Windows.Forms.Timer? _repeatTimer;
        private Action? _repeatAction;
        private int _repeatTickCount;
        private bool _isDisposing;

        public event Action? AutoRequested;
        public event Action<int>? FineNudgeRequested;
        public event Action<int>? CoarseNudgeRequested;
        public event Action? ResetRequested;
        public event Action? CloseRequested;

        public AlignmentBar()
        {
            Dock = DockStyle.Top;
            Height = 38;
            Visible = false;
            BackColor = SurfaceArmed;
            Padding = new Padding(8, 4, 8, 4);
            _repeatTimer = new System.Windows.Forms.Timer { Interval = 300 };
            _repeatTimer.Tick += (_, _) => RepeatNudge();

            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = SurfaceArmed,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };

            _title = new Label
            {
                AutoSize = false,
                Width = 280,
                Height = 28,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = TextAccent,
                Font = new Font(SystemFonts.DefaultFont.FontFamily, 9f, FontStyle.Bold),
                AutoEllipsis = true,
                Margin = new Padding(0)
            };
            flow.Controls.Add(_title);

            flow.Controls.Add(MakeButton("auto", "Auto-align launch to t = 0", () => AutoRequested?.Invoke(), 48));
            flow.Controls.Add(MakeRepeatButton("<<", "Hold to coarse nudge left (Shift+Left)", () => CoarseNudgeRequested?.Invoke(-1)));
            flow.Controls.Add(MakeRepeatButton("<", "Hold to fine nudge left (Left)", () => FineNudgeRequested?.Invoke(-1)));
            flow.Controls.Add(MakeRepeatButton(">", "Hold to fine nudge right (Right)", () => FineNudgeRequested?.Invoke(+1)));
            flow.Controls.Add(MakeRepeatButton(">>", "Hold to coarse nudge right (Shift+Right)", () => CoarseNudgeRequested?.Invoke(+1)));

            _offset = new Label
            {
                AutoSize = false,
                Width = 116,
                Height = 28,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = TextPrimary,
                Font = new Font(SystemFonts.DefaultFont.FontFamily, 9f),
                Margin = new Padding(8, 0, 4, 0)
            };
            flow.Controls.Add(_offset);

            flow.Controls.Add(MakeButton("reset", "Reset offset to zero", () => ResetRequested?.Invoke(), 52));
            flow.Controls.Add(MakeButton("X", "Disarm alignment (Esc)", () => CloseRequested?.Invoke(), 32));

            Controls.Add(flow);
        }

        public void ShowFor(string fileName, double offsetMs)
        {
            _title.Text = $"aligning: {fileName}";
            SetOffset(offsetMs);
            Visible = true;
            BringToFront();
        }

        public void SetOffset(double offsetMs)
        {
            double seconds = offsetMs / 1000.0;
            _offset.Text = $"offset {seconds:+0.000;-0.000;0.000}s";
        }

        public void HideBar() => Visible = false;

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (!Visible)
                StopRepeat();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _isDisposing = true;
                StopRepeat();
                _repeatTimer?.Dispose();
                _repeatTimer = null;
            }

            base.Dispose(disposing);
        }

        private Button MakeButton(string text, string tooltip, Action click, int width = 34, bool wireClick = true)
        {
            var button = new Button
            {
                Text = text,
                Width = width,
                Height = 28,
                FlatStyle = FlatStyle.Flat,
                BackColor = SurfaceArmed,
                ForeColor = TextPrimary,
                Margin = new Padding(2, 0, 2, 0),
                Padding = new Padding(0),
                TabStop = false
            };
            button.FlatAppearance.BorderColor = BorderFocus;
            button.FlatAppearance.BorderSize = 1;
            if (wireClick)
                button.Click += (_, _) => click();
            _toolTip.SetToolTip(button, tooltip);
            return button;
        }

        private Button MakeRepeatButton(string text, string tooltip, Action action, int width = 34)
        {
            var button = MakeButton(text, tooltip, action, width, wireClick: false);
            button.MouseDown += (_, e) =>
            {
                if (_isDisposing || IsDisposed) return;
                if (e.Button != MouseButtons.Left) return;
                action();
                _repeatAction = action;
                _repeatTickCount = 0;
                if (_repeatTimer is null)
                    return;

                _repeatTimer.Interval = 300;
                _repeatTimer.Start();
            };
            button.MouseUp += (_, _) => StopRepeat();
            button.MouseLeave += (_, _) => StopRepeat();
            return button;
        }

        private void RepeatNudge()
        {
            if (_isDisposing || IsDisposed)
            {
                StopRepeat();
                return;
            }

            if (_repeatAction is null)
            {
                StopRepeat();
                return;
            }

            if (_repeatTimer is null)
            {
                _repeatAction = null;
                _repeatTickCount = 0;
                return;
            }

            _repeatTickCount++;
            if (_repeatTickCount == 1)
                _repeatTimer.Interval = 75;

            _repeatAction();
        }

        private void StopRepeat()
        {
            _repeatTimer?.Stop();
            _repeatAction = null;
            _repeatTickCount = 0;
        }
    }
}
