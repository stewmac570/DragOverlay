using CastleOverlayV2.Models;
using CastleOverlayV2.Services;
using ScottPlot.WinForms;
using System.Globalization;

namespace CastleOverlayV2.Controls;

public sealed record TcSourceRun(int Slot, RunData Run)
{
    public override string ToString()
    {
        // Negative slots are files opened from the TC tab's own Load log button.
        if (Slot < 0) return $"File — {Run.FileName}";
        string kind = RunSlots.IsCastle(Slot) ? "Castle" : "Data Log";
        return $"{kind} {RunSlots.ButtonNumber(Slot)} — {Run.FileName}";
    }
}

/// <summary>TC curve creation, editing, and Mini Logger USB operations.</summary>
public sealed class TcPanel : UserControl
{
    private static readonly Color Window = Color.FromArgb(0x13, 0x17, 0x1E);
    private static readonly Color Card = Color.FromArgb(0x1A, 0x1F, 0x27);
    private static readonly Color Input = Color.FromArgb(0x20, 0x26, 0x31);
    private static readonly Color TextPrimary = Color.FromArgb(0xE6, 0xE9, 0xEF);
    private static readonly Color TextSecond = Color.FromArgb(0x9A, 0xA3, 0xB2);
    private static readonly Color Border = Color.FromArgb(0x29, 0x2F, 0x39);
    private static readonly Color Accent = Color.FromArgb(0x2D, 0x6C, 0xB5);

    private readonly ConfigService _config;
    private readonly Func<IMiniLoggerUsbClient> _usbFactory;
    private readonly ComboBox _source = new();
    private readonly Button _loadLog;
    private readonly List<TcSourceRun> _logRuns = new();
    private readonly List<TcSourceRun> _fileRuns = new();
    private int _nextFileSlot = -1;
    private readonly TextBox _margin = new();
    private readonly ComboBox _marginUnit = new();
    private readonly TextBox _curveEnd = new();
    private readonly Button _makeCurve;
    private readonly Button _readLogger;
    private readonly Button _sendLogger;
    private readonly Button _clearLogger;
    private readonly Button _addPoint;
    private readonly Button _deletePoint;
    private readonly Button _fitChart;
    private readonly Label _launchInfo = new();
    private readonly TextBox _status = new();
    private readonly FormsPlot _chart = new();
    private readonly DataGridView _points = new();
    private IReadOnlyList<TcRpmSample> _rpmTrace = Array.Empty<TcRpmSample>();
    private TcLoggerStatus? _sessionStatus;
    private bool _updatingGrid;
    private bool _updatingSources;
    private bool _busy;
    private int _dragPointIndex = -1;
    private const double PointHitRadiusPixels = 8.0;

    public TcPanel(ConfigService config, Func<IMiniLoggerUsbClient>? usbFactory = null)
    {
        _config = config;
        _usbFactory = usbFactory ?? (() => new MiniLoggerUsb());
        Dock = DockStyle.Fill;
        BackColor = Window;
        ForeColor = TextPrimary;
        Padding = new Padding(14);

        var top = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 76,
            AutoSize = false,
            WrapContents = true,
            BackColor = Card,
            Padding = new Padding(12, 10, 12, 8)
        };
        top.Controls.Add(Field("Source run", _source, 250));
        _loadLog = MakeButton("Load log…", accent: false, 90);
        _loadLog.Margin = new Padding(4, 20, 12, 0);
        _loadLog.Click += (_, _) => LoadLogFromFile();
        top.Controls.Add(_loadLog);
        top.Controls.Add(Field("Margin", _margin, 90));
        top.Controls.Add(Field("Unit", _marginUnit, 74));
        top.Controls.Add(Field("Curve end (s)", _curveEnd, 95));
        _makeCurve = MakeButton("Make curve", accent: true, 108);
        _makeCurve.Margin = new Padding(10, 20, 4, 0);
        _makeCurve.Click += (_, _) => MakeCurve();
        top.Controls.Add(_makeCurve);

        _source.DropDownStyle = ComboBoxStyle.DropDownList;
        _source.SelectedIndexChanged += (_, _) => SourceChanged();
        _marginUnit.DropDownStyle = ComboBoxStyle.DropDownList;
        _marginUnit.Items.AddRange(new object[] { "RPM", "%" });
        _marginUnit.SelectedIndex = _config.Config.TcCurveMarginIsPercent == true ? 1 : 0;
        _marginUnit.SelectedIndexChanged += (_, _) => InputsChanged();
        _margin.Text = _config.Config.TcCurveMargin?.ToString("0.###", CultureInfo.InvariantCulture) ?? "";
        _curveEnd.Text = _config.Config.TcCurveEndSeconds?.ToString("0.###", CultureInfo.InvariantCulture) ?? "";
        _margin.TextChanged += (_, _) => InputsChanged();
        _curveEnd.TextChanged += (_, _) => InputsChanged();

        _launchInfo.Dock = DockStyle.Top;
        _launchInfo.Height = 48;
        _launchInfo.Padding = new Padding(4, 8, 4, 0);
        _launchInfo.ForeColor = TextSecond;
        _launchInfo.Text = "Click Load log… to open a Castle or data log, or load one on the Logs page.";

        ConfigureChart();
        ConfigureGrid();
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Width = 1200,
            Orientation = Orientation.Vertical,
            SplitterWidth = 6,
            SplitterDistance = 760,
            BackColor = Border,
            Panel1MinSize = 360,
            Panel2MinSize = 280
        };
        var chartFooter = new Panel { Dock = DockStyle.Bottom, Height = 32, BackColor = Card };
        var chartHint = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Click to add a dot · drag a dot to move it · right-click a dot to delete it",
            ForeColor = TextSecond,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 0, 0)
        };
        _fitChart = MakeButton("Fit", false, 54);
        _fitChart.Dock = DockStyle.Right;
        _fitChart.Height = 28;
        _fitChart.Margin = new Padding(2);
        _fitChart.Click += (_, _) => FitChart();
        chartFooter.Controls.Add(chartHint);
        chartFooter.Controls.Add(_fitChart);
        split.Panel1.Controls.Add(_chart);
        split.Panel1.Controls.Add(chartFooter);

        var pointButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 42,
            BackColor = Card,
            Padding = new Padding(6)
        };
        _addPoint = MakeButton("Add point", false, 92);
        _deletePoint = MakeButton("Delete row", false, 96);
        _addPoint.Click += (_, _) => AddPoint();
        _deletePoint.Click += (_, _) => DeletePoints();
        pointButtons.Controls.Add(_addPoint);
        pointButtons.Controls.Add(_deletePoint);
        split.Panel2.Controls.Add(_points);
        split.Panel2.Controls.Add(pointButtons);

        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 136, BackColor = Card, Padding = new Padding(10) };
        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 38,
            BackColor = Card,
            WrapContents = false
        };
        _readLogger = MakeButton("Read logger", false, 104);
        _sendLogger = MakeButton("Send to logger", true, 118);
        _clearLogger = MakeButton("Clear logger curve", false, 138);
        _readLogger.Click += async (_, _) => await ReadLoggerAsync();
        _sendLogger.Click += async (_, _) => await SendCurveAsync();
        _clearLogger.Click += async (_, _) => await ClearCurveAsync();
        actions.Controls.Add(_readLogger);
        actions.Controls.Add(_sendLogger);
        actions.Controls.Add(_clearLogger);

        _status.Dock = DockStyle.Fill;
        _status.Multiline = true;
        _status.ReadOnly = true;
        _status.ScrollBars = ScrollBars.Vertical;
        _status.BackColor = Input;
        _status.ForeColor = TextPrimary;
        _status.BorderStyle = BorderStyle.FixedSingle;
        _status.Text = "Read logger shows its TC state and settings here.";
        bottom.Controls.Add(_status);
        bottom.Controls.Add(actions);

        Controls.Add(split);
        Controls.Add(bottom);
        Controls.Add(_launchInfo);
        Controls.Add(top);
        UpdateEnabledState();
    }

    /// <summary>Runs loaded on the Logs page. Files opened with Load log stay in the list too.</summary>
    public void SetRuns(IEnumerable<TcSourceRun> runs)
    {
        _logRuns.Clear();
        _logRuns.AddRange(runs);
        RefreshSources(selectSlot: null);
    }

    private void RefreshSources(int? selectSlot)
    {
        TcSourceRun? previous = _source.SelectedItem as TcSourceRun;
        int selectedSlot = selectSlot ?? previous?.Slot ?? int.MinValue;
        var items = _fileRuns.Concat(_logRuns.OrderBy(item => item.Slot)).ToList();
        _updatingSources = true;
        _source.BeginUpdate();
        _source.Items.Clear();
        foreach (TcSourceRun item in items) _source.Items.Add(item);
        _source.EndUpdate();
        int selectedIndex = items.FindIndex(item => item.Slot == selectedSlot);
        _source.SelectedIndex = selectedIndex >= 0 ? selectedIndex : (items.Count > 0 ? 0 : -1);
        _updatingSources = false;

        TcSourceRun? current = _source.SelectedItem as TcSourceRun;
        if (current is null || previous is null || current.Slot != previous.Slot ||
            !ReferenceEquals(current.Run, previous.Run))
        {
            _rpmTrace = Array.Empty<TcRpmSample>();
            SetCurvePoints(Array.Empty<TcCurvePoint>());
            _launchInfo.Text = current is null
                ? "Click Load log… to open a Castle or data log, or load one on the Logs page."
                : "Enter a margin and curve end, then make the curve.";
        }
        UpdateEnabledState();
    }

    /// <summary>Opens a Castle or Mini Logger CSV straight into the source list (not into a Logs slot).</summary>
    private void LoadLogFromFile()
    {
        string folder = _config.Config.CastleLogDirectory ?? "";
        using var ofd = new OpenFileDialog
        {
            Title = "Open a Castle or data log to build the curve from",
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            InitialDirectory = Directory.Exists(folder) ? folder : ""
        };
        if (ofd.ShowDialog(this) == DialogResult.OK)
            AddLogFile(ofd.FileName);
    }

    private void AddLogFile(string path)
    {
        LoadResult<RunData> result;
        try
        {
            Cursor = Cursors.WaitCursor;
            result = MiniLoggerLoader.IsMiniLoggerCsv(path)
                ? MiniLoggerLoader.Load(path, keepFullLog: true)
                : new CsvLoader(_config).Load(path, trimForDrag: true, keepFullLog: true);
        }
        catch (Exception ex)
        {
            result = LoadResult<RunData>.Error("Load log", ex.Message);
        }
        finally
        {
            Cursor = Cursors.Default;
        }

        if (!result.Ok || result.Value is null)
        {
            MessageBox.Show(this, result.Message ?? "The log could not be loaded.", result.Title ?? "Load log",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var item = new TcSourceRun(_nextFileSlot--, result.Value);
        _fileRuns.Insert(0, item);
        RefreshSources(item.Slot);
        _status.Text = $"Loaded {result.Value.FileName}. Enter a margin and curve end, then Make curve.";
    }

    private void SourceChanged()
    {
        if (_updatingSources) return;
        _rpmTrace = Array.Empty<TcRpmSample>();
        SetCurvePoints(Array.Empty<TcCurvePoint>());
        _launchInfo.Text = _source.SelectedItem is TcSourceRun
            ? "Enter a margin and curve end, then make the curve."
            : "Click Load log… to open a Castle or data log, or load one on the Logs page.";
        UpdateEnabledState();
    }

    private void InputsChanged()
    {
        if (TryPositiveOrZero(_margin.Text, out double margin))
        {
            _config.Config.TcCurveMargin = margin;
            _config.Config.TcCurveMarginIsPercent = _marginUnit.SelectedIndex == 1;
            _config.Save();
        }
        if (TryPositive(_curveEnd.Text, out double end))
        {
            _config.Config.TcCurveEndSeconds = end;
            _config.Save();
        }
        UpdateEnabledState();
    }

    private void MakeCurve()
    {
        if (_source.SelectedItem is not TcSourceRun source ||
            !TryPositiveOrZero(_margin.Text, out double margin) ||
            !TryPositive(_curveEnd.Text, out double end))
            return;

        try
        {
            TcCurveBuildResult result = TcCurveBuilder.Build(
                source.Run,
                margin,
                _marginUnit.SelectedIndex == 1 ? TcMarginUnit.Percent : TcMarginUnit.Rpm,
                end,
                _sessionStatus,
                _config.Config.IsFourPoleMode);
            _rpmTrace = result.RpmTrace;
            SetCurvePoints(result.Points, fitView: true);
            string rpmMode = source.Run.IsMiniLogger
                ? "RPM: logger motor RPM (pole setting does not apply)."
                : $"Castle RPM: {(_config.Config.IsFourPoleMode ? "4-pole (halved once)" : "2-pole (as logged)")}.";
            _launchInfo.Text = result.Launch.Description + " " + rpmMode;
            _status.Text = $"Curve made: {result.Points.Count} points. Edit the list before sending if needed.";
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or OverflowException)
        {
            MessageBox.Show(ex.Message, "Make TC curve", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task ReadLoggerAsync()
    {
        if (!ConfirmUsbSafety()) return;
        SetBusy(true, "Looking for the Mini Logger…");
        try
        {
            TcLoggerStatus status = await Task.Run(() =>
            {
                using IMiniLoggerUsbClient usb = _usbFactory();
                EnsureConnected(usb);
                return usb.ReadTcStatus();
            });
            if (!status.IsTcLogger)
            {
                _sessionStatus = null;
                _status.Text = "This is a plain Mini Logger, not a TC logger (tc status returned Commands:).";
                return;
            }
            _sessionStatus = status;
            _status.Text = FormatStatus(status);
        }
        catch (Exception ex)
        {
            _status.Text = "Read failed: " + ex.Message;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task SendCurveAsync()
    {
        if (!TryReadCurve(out var curve, out string error))
        {
            MessageBox.Show(error, "TC curve", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (!ConfirmUsbSafety()) return;

        SetBusy(true, "Sending curve to logger…");
        try
        {
            TcCurveTransferResult result = await Task.Run(() =>
            {
                using IMiniLoggerUsbClient usb = _usbFactory();
                EnsureConnected(usb);
                return usb.SendTcCurve(curve);
            });
            if (result.Status?.IsTcLogger == true) _sessionStatus = result.Status;
            _status.Text = result.Message + (result.Status?.IsTcLogger == true
                ? Environment.NewLine + Environment.NewLine + FormatStatus(result.Status)
                : "");
            if (!result.Success)
                MessageBox.Show(result.Message, "Send TC curve", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (Exception ex)
        {
            _status.Text = "Send failed: " + ex.Message;
            MessageBox.Show(ex.Message, "Send TC curve", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task ClearCurveAsync()
    {
        if (MessageBox.Show("Clear the saved TC curve from the logger?", "Clear logger curve",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            return;
        if (!ConfirmUsbSafety()) return;

        SetBusy(true, "Clearing logger curve…");
        try
        {
            TcCurveTransferResult result = await Task.Run(() =>
            {
                using IMiniLoggerUsbClient usb = _usbFactory();
                EnsureConnected(usb);
                return usb.ClearTcCurve();
            });
            if (result.Status?.IsTcLogger == true) _sessionStatus = result.Status;
            _status.Text = result.Message;
            if (!result.Success)
                MessageBox.Show(result.Message, "Clear logger curve", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (Exception ex)
        {
            _status.Text = "Clear failed: " + ex.Message;
            MessageBox.Show(ex.Message, "Clear logger curve", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private static void EnsureConnected(IMiniLoggerUsbClient usb)
    {
        MiniLoggerConnectResult connection = usb.Connect();
        if (connection.Status == MiniLoggerConnectStatus.PortBusy)
            throw new IOException("The Mini Logger USB port is busy. Close Serial Monitor and try again.");
        if (connection.Status != MiniLoggerConnectStatus.Connected)
            throw new IOException("No Mini Logger was found. Plug it in by USB and try again.");
    }

    private bool ConfirmUsbSafety() =>
        MessageBox.Show(
            "Switch the car off and unplug the logger's receiver lead.\n\n" +
            "The car's BEC can back-feed the USB cable if both are connected.",
            "Mini Logger USB safety",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2) == DialogResult.OK;

    private static string FormatStatus(TcLoggerStatus status)
    {
        string settings = string.Join(Environment.NewLine,
            status.Settings.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(pair => string.Create(CultureInfo.InvariantCulture, $"  {pair.Key} {pair.Value:g}")));
        return string.Format(CultureInfo.InvariantCulture,
            "TC {0} | in {1} us | out {2} us | rpm {3:0} | curve {4} points\r\n" +
            "neutral {5} us | full {6} us | brake {7} us\r\n",
            status.StateText, status.InputUs, status.OutputUs, status.MotorRpm,
            status.CurvePointCount, status.NeutralUs, status.FullUs, status.BrakeUs) +
            settings + (settings.Length > 0 ? "\r\n" : "") + status.Firmware;
    }

    private void ConfigureGrid()
    {
        _points.Dock = DockStyle.Fill;
        _points.BackgroundColor = Window;
        _points.GridColor = Border;
        _points.BorderStyle = BorderStyle.None;
        _points.ForeColor = TextPrimary;
        _points.DefaultCellStyle.BackColor = Input;
        _points.DefaultCellStyle.ForeColor = TextPrimary;
        _points.DefaultCellStyle.SelectionBackColor = Accent;
        _points.ColumnHeadersDefaultCellStyle.BackColor = Card;
        _points.ColumnHeadersDefaultCellStyle.ForeColor = TextPrimary;
        _points.EnableHeadersVisualStyles = false;
        _points.RowHeadersVisible = false;
        _points.AllowUserToAddRows = false;
        _points.AllowUserToDeleteRows = false;
        _points.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _points.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _points.MultiSelect = true;
        _points.Columns.Add("TimeMs", "Time (ms)");
        _points.Columns.Add("Rpm", "RPM");
        _points.CellEndEdit += (_, _) => GridEdited();
        _points.SelectionChanged += (_, _) =>
        {
            if (!_busy) _deletePoint.Enabled = _points.SelectedRows.Count > 0;
        };
    }

    private void ConfigureChart()
    {
        _chart.Dock = DockStyle.Fill;
        _chart.Plot.FigureBackground.Color = new ScottPlot.Color(0x13, 0x17, 0x1E);
        _chart.Plot.DataBackground.Color = new ScottPlot.Color(0x0E, 0x12, 0x18);
        _chart.Plot.Axes.Bottom.Label.Text = "Time from logger launch (s)";
        _chart.Plot.Axes.Left.Label.Text = "Motor RPM";
        ApplyAxisStyle(_chart.Plot.Axes.Bottom);
        ApplyAxisStyle(_chart.Plot.Axes.Left);
        _chart.Plot.Grid.MajorLineColor = new ScottPlot.Color(0xFF, 0xFF, 0xFF, 18);
        _chart.UserInputProcessor.LeftClickDragPan(enable: false, horizontal: true, vertical: true);
        _chart.Menu?.Clear();
        _chart.MouseDown += ChartMouseDown;
        _chart.MouseMove += ChartMouseMove;
        _chart.MouseUp += ChartMouseUp;
        _chart.MouseLeave += (_, _) =>
        {
            if (_dragPointIndex < 0) _chart.Cursor = Cursors.Default;
        };
    }

    private static void ApplyAxisStyle(ScottPlot.IAxis axis)
    {
        var color = new ScottPlot.Color(0x9A, 0xA3, 0xB2);
        axis.Label.ForeColor = color;
        axis.TickLabelStyle.ForeColor = color;
        axis.MajorTickStyle.Color = color;
        axis.MinorTickStyle.Color = color;
        axis.FrameLineStyle.Color = color;
    }

    private void SetCurvePoints(IEnumerable<TcCurvePoint> points, bool fitView = false)
    {
        _updatingGrid = true;
        _points.Rows.Clear();
        foreach (TcCurvePoint point in points)
            _points.Rows.Add(
                point.TimeMs.ToString(CultureInfo.InvariantCulture),
                point.Rpm.ToString(CultureInfo.InvariantCulture));
        _updatingGrid = false;
        RedrawChart(fitView);
        UpdateEnabledState();
    }

    private void GridEdited()
    {
        if (_updatingGrid) return;
        if (TryReadCurve(out _, out _)) RedrawChart();
        UpdateEnabledState();
    }

    private bool TryReadCurve(
        out List<TcCurvePoint> curve,
        out string error,
        bool allowEmpty = false)
    {
        _points.EndEdit();
        curve = new List<TcCurvePoint>();
        for (int index = 0; index < _points.Rows.Count; index++)
        {
            DataGridViewRow row = _points.Rows[index];
            string timeText = Convert.ToString(row.Cells[0].Value, CultureInfo.InvariantCulture) ?? "";
            string rpmText = Convert.ToString(row.Cells[1].Value, CultureInfo.InvariantCulture) ?? "";
            if (!int.TryParse(timeText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int timeMs) ||
                !int.TryParse(rpmText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int rpm))
            {
                error = $"Row {index + 1} must contain a whole time in ms and a whole RPM.";
                return false;
            }
            curve.Add(new TcCurvePoint(timeMs, rpm));
        }
        if (allowEmpty && curve.Count == 0)
        {
            error = "";
            return true;
        }
        try
        {
            TcCurveBuilder.Validate(curve);
            error = "";
            return true;
        }
        catch (InvalidOperationException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private void RedrawChart(bool fitView = false)
    {
        _chart.Plot.Clear();
        if (_rpmTrace.Count > 1)
        {
            var raw = _chart.Plot.Add.Scatter(
                _rpmTrace.Select(sample => sample.TimeSeconds).ToArray(),
                _rpmTrace.Select(sample => sample.Rpm).ToArray());
            raw.LegendText = "Run RPM";
            raw.Color = new ScottPlot.Color(0xF2, 0x9E, 0x86);
            raw.MarkerSize = 0;
            raw.LineWidth = 1.5f;
        }
        if (TryReadCurve(out var curve, out _) && curve.Count > 0)
        {
            var line = _chart.Plot.Add.Scatter(
                curve.Select(point => point.TimeMs / 1000.0).ToArray(),
                curve.Select(point => (double)point.Rpm).ToArray());
            line.LegendText = "TC curve";
            line.Color = new ScottPlot.Color(0x66, 0xE0, 0xFF);
            line.LineWidth = 2.5f;
            line.MarkerSize = 6;
        }
        _chart.Plot.Legend.IsVisible = _rpmTrace.Count > 0;
        if (fitView) _chart.Plot.Axes.AutoScale();
        _chart.Refresh();
    }

    private void FitChart()
    {
        _chart.Plot.Axes.AutoScale();
        _chart.Refresh();
    }

    private void ChartMouseDown(object? sender, MouseEventArgs e)
    {
        if (_busy || !TryReadCurve(out var curve, out _, allowEmpty: true)) return;
        int hit = HitTestCurve(curve, e.Location);

        if (e.Button == MouseButtons.Right && hit >= 0)
        {
            TcCurveEditor.Delete(curve, hit);
            SetCurvePoints(curve);
            _status.Text = "Point deleted.";
            return;
        }

        if (e.Button != MouseButtons.Left) return;
        if (hit >= 0)
        {
            _dragPointIndex = hit;
            _chart.Capture = true;
            _chart.Cursor = Cursors.SizeAll;
            return;
        }

        ScottPlot.Coordinates coordinates = _chart.Plot.GetCoordinates(new ScottPlot.Pixel(e.X, e.Y));
        if (coordinates.X < 0)
        {
            _status.Text = "A point cannot be added before 0 ms.";
            return;
        }

        int timeMs;
        int rpm;
        try
        {
            timeMs = checked((int)Math.Round(coordinates.X * 1000.0, MidpointRounding.AwayFromZero));
            rpm = checked((int)Math.Round(coordinates.Y, MidpointRounding.AwayFromZero));
        }
        catch (OverflowException)
        {
            _status.Text = "That chart position is outside the curve range.";
            return;
        }

        if (!TcCurveEditor.TryInsert(curve, timeMs, rpm, out string error))
        {
            _status.Text = error;
            return;
        }
        SetCurvePoints(curve);
        _status.Text = string.Create(CultureInfo.InvariantCulture,
            $"Point added at {timeMs} ms, {Math.Max(1, rpm)} RPM.");
    }

    private void ChartMouseMove(object? sender, MouseEventArgs e)
    {
        if (_dragPointIndex >= 0)
        {
            if (!TryReadCurve(out var curve, out _) || _dragPointIndex >= curve.Count) return;
            ScottPlot.Coordinates coordinates = _chart.Plot.GetCoordinates(new ScottPlot.Pixel(e.X, e.Y));
            int timeMs = ToEditorInt(coordinates.X * 1000.0);
            int rpm = ToEditorInt(coordinates.Y);
            TcCurvePoint moved = TcCurveEditor.Move(curve, _dragPointIndex, timeMs, rpm);
            SetCurvePoints(curve);
            _status.Text = string.Create(CultureInfo.InvariantCulture,
                $"Point moved to {moved.TimeMs} ms, {moved.Rpm} RPM.");
            return;
        }

        if (_busy || !TryReadCurve(out var current, out _, allowEmpty: true))
        {
            _chart.Cursor = Cursors.Default;
            return;
        }
        _chart.Cursor = HitTestCurve(current, e.Location) >= 0 ? Cursors.Hand : Cursors.Default;
    }

    private void ChartMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || _dragPointIndex < 0) return;
        _dragPointIndex = -1;
        _chart.Capture = false;
        _chart.Cursor = Cursors.Default;
    }

    private int HitTestCurve(IReadOnlyList<TcCurvePoint> curve, Point mouse)
    {
        var pixels = new TcEditorPixel[curve.Count];
        for (int i = 0; i < curve.Count; i++)
        {
            ScottPlot.Pixel pixel = _chart.Plot.GetPixel(
                new ScottPlot.Coordinates(curve[i].TimeMs / 1000.0, curve[i].Rpm));
            pixels[i] = new TcEditorPixel(pixel.X, pixel.Y);
        }
        return TcCurveEditor.HitTest(pixels, new TcEditorPixel(mouse.X, mouse.Y), PointHitRadiusPixels);
    }

    private static int ToEditorInt(double value)
    {
        if (!double.IsFinite(value)) return 0;
        double rounded = Math.Round(value, MidpointRounding.AwayFromZero);
        return rounded <= int.MinValue ? int.MinValue
            : rounded >= int.MaxValue ? int.MaxValue
            : (int)rounded;
    }

    private void AddPoint()
    {
        if (_points.Rows.Count >= TcCurveBuilder.MaximumPoints) return;
        int time = 0;
        int rpm = 1;
        if (_points.Rows.Count > 0)
        {
            int.TryParse(Convert.ToString(_points.Rows[^1].Cells[0].Value, CultureInfo.InvariantCulture),
                NumberStyles.Integer, CultureInfo.InvariantCulture, out time);
            int.TryParse(Convert.ToString(_points.Rows[^1].Cells[1].Value, CultureInfo.InvariantCulture),
                NumberStyles.Integer, CultureInfo.InvariantCulture, out rpm);
            time += 10;
            rpm = Math.Max(1, rpm);
        }
        _points.Rows.Add(time.ToString(CultureInfo.InvariantCulture), rpm.ToString(CultureInfo.InvariantCulture));
        GridEdited();
    }

    private void DeletePoints()
    {
        foreach (DataGridViewRow row in _points.SelectedRows.Cast<DataGridViewRow>()
                     .OrderByDescending(row => row.Index))
            _points.Rows.RemoveAt(row.Index);
        GridEdited();
    }

    private void SetBusy(bool busy, string? message = null)
    {
        _busy = busy;
        if (message is not null) _status.Text = message;
        UpdateEnabledState();
    }

    private void UpdateEnabledState()
    {
        bool inputsValid = _source.SelectedItem is TcSourceRun &&
                           TryPositiveOrZero(_margin.Text, out _) &&
                           TryPositive(_curveEnd.Text, out _);
        _makeCurve.Enabled = !_busy && inputsValid;
        _readLogger.Enabled = !_busy;
        _sendLogger.Enabled = !_busy && TryReadCurve(out _, out _);
        _clearLogger.Enabled = !_busy;
        _fitChart.Enabled = !_busy;
        _addPoint.Enabled = !_busy && _points.Rows.Count < TcCurveBuilder.MaximumPoints;
        _deletePoint.Enabled = !_busy && _points.SelectedRows.Count > 0;
        _source.Enabled = !_busy;
        _loadLog.Enabled = !_busy;
        _margin.Enabled = !_busy;
        _marginUnit.Enabled = !_busy;
        _curveEnd.Enabled = !_busy;
        _points.ReadOnly = _busy;
    }

    private static bool TryPositive(string text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) &&
        double.IsFinite(value) && value > 0;

    private static bool TryPositiveOrZero(string text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) &&
        double.IsFinite(value) && value >= 0;

    private static Control Field(string label, Control input, int width)
    {
        var panel = new Panel { Width = width, Height = 54, Margin = new Padding(4, 0, 6, 0) };
        var caption = new Label
        {
            Text = label,
            Dock = DockStyle.Top,
            Height = 19,
            ForeColor = TextSecond
        };
        input.Dock = DockStyle.Bottom;
        input.Height = 28;
        input.BackColor = Input;
        input.ForeColor = TextPrimary;
        panel.Controls.Add(input);
        panel.Controls.Add(caption);
        return panel;
    }

    private static Button MakeButton(string text, bool accent, int width)
    {
        var button = new Button
        {
            Text = text,
            Width = width,
            Height = 30,
            FlatStyle = FlatStyle.Flat,
            BackColor = accent ? Accent : Input,
            ForeColor = TextPrimary,
            Margin = new Padding(4),
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderColor = accent ? Accent : Border;
        return button;
    }
}
