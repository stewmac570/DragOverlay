using CastleOverlayV2.Models;
using System.Drawing.Drawing2D;

namespace CastleOverlayV2.Controls
{
    public sealed class TuneCurvePreview : Control
    {
        private readonly List<TuneCurvePoint> _points = new();
        private string _title = "";
        private string _emptyText = "No curve";
        private bool _disabledOverlay;
        private readonly bool _interactive;

        private static readonly Color PanelBack = Color.FromArgb(0x18, 0x1D, 0x25);
        private static readonly Color Grid = Color.FromArgb(0x2A, 0x31, 0x3C);
        private static readonly Color Axis = Color.FromArgb(0x55, 0x60, 0x70);
        private static readonly Color TextMuted = Color.FromArgb(0x9A, 0xA4, 0xB2);
        private static readonly Color AxisNumber = Color.FromArgb(0xCE, 0xD5, 0xE0);
        private static readonly Color Curve = Color.FromArgb(0x4C, 0xA3, 0xFF);

        public TuneCurvePreview()
            : this(interactive: true)
        {
        }

        private TuneCurvePreview(bool interactive)
        {
            _interactive = interactive;
            DoubleBuffered = true;
            Height = 118;
            MinimumSize = new Size(180, 100);
            BackColor = PanelBack;
            ForeColor = Color.FromArgb(0xE6, 0xE9, 0xEF);
            Margin = new Padding(0, 6, 0, 10);
        }

        public void SetCurve(string title, IReadOnlyList<TuneCurvePoint> points, string emptyText, bool disabledOverlay = false)
        {
            _title = title;
            _emptyText = emptyText;
            _disabledOverlay = disabledOverlay;
            _points.Clear();
            _points.AddRange(points);
            Cursor = _interactive && _points.Count >= 2 ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (_interactive && e.Button == MouseButtons.Left && _points.Count >= 2)
                OpenPopout();
        }

        private void OpenPopout()
        {
            var enlarged = new TuneCurvePreview(interactive: false)
            {
                Dock = DockStyle.Fill,
                Font = new Font(Font.FontFamily, 11f)
            };
            enlarged.SetCurve(_title, _points, _emptyText, _disabledOverlay);

            using var window = new Form
            {
                Text = string.IsNullOrWhiteSpace(_title) ? "Tune curve" : _title,
                StartPosition = FormStartPosition.CenterParent,
                BackColor = PanelBack,
                ForeColor = ForeColor,
                ClientSize = new Size(900, 620),
                MinimumSize = new Size(420, 320),
                ShowInTaskbar = false,
                KeyPreview = true
            };
            window.Controls.Add(enlarged);
            window.KeyDown += (_, args) =>
            {
                if (args.KeyCode is Keys.Escape or Keys.Enter)
                    window.Close();
            };
            window.ShowDialog(FindForm());
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(PanelBack);

            using var titleBrush = new SolidBrush(ForeColor);
            using var mutedBrush = new SolidBrush(TextMuted);
            using var numberBrush = new SolidBrush(AxisNumber);
            using var gridPen = new Pen(Grid, 1);
            using var axisPen = new Pen(Axis, 1);
            using var curvePen = new Pen(Curve, 2.5f);
            using var overlayBrush = new SolidBrush(Color.FromArgb(150, 0x18, 0x1D, 0x25));

            g.DrawString(_title, Font, titleBrush, 8, 6);
            DrawPopoutHint(g, mutedBrush);

            float tickSize = Math.Clamp(Height / 48f, 7f, 9.5f);
            using var tickFont = new Font(Font.FontFamily, tickSize, FontStyle.Bold);
            bool showCaptions = Height >= 210 && Width >= 260;
            using var captionFont = new Font(Font.FontFamily, tickSize + 1f);
            using var minorGridPen = new Pen(Color.FromArgb(0x20, 0x26, 0x30), 1);

            int leftPad = showCaptions ? 54 : 38;
            int bottomPad = showCaptions ? 38 : 22;
            int topPad = 28;
            int rightPad = 14;
            var plot = new Rectangle(
                leftPad,
                topPad,
                Math.Max(20, Width - leftPad - rightPad),
                Math.Max(20, Height - topPad - bottomPad));

            // Finest readable label step per axis (labels must not overlap).
            float xLabelWidth = g.MeasureString("100", tickFont).Width;
            float yLabelHeight = tickFont.GetHeight(g);
            int xStep = ChooseStep(plot.Width, xLabelWidth + 8);
            int yStep = ChooseStep(plot.Height, yLabelHeight + 3);

            // Minor gridlines one step finer, for a fine visual reference.
            DrawGridLines(g, minorGridPen, plot, FinerStep(xStep), FinerStep(yStep));
            DrawGridLines(g, gridPen, plot, xStep, yStep);

            g.DrawLine(axisPen, plot.Left, plot.Bottom, plot.Right, plot.Bottom);
            g.DrawLine(axisPen, plot.Left, plot.Top, plot.Left, plot.Bottom);

            DrawAxisNumbers(g, tickFont, numberBrush, plot, xStep, yStep);
            if (showCaptions)
                DrawAxisCaptions(g, captionFont, mutedBrush, plot);

            if (_points.Count < 2)
            {
                DrawCentered(g, _emptyText, mutedBrush, plot);
                return;
            }

            var drawPoints = _points
                .Select(p => new PointF(
                    plot.Left + (float)(Math.Clamp(p.InputPercent, 0, 100) / 100.0 * plot.Width),
                    plot.Bottom - (float)(Math.Clamp(p.OutputPercent, 0, 100) / 100.0 * plot.Height)))
                .ToArray();
            g.DrawLines(curvePen, drawPoints);

            if (_disabledOverlay)
            {
                g.FillRectangle(overlayBrush, plot);
                DrawCentered(g, "Boost disabled in tune", mutedBrush, plot);
            }
        }

        // Candidate "nice" steps that divide 100, finest first.
        private static readonly int[] StepCandidates = { 2, 5, 10, 20, 25, 50, 100 };

        // Finest step whose labels fit within availablePx without overlapping.
        private static int ChooseStep(float availablePx, float labelFootprintPx)
        {
            foreach (int step in StepCandidates)
            {
                int ticks = 100 / step + 1;
                if (availablePx / ticks >= labelFootprintPx)
                    return step;
            }
            return 100;
        }

        // One step finer than the given label step (for minor gridlines).
        private static int FinerStep(int step)
        {
            int index = Array.IndexOf(StepCandidates, step);
            return index > 0 ? StepCandidates[index - 1] : step;
        }

        private static void DrawGridLines(Graphics g, Pen pen, Rectangle plot, int xStep, int yStep)
        {
            for (int v = 0; v <= 100; v += xStep)
            {
                float x = plot.Left + plot.Width * v / 100f;
                g.DrawLine(pen, x, plot.Top, x, plot.Bottom);
            }
            for (int v = 0; v <= 100; v += yStep)
            {
                float y = plot.Bottom - plot.Height * v / 100f;
                g.DrawLine(pen, plot.Left, y, plot.Right, y);
            }
        }

        private void DrawAxisNumbers(Graphics g, Font font, Brush brush, Rectangle plot, int xStep, int yStep)
        {
            for (int value = 0; value <= 100; value += xStep)
            {
                // X axis: 0..100 left-to-right, drawn under the axis.
                float x = plot.Left + plot.Width * value / 100f;
                string xText = value.ToString();
                SizeF xSize = g.MeasureString(xText, font);
                float xPos = Math.Clamp(
                    x - xSize.Width / 2f,
                    plot.Left,
                    plot.Right - xSize.Width);
                g.DrawString(xText, font, brush, xPos, plot.Bottom + 3);
            }

            for (int value = 0; value <= 100; value += yStep)
            {
                // Y axis: 100 at top, 0 at bottom, drawn left of the axis.
                float y = plot.Bottom - plot.Height * value / 100f;
                string yText = value.ToString();
                SizeF ySize = g.MeasureString(yText, font);
                g.DrawString(
                    yText,
                    font,
                    brush,
                    plot.Left - ySize.Width - 4,
                    y - ySize.Height / 2f);
            }
        }

        private void DrawAxisCaptions(Graphics g, Font font, Brush brush, Rectangle plot)
        {
            const string xCaption = "Input %";
            const string yCaption = "Output %";

            SizeF xSize = g.MeasureString(xCaption, font);
            g.DrawString(
                xCaption,
                font,
                brush,
                plot.Left + (plot.Width - xSize.Width) / 2f,
                Height - xSize.Height - 4);

            var state = g.Save();
            g.TranslateTransform(8, plot.Top + plot.Height / 2f);
            g.RotateTransform(-90);
            SizeF ySize = g.MeasureString(yCaption, font);
            g.DrawString(yCaption, font, brush, -ySize.Width / 2f, 0);
            g.Restore(state);
        }

        private void DrawPopoutHint(Graphics g, Brush brush)
        {
            if (!_interactive || _points.Count < 2)
                return;

            const string hint = "⤢ Enlarge";
            SizeF size = g.MeasureString(hint, Font);
            g.DrawString(hint, Font, brush, Width - size.Width - 8, 6);
        }

        private void DrawCentered(Graphics g, string text, Brush brush, Rectangle bounds)
        {
            var size = g.MeasureString(text, Font);
            g.DrawString(
                text,
                Font,
                brush,
                bounds.Left + (bounds.Width - size.Width) / 2f,
                bounds.Top + (bounds.Height - size.Height) / 2f);
        }
    }
}
