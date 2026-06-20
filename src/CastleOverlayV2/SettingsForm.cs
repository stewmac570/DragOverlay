using CastleOverlayV2.Models;
using CastleOverlayV2.Services;

namespace CastleOverlayV2
{
    /// <summary>
    /// Modal settings dialog. Persists changes to <see cref="ConfigService"/> on OK.
    /// </summary>
    public sealed class SettingsForm : Form
    {
        private readonly ConfigService _configService;
        private readonly TextBox _castleDir = new();
        private readonly TextBox _raceboxDir = new();
        private readonly TextBox _tuneDir = new();
        private readonly CheckBox _smoothingEnabled = new();
        private readonly NumericUpDown _smoothingWindow = new();

        public SettingsForm(ConfigService configService)
        {
            _configService = configService;

            Text = "Settings";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            Width = 540;
            Height = 360;
            Font = new Font("Segoe UI", 9f);

            BuildLayout();
            LoadFromConfig();
        }

        private void BuildLayout()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(12),
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(root);

            // -------- Default folders -------------
            root.Controls.Add(MakeSectionLabel("Default open folders"), 0, 0);
            root.Controls.Add(BuildFolderPanel(), 0, 1);

            // -------- Voltage smoothing -----------
            var smoothingPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(0, 12, 0, 0),
            };
            _smoothingEnabled.Text = "Smooth voltage trace";
            _smoothingEnabled.AutoSize = true;
            _smoothingEnabled.Margin = new Padding(0, 5, 12, 0);
            _smoothingWindow.Minimum = 3;
            _smoothingWindow.Maximum = 15;
            _smoothingWindow.Increment = 2;
            _smoothingWindow.Width = 60;
            var winLabel = new Label
            {
                Text = "Window:",
                AutoSize = true,
                Margin = new Padding(8, 5, 4, 0),
            };
            smoothingPanel.Controls.Add(_smoothingEnabled);
            smoothingPanel.Controls.Add(winLabel);
            smoothingPanel.Controls.Add(_smoothingWindow);

            var bottom = new Panel { Dock = DockStyle.Fill };
            bottom.Controls.Add(smoothingPanel);
            bottom.Controls.Add(MakeSectionLabel("Voltage smoothing"));
            bottom.Controls.Add(BuildOkCancelRow());
            root.Controls.Add(bottom, 0, 2);

            // Lay out top-down inside `bottom`.
            bottom.Controls[bottom.Controls.Count - 1].Dock = DockStyle.Bottom;
        }

        private Label MakeSectionLabel(string text) => new()
        {
            Text = text,
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 6),
            Dock = DockStyle.Top,
        };

        private TableLayoutPanel BuildFolderPanel()
        {
            var panel = new TableLayoutPanel
            {
                ColumnCount = 3,
                RowCount = 3,
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
            };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));

            AddFolderRow(panel, 0, "Castle log folder", _castleDir);
            AddFolderRow(panel, 1, "RaceBox log folder", _raceboxDir);
            AddFolderRow(panel, 2, "Tune folder", _tuneDir);
            return panel;
        }

        private static void AddFolderRow(TableLayoutPanel panel, int row, string label, TextBox text)
        {
            panel.Controls.Add(new Label
            {
                Text = label,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 4, 8, 4),
            }, 0, row);

            text.Dock = DockStyle.Fill;
            text.Margin = new Padding(0, 2, 8, 2);
            panel.Controls.Add(text, 1, row);

            var browse = new Button
            {
                Text = "Browse…",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 2, 0, 2),
            };
            browse.Click += (_, _) =>
            {
                using var dlg = new FolderBrowserDialog();
                if (!string.IsNullOrWhiteSpace(text.Text) && Directory.Exists(text.Text))
                    dlg.SelectedPath = text.Text;
                if (dlg.ShowDialog() == DialogResult.OK)
                    text.Text = dlg.SelectedPath;
            };
            panel.Controls.Add(browse, 2, row);
        }

        private Panel BuildOkCancelRow()
        {
            var row = new Panel
            {
                Height = 40,
                Padding = new Padding(0, 8, 0, 0),
            };
            var ok = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Width = 80,
                Anchor = AnchorStyles.Right | AnchorStyles.Top,
            };
            ok.Location = new Point(row.Width - 180, 4);
            var cancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Width = 80,
                Anchor = AnchorStyles.Right | AnchorStyles.Top,
            };
            cancel.Location = new Point(row.Width - 90, 4);
            row.Controls.Add(ok);
            row.Controls.Add(cancel);
            AcceptButton = ok;
            CancelButton = cancel;
            ok.Click += (_, _) => SaveToConfig();
            row.Resize += (_, _) =>
            {
                ok.Location = new Point(row.ClientSize.Width - 180, 4);
                cancel.Location = new Point(row.ClientSize.Width - 90, 4);
            };
            return row;
        }

        private void LoadFromConfig()
        {
            var c = _configService.Config;
            _castleDir.Text = c.CastleLogDirectory ?? "";
            _raceboxDir.Text = c.RaceBoxLogDirectory ?? "";
            _tuneDir.Text = c.TuneDirectory ?? "";
            _smoothingEnabled.Checked = c.VoltageSmoothingEnabled;
            _smoothingWindow.Value = Math.Max(3, Math.Min(15, c.VoltageSmoothingWindow | 1));
        }

        private void SaveToConfig()
        {
            var c = _configService.Config;
            c.CastleLogDirectory = _castleDir.Text.Trim();
            c.RaceBoxLogDirectory = _raceboxDir.Text.Trim();
            c.TuneDirectory = _tuneDir.Text.Trim();
            c.VoltageSmoothingEnabled = _smoothingEnabled.Checked;
            int win = (int)_smoothingWindow.Value;
            if (win % 2 == 0) win += 1; // force odd
            c.VoltageSmoothingWindow = Math.Max(3, Math.Min(15, win));
            _configService.Save();
        }
    }
}
