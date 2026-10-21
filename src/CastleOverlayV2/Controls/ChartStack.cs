using ScottPlot.WinForms;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace CastleOverlayV2.Controls
{
    /// <summary>Hosts one to three plot controls in vertically resizable panes.</summary>
    internal sealed class ChartStack : Panel
    {
        private static readonly Color SurfaceWindow = Color.FromArgb(0x13, 0x17, 0x1E);
        private static readonly Color DividerColor = Color.FromArgb(0x29, 0x2F, 0x39);
        private const int SplitterWidth = 6;

        private readonly FormsPlot[] _plots;
        private Control? _layoutRoot;
        private SplitContainer? _outerSplit;
        private SplitContainer? _innerSplit;
        private double[] _heights = { 1.0 };
        private bool _applyingLayout;

        public ChartStack(FormsPlot firstPlot)
        {
            ArgumentNullException.ThrowIfNull(firstPlot);

            Dock = DockStyle.Fill;
            Margin = Padding.Empty;
            Padding = Padding.Empty;
            BackColor = SurfaceWindow;

            _plots = new[] { firstPlot, NewPlot(), NewPlot() };
            foreach (FormsPlot plot in _plots)
            {
                plot.Dock = DockStyle.Fill;
                plot.Margin = Padding.Empty;
                plot.Padding = Padding.Empty;
            }

            Resize += (_, _) => ApplySplitterDistances();
            SetPaneCount(1);
        }

        public event Action<IReadOnlyList<double>>? PaneHeightsChanged;

        public int PaneCount { get; private set; }
        public IReadOnlyList<FormsPlot> PaneControls => _plots.Take(PaneCount).ToArray();
        public IReadOnlyList<double> PaneHeights => _heights.ToArray();

        public FormsPlot GetPaneControl(int paneIndex) => _plots[paneIndex];

        public void SetPaneCount(int paneCount)
        {
            if (paneCount is < 1 or > 3)
                throw new ArgumentOutOfRangeException(nameof(paneCount));
            if (PaneCount == paneCount && _layoutRoot is not null)
                return;

            PaneCount = paneCount;
            _heights = Enumerable.Repeat(1.0 / paneCount, paneCount).ToArray();
            RebuildLayout();
        }

        public void SetPaneHeights(IEnumerable<double>? heights)
        {
            double[] values = heights?.Take(PaneCount).ToArray() ?? Array.Empty<double>();
            if (values.Length != PaneCount || values.Any(value => !double.IsFinite(value) || value <= 0))
                values = Enumerable.Repeat(1.0 / PaneCount, PaneCount).ToArray();

            double total = values.Sum();
            _heights = values.Select(value => value / total).ToArray();
            ApplySplitterDistances();
        }

        private static FormsPlot NewPlot() => new()
        {
            BackColor = SurfaceWindow
        };

        private void RebuildLayout()
        {
            SuspendLayout();
            foreach (FormsPlot plot in _plots)
                plot.Parent?.Controls.Remove(plot);

            if (_layoutRoot is not null)
            {
                Controls.Remove(_layoutRoot);
                _layoutRoot.Dispose();
            }

            _outerSplit = null;
            _innerSplit = null;
            _layoutRoot = PaneCount switch
            {
                1 => BuildSinglePane(),
                2 => BuildTwoPanes(),
                _ => BuildThreePanes()
            };
            Controls.Add(_layoutRoot);
            _layoutRoot.BringToFront();
            ResumeLayout(true);
            BeginInvokeIfReady(ApplySplitterDistances);
        }

        private Control BuildSinglePane()
        {
            var panel = NewPanel();
            panel.Controls.Add(_plots[0]);
            return panel;
        }

        private Control BuildTwoPanes()
        {
            _outerSplit = NewSplitContainer();
            _outerSplit.Panel1.Controls.Add(_plots[0]);
            _outerSplit.Panel2.Controls.Add(_plots[1]);
            _outerSplit.SplitterMoved += (_, _) => CaptureHeights();
            return _outerSplit;
        }

        private Control BuildThreePanes()
        {
            _outerSplit = NewSplitContainer();
            _innerSplit = NewSplitContainer();
            _outerSplit.Panel1.Controls.Add(_plots[0]);
            _outerSplit.Panel2.Controls.Add(_innerSplit);
            _innerSplit.Panel1.Controls.Add(_plots[1]);
            _innerSplit.Panel2.Controls.Add(_plots[2]);
            _outerSplit.SplitterMoved += (_, _) => CaptureHeights();
            _innerSplit.SplitterMoved += (_, _) => CaptureHeights();
            return _outerSplit;
        }

        private static Panel NewPanel() => new()
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = SurfaceWindow
        };

        private static SplitContainer NewSplitContainer() => new()
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            BorderStyle = BorderStyle.None,
            SplitterWidth = SplitterWidth,
            BackColor = DividerColor,
            Panel1MinSize = 60,
            Panel2MinSize = 60,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        private void ApplySplitterDistances()
        {
            if (_outerSplit is null || _outerSplit.Height <= SplitterWidth)
                return;

            _applyingLayout = true;
            try
            {
                int outerAvailable = _outerSplit.Height - _outerSplit.SplitterWidth;
                if (outerAvailable < _outerSplit.Panel1MinSize + _outerSplit.Panel2MinSize)
                    return;
                _outerSplit.SplitterDistance = SafeDistance(outerAvailable, _heights[0]);

                if (_innerSplit is not null && _innerSplit.Height > SplitterWidth)
                {
                    int innerAvailable = _innerSplit.Height - _innerSplit.SplitterWidth;
                    if (innerAvailable < _innerSplit.Panel1MinSize + _innerSplit.Panel2MinSize)
                        return;
                    double lowerTotal = _heights[1] + _heights[2];
                    _innerSplit.SplitterDistance = SafeDistance(innerAvailable, _heights[1] / lowerTotal);
                }
            }
            finally
            {
                _applyingLayout = false;
            }
        }

        private static int SafeDistance(int available, double ratio)
        {
            int minimum = Math.Min(60, Math.Max(0, available / 2));
            return Math.Clamp((int)Math.Round(available * ratio), minimum, Math.Max(minimum, available - minimum));
        }

        private void CaptureHeights()
        {
            if (_applyingLayout || _outerSplit is null)
                return;

            int outerAvailable = Math.Max(1, _outerSplit.Height - _outerSplit.SplitterWidth);
            double top = (double)_outerSplit.SplitterDistance / outerAvailable;
            if (PaneCount == 2)
            {
                _heights = Normalize(new[] { top, 1.0 - top });
            }
            else if (_innerSplit is not null)
            {
                int innerAvailable = Math.Max(1, _innerSplit.Height - _innerSplit.SplitterWidth);
                double middleShare = (double)_innerSplit.SplitterDistance / innerAvailable;
                double lower = 1.0 - top;
                _heights = Normalize(new[] { top, lower * middleShare, lower * (1.0 - middleShare) });
            }

            PaneHeightsChanged?.Invoke(PaneHeights);
        }

        private static double[] Normalize(IEnumerable<double> values)
        {
            double[] result = values.Select(value => Math.Max(value, 0.001)).ToArray();
            double total = result.Sum();
            return result.Select(value => value / total).ToArray();
        }

        private void BeginInvokeIfReady(Action action)
        {
            if (IsHandleCreated)
                BeginInvoke(action);
            else
                HandleCreated += RunOnce;

            void RunOnce(object? sender, EventArgs e)
            {
                HandleCreated -= RunOnce;
                BeginInvoke(action);
            }
        }
    }
}
