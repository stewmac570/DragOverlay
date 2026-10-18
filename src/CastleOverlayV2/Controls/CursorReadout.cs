using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace CastleOverlayV2.Controls
{
    /// <summary>
    /// Small box that sits beside the chart cursor and moves with it: the time, then one
    /// line per visible channel with its colour and its value for runs 1, 2 and 3.
    /// Mouse events pass through it to the chart underneath.
    /// </summary>
    public sealed class CursorReadout : Control
    {
        public sealed record Line(string Name, Color Color, string[] Values);

        private static readonly Color Back = Color.FromArgb(0x10, 0x14, 0x1B);
        private static readonly Color Border = Color.FromArgb(0x3A, 0x42, 0x50);
        private static readonly Color TextPrimary = Color.FromArgb(0xE6, 0xE9, 0xEF);
        private static readonly Color TextSecond = Color.FromArgb(0x9A, 0xA3, 0xB2);
        private static readonly Color TimeColor = Color.FromArgb(0xF5, 0xC5, 0x42);

        private const int Pad = 8;
        private const int LineHeight = 17;
        private const int NameWidth = 96;
        private const int ValueWidth = 62;
        private const int Offset = 16; // gap between the cursor and the box

        private readonly Font _font = new(SystemFonts.DefaultFont.FontFamily, 8.5f);
        private readonly Font _bold = new(SystemFonts.DefaultFont.FontFamily, 8.5f, FontStyle.Bold);
        private string _time = "";
        private IReadOnlyList<Line> _lines = Array.Empty<Line>();
        private int _columns = 1;

        public CursorReadout()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            SetStyle(ControlStyles.Selectable, false);
            Visible = false;
        }

        /// <summary>Show the box beside the cursor at pixel (<paramref name="x"/>, <paramref name="y"/>) of the chart.</summary>
        public void ShowAt(int x, int y, double timeSeconds, IReadOnlyList<Line> lines)
        {
            if (lines.Count == 0 || Parent == null)
            {
                Visible = false;
                return;
            }

            _time = $"{timeSeconds:0.000} s";
            _lines = lines;
            _columns = 1;
            foreach (var line in lines)
                for (int i = line.Values.Length - 1; i >= 0; i--)
                    if (line.Values[i] != "–") { _columns = Math.Max(_columns, i + 1); break; }

            int width = Pad * 2 + 14 + NameWidth + ValueWidth * _columns;
            int height = Pad * 2 + LineHeight * (lines.Count + 1);

            // Right of the cursor, or left when it would run off the chart.
            int left = x + Offset;
            if (left + width > Parent.ClientSize.Width) left = x - Offset - width;
            int top = Math.Clamp(y - height / 2, 0, Math.Max(0, Parent.ClientSize.Height - height));

            SetBounds(Math.Max(0, left), top, width, height);
            Visible = true;
            BringToFront();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Back);
            using (var pen = new Pen(Border))
                g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);

            int y = Pad;
            TextRenderer.DrawText(g, _time, _bold, new Point(Pad, y), TimeColor);
            for (int i = 0; i < _columns; i++)
            {
                var rect = new Rectangle(Pad + 14 + NameWidth + i * ValueWidth, y, ValueWidth - 4, LineHeight);
                TextRenderer.DrawText(g, $"run {i + 1}", _font, rect, TextSecond, TextFormatFlags.Right);
            }
            y += LineHeight;

            foreach (var line in _lines)
            {
                using (var brush = new SolidBrush(line.Color))
                    g.FillRectangle(brush, Pad, y + 4, 9, 9);
                TextRenderer.DrawText(g, line.Name, _font,
                    new Rectangle(Pad + 14, y, NameWidth, LineHeight), TextPrimary, TextFormatFlags.EndEllipsis);
                for (int i = 0; i < _columns && i < line.Values.Length; i++)
                {
                    var rect = new Rectangle(Pad + 14 + NameWidth + i * ValueWidth, y, ValueWidth - 4, LineHeight);
                    TextRenderer.DrawText(g, line.Values[i], _font, rect, TextPrimary, TextFormatFlags.Right);
                }
                y += LineHeight;
            }
        }

        // Let the mouse fall through to the chart so hover and clicks keep working.
        protected override void WndProc(ref Message m)
        {
            const int WM_NCHITTEST = 0x0084;
            const int HTTRANSPARENT = -1;
            if (m.Msg == WM_NCHITTEST)
            {
                m.Result = (IntPtr)HTTRANSPARENT;
                return;
            }
            base.WndProc(ref m);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _font.Dispose();
                _bold.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
