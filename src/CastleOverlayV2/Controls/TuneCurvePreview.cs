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

        private static readonly Color PanelBack = Color.FromArgb(0x18, 0x1D, 0x25);
        private static readonly Color Grid = Color.FromArgb(0x2A, 0x31, 0x3C);
        private static readonly Color Axis = Color.FromArgb(0x55, 0x60, 0x70);
        private static readonly Color TextMuted = Color.FromArgb(0x9A, 0xA4, 0xB2);
        private static readonly Color Curve = Color.FromArgb(0x4C, 0xA3, 0xFF);

        public TuneCurvePreview()
        {
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
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(PanelBack);

            using var titleBrush = new SolidBrush(ForeColor);
            using var mutedBrush = new SolidBrush(TextMuted);
            using var gridPen = new Pen(Grid, 1);
            using var axisPen = new Pen(Axis, 1);
            using var curvePen = new Pen(Curve, 2.5f);
            using var overlayBrush = new SolidBrush(Color.FromArgb(150, 0x18, 0x1D, 0x25));

            g.DrawString(_title, Font, titleBrush, 8, 6);

            var plot = new Rectangle(34, 28, Math.Max(20, Width - 46), Math.Max(20, Height - 42));
            for (int i = 0; i <= 4; i++)
            {
                float x = plot.Left + plot.Width * i / 4f;
                float y = plot.Top + plot.Height * i / 4f;
                g.DrawLine(gridPen, x, plot.Top, x, plot.Bottom);
                g.DrawLine(gridPen, plot.Left, y, plot.Right, y);
            }

            g.DrawLine(axisPen, plot.Left, plot.Bottom, plot.Right, plot.Bottom);
            g.DrawLine(axisPen, plot.Left, plot.Top, plot.Left, plot.Bottom);
            g.DrawString("0", Font, mutedBrush, 8, plot.Bottom - 10);
            g.DrawString("100", Font, mutedBrush, 4, plot.Top - 4);

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
