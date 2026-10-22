using CastleOverlayV2.Services;

namespace CastleOverlayV2;

internal static class MiniLoggerDialogTheme
{
    public static readonly Color Window = Color.FromArgb(0x13, 0x17, 0x1E);
    public static readonly Color Card = Color.FromArgb(0x1A, 0x1F, 0x27);
    public static readonly Color Input = Color.FromArgb(0x20, 0x26, 0x31);
    public static readonly Color Text = Color.FromArgb(0xE6, 0xE9, 0xEF);
    public static readonly Color Muted = Color.FromArgb(0x9A, 0xA3, 0xB2);
    public static readonly Color Border = Color.FromArgb(0x29, 0x2F, 0x39);

    public static Button Button(string text)
    {
        var button = new Button
        {
            Text = text,
            Width = 92,
            Height = 30,
            FlatStyle = FlatStyle.Flat,
            BackColor = Card,
            ForeColor = Text,
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderColor = Border;
        return button;
    }

    public static void Apply(Form form)
    {
        form.Font = new Font("Segoe UI", 9f);
        form.BackColor = Window;
        form.ForeColor = Text;
        form.StartPosition = FormStartPosition.CenterParent;
        form.FormBorderStyle = FormBorderStyle.FixedDialog;
        form.MinimizeBox = false;
        form.MaximizeBox = false;
        form.HandleCreated += (_, _) => Utils.WindowsDarkTitleBar.Apply(form.Handle);
    }
}

internal sealed class MiniLoggerLookingForm : Form
{
    public MiniLoggerLookingForm()
    {
        MiniLoggerDialogTheme.Apply(this);
        Text = "Mini Logger";
        Width = 330;
        Height = 115;
        ControlBox = false;
        Controls.Add(new Label
        {
            Text = "Looking for the Mini Logger…",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = MiniLoggerDialogTheme.Text
        });
    }
}

internal sealed class MiniLoggerRunSelectionForm : Form
{
    private readonly CheckedListBox _runs = new();
    private readonly IReadOnlyList<MiniLoggerRunInfo> _items;

    public MiniLoggerRunSelectionForm(IReadOnlyList<MiniLoggerRunInfo> runs)
    {
        _items = runs;
        MiniLoggerDialogTheme.Apply(this);
        Text = "Runs on Mini Logger";
        Width = 390;
        Height = 420;

        _runs.Dock = DockStyle.Fill;
        _runs.CheckOnClick = true;
        _runs.BackColor = MiniLoggerDialogTheme.Input;
        _runs.ForeColor = MiniLoggerDialogTheme.Text;
        _runs.BorderStyle = BorderStyle.FixedSingle;
        foreach (MiniLoggerRunInfo run in runs)
            _runs.Items.Add(run, true);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 48,
            Padding = new Padding(0, 8, 8, 0),
            FlowDirection = FlowDirection.RightToLeft
        };
        Button cancel = MiniLoggerDialogTheme.Button("Cancel");
        cancel.DialogResult = DialogResult.Cancel;
        Button download = MiniLoggerDialogTheme.Button("Download");
        download.DialogResult = DialogResult.OK;
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(download);
        Controls.Add(_runs);
        Controls.Add(buttons);
        AcceptButton = download;
        CancelButton = cancel;
    }

    public IReadOnlyList<MiniLoggerRunInfo> SelectedRuns =>
        _runs.CheckedIndices.Cast<int>().Select(index => _items[index]).ToList();
}

internal sealed class MiniLoggerDownloadForm : Form, IMiniLoggerDownloadSession
{
    private readonly Label _runLabel = new();
    private readonly Label _amountLabel = new();
    private readonly ProgressBar _progress = new();
    private readonly CancellationTokenSource _cancellation = new();

    public MiniLoggerDownloadForm(int runCount)
    {
        MiniLoggerDialogTheme.Apply(this);
        Text = "Download from Mini Logger";
        Width = 430;
        Height = 190;
        ControlBox = false;

        _runLabel.Text = $"Preparing {runCount} run{(runCount == 1 ? "" : "s")}…";
        _runLabel.SetBounds(20, 20, 370, 24);
        _runLabel.ForeColor = MiniLoggerDialogTheme.Text;
        _amountLabel.SetBounds(20, 48, 370, 22);
        _amountLabel.ForeColor = MiniLoggerDialogTheme.Muted;
        _progress.SetBounds(20, 76, 374, 20);
        _progress.Minimum = 0;
        _progress.Maximum = 1000;
        Button cancel = MiniLoggerDialogTheme.Button("Cancel");
        cancel.SetBounds(302, 108, 92, 30);
        cancel.Click += (_, _) =>
        {
            cancel.Enabled = false;
            cancel.Text = "Cancelling…";
            _cancellation.Cancel();
        };

        Controls.Add(_runLabel);
        Controls.Add(_amountLabel);
        Controls.Add(_progress);
        Controls.Add(cancel);
    }

    public CancellationToken CancellationToken => _cancellation.Token;

    public void Report(MiniLoggerDownloadProgress value)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => Report(value));
            return;
        }
        if (IsDisposed) return;

        _runLabel.Text = $"Downloading Run {value.RunNumber} — {value.RunIndex} of {value.RunCount}";
        double receivedKb = value.BytesReceived / 1024.0;
        double expectedKb = value.ExpectedBytes / 1024.0;
        _amountLabel.Text = $"{receivedKb:N0} KB of about {expectedKb:N0} KB";
        int fraction = value.ExpectedBytes <= 0
            ? 0
            : (int)Math.Clamp(value.BytesReceived * 1000L / value.ExpectedBytes, 0, 1000);
        _progress.Value = fraction;
    }

    public void Complete()
    {
        if (InvokeRequired)
        {
            BeginInvoke(Complete);
            return;
        }
        if (!IsDisposed) Close();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _cancellation.Dispose();
        base.Dispose(disposing);
    }
}
