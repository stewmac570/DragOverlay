// File: src/CastleOverlayV2/Controls/TopBar.cs
using System;
using System.Drawing;
using System.Windows.Forms;

namespace CastleOverlayV2.Controls
{
    public enum AppPage
    {
        Logs,
        Tune,
        Tc
    }

    /// <summary>
    /// Top bar above the run strip (Docs/UI_Layout_Plan.md): app name, the Logs / Tune / TC
    /// page tabs, and the app-wide actions (project open/save, settings). The Logs actions
    /// (Drag/Speed pill, auto load, from logger, auto trim, View menu) only show on the Logs
    /// page; View also shows on TC for the RPM pole setting.
    /// </summary>
    public sealed class TopBar : UserControl
    {
        private static readonly Color SurfaceBar = Color.FromArgb(0x1B, 0x21, 0x2B);
        private static readonly Color SurfaceCard = Color.FromArgb(0x1A, 0x1F, 0x27);
        private static readonly Color TextPrimary = Color.FromArgb(0xE6, 0xE9, 0xEF);
        private static readonly Color TextSecond = Color.FromArgb(0x9A, 0xA3, 0xB2);
        private static readonly Color TextAccent = Color.FromArgb(0x9A, 0xC7, 0xF2);
        private static readonly Color BorderDef = Color.FromArgb(0x29, 0x2F, 0x39);
        private static readonly Color AccentBlue = Color.FromArgb(0x2D, 0x6C, 0xB5);

        private readonly TabButton _logsTab;
        private readonly TabButton _tuneTab;
        private readonly TabButton _tcTab;
        private readonly Button _viewButton;
        private readonly Button _autoLoadButton;
        private readonly Button _fromLoggerButton;
        private Control? _runTypeSwitch;
        private readonly FlowLayoutPanel _actions;
        private readonly Button _saveProjectButton;
        private readonly Button _autoTrimButton;
        private readonly ToolTip _toolTip;

        public event Action<AppPage>? PageSelected;
        public event Action? AutoLoadRequested;
        public event Action? DownloadFromLoggerRequested;
        public event Action? AutoTrimToggled;
        public event Action? OpenProjectRequested;
        public event Action? SaveProjectRequested;
        public event Action? SettingsRequested;
        public event Action<int>? PaneCountRequested;
        public event Action? BySourcePanesRequested;
        public event Action<bool>? RpmModeRequested;

        /// <summary>Asked when the View menu opens: (pane count, RPM is 4 pole), for the ticks.</summary>
        public Func<(int PaneCount, bool FourPole)>? ViewState { get; set; }

        public AppPage SelectedPage { get; private set; } = AppPage.Logs;

        public TopBar()
        {
            Dock = DockStyle.Top;
            Height = 44;
            BackColor = SurfaceBar;
            ForeColor = TextPrimary;

            _toolTip = new ToolTip { AutoPopDelay = 8000, InitialDelay = 400, ReshowDelay = 200, ShowAlways = true };

            _actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = SurfaceBar,
                Padding = new Padding(0, 7, 8, 0),
                Margin = new Padding(0)
            };

            // RightToLeft flow: first added sits furthest right.
            var gear = MakeActionButton("⚙", 36, TextPrimary, "Settings");
            gear.Font = new Font(SystemFonts.DefaultFont.FontFamily, 12f, FontStyle.Bold);
            gear.Click += (_, _) => SettingsRequested?.Invoke();
            _actions.Controls.Add(gear);

            _saveProjectButton = MakeActionButton("Save project", 104, TextPrimary, "Save logs, tunes and settings as a project file");
            _saveProjectButton.BackColor = AccentBlue;
            _saveProjectButton.FlatAppearance.BorderColor = AccentBlue;
            _saveProjectButton.Enabled = false;
            _saveProjectButton.Click += (_, _) => SaveProjectRequested?.Invoke();
            _actions.Controls.Add(_saveProjectButton);

            var openProject = MakeActionButton("Open project", 104, TextPrimary, "Open a saved project file");
            openProject.Click += (_, _) => OpenProjectRequested?.Invoke();
            _actions.Controls.Add(openProject);

            // Logs actions from here on. A wider gap separates them from the project buttons.
            _autoLoadButton = MakeActionButton("⤓ Auto load", 96, TextAccent, "Load the most recent log + tune + RaceBox");
            _autoLoadButton.Margin = new Padding(4, 0, 20, 0);
            _autoLoadButton.Click += (_, _) => AutoLoadRequested?.Invoke();
            _actions.Controls.Add(_autoLoadButton);

            _fromLoggerButton = MakeActionButton("⤓ From logger", 108, TextAccent,
                "Find the Mini Logger over USB and download saved runs");
            _fromLoggerButton.Click += (_, _) => DownloadFromLoggerRequested?.Invoke();
            _actions.Controls.Add(_fromLoggerButton);

            _autoTrimButton = MakeActionButton("✂ Auto trim: On", 118, TextPrimary,
                "On: logs are cut to 0.5 s before launch → 2.5 s after.\nOff: logs load in full, still lined up at launch.");
            _autoTrimButton.Click += (_, _) => AutoTrimToggled?.Invoke();
            _actions.Controls.Add(_autoTrimButton);

            _viewButton = MakeActionButton("View ▾", 72, TextPrimary, "Chart panes and RPM motor poles");
            _viewButton.Click += (_, _) => ShowViewMenu();
            _actions.Controls.Add(_viewButton);

            Controls.Add(_actions);

            var left = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = SurfaceBar,
                Padding = new Padding(10, 0, 0, 0),
                Margin = new Padding(0)
            };

            var brand = new Label
            {
                Text = "DragOverlay",
                AutoSize = false,
                Width = 120,
                Height = 44,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = TextPrimary,
                Font = new Font(SystemFonts.DefaultFont.FontFamily, 11f, FontStyle.Bold),
                Margin = new Padding(0, 0, 12, 0)
            };
            left.Controls.Add(brand);

            _logsTab = new TabButton("Logs");
            _logsTab.Click += (_, _) => SelectPage(AppPage.Logs);
            left.Controls.Add(_logsTab);

            _tuneTab = new TabButton("Tune");
            _tuneTab.Click += (_, _) => SelectPage(AppPage.Tune);
            left.Controls.Add(_tuneTab);

            _tcTab = new TabButton("TC");
            _tcTab.Click += (_, _) => SelectPage(AppPage.Tc);
            left.Controls.Add(_tcTab);

            Controls.Add(left);
            left.BringToFront();

            ApplyTabState();
        }

        /// <summary>Places the Drag/Speed pill to the left of the action buttons (Logs page only).</summary>
        public void HostRunTypeSwitch(Control runTypeSwitch)
        {
            runTypeSwitch.Parent?.Controls.Remove(runTypeSwitch);
            runTypeSwitch.Margin = new Padding(0, 1, 12, 0);
            _actions.Controls.Add(runTypeSwitch);
            _runTypeSwitch = runTypeSwitch;
            ApplyTabState();
        }

        public void SetSaveProjectEnabled(bool enabled) => _saveProjectButton.Enabled = enabled;

        /// <summary>Show the auto trim state on its button (blue outline while off).</summary>
        public void SetAutoTrim(bool on)
        {
            _autoTrimButton.Text = on ? "✂ Auto trim: On" : "✂ Auto trim: Off";
            _autoTrimButton.ForeColor = on ? TextPrimary : TextAccent;
            _autoTrimButton.FlatAppearance.BorderColor = on ? BorderDef : AccentBlue;
        }

        public void SelectPage(AppPage page)
        {
            if (SelectedPage == page)
                return;

            SelectedPage = page;
            ApplyTabState();
            PageSelected?.Invoke(page);
        }

        private void ShowViewMenu()
        {
            var (paneCount, fourPole) = ViewState?.Invoke() ?? (1, false);
            var menu = new ContextMenuStrip();
            // Panes only mean something on the Logs chart; TC only needs the pole setting.
            if (SelectedPage == AppPage.Logs)
            {
                for (int count = 1; count <= 3; count++)
                {
                    int selected = count;
                    var item = new ToolStripMenuItem(count == 1 ? "1 pane" : $"{count} panes", null,
                        (_, _) => PaneCountRequested?.Invoke(selected)) { Checked = count == paneCount };
                    menu.Items.Add(item);
                }
                menu.Items.Add(new ToolStripMenuItem("Panes by source (ESC, GPS, data log)", null,
                    (_, _) => BySourcePanesRequested?.Invoke()));
                menu.Items.Add(new ToolStripSeparator());
            }
            menu.Items.Add(new ToolStripMenuItem("RPM: 2 pole", null,
                (_, _) => RpmModeRequested?.Invoke(false)) { Checked = !fourPole });
            menu.Items.Add(new ToolStripMenuItem("RPM: 4 pole (halves the value)", null,
                (_, _) => RpmModeRequested?.Invoke(true)) { Checked = fourPole });
            menu.Closed += (_, _) => BeginInvoke(menu.Dispose);
            menu.Show(_viewButton, new Point(0, _viewButton.Height));
        }

        private void ApplyTabState()
        {
            _logsTab.Selected = SelectedPage == AppPage.Logs;
            _tuneTab.Selected = SelectedPage == AppPage.Tune;
            _tcTab.Selected = SelectedPage == AppPage.Tc;

            // Each page shows only its own buttons; the project buttons and settings stay.
            bool logs = SelectedPage == AppPage.Logs;
            _actions.SuspendLayout();
            _autoLoadButton.Visible = logs;
            _fromLoggerButton.Visible = logs;
            _autoTrimButton.Visible = logs;
            _viewButton.Visible = logs || SelectedPage == AppPage.Tc;
            if (_runTypeSwitch != null) _runTypeSwitch.Visible = logs;
            _actions.ResumeLayout(true);
        }

        private Button MakeActionButton(string text, int width, Color foreColor, string tooltip)
        {
            var button = new Button
            {
                Text = text,
                Width = width,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                BackColor = SurfaceCard,
                ForeColor = foreColor,
                Margin = new Padding(4, 0, 4, 0),
                Padding = new Padding(0),
                Font = new Font(SystemFonts.DefaultFont.FontFamily, 9f),
                TabStop = false,
                UseVisualStyleBackColor = false
            };
            button.FlatAppearance.BorderColor = BorderDef;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.MouseOverBackColor = SurfaceBar;
            button.FlatAppearance.MouseDownBackColor = SurfaceBar;
            _toolTip.SetToolTip(button, tooltip);
            return button;
        }

        /// <summary>Flat page tab: bold text and a blue underline when selected.</summary>
        private sealed class TabButton : Button
        {
            private bool _selected;

            public bool Selected
            {
                get => _selected;
                set
                {
                    _selected = value;
                    ForeColor = value ? TextPrimary : TextSecond;
                    Font = new Font(SystemFonts.DefaultFont.FontFamily, 10f, value ? FontStyle.Bold : FontStyle.Regular);
                    Invalidate();
                }
            }

            public TabButton(string text)
            {
                Text = text;
                Width = 76;
                Height = 44;
                FlatStyle = FlatStyle.Flat;
                BackColor = SurfaceBar;
                Margin = new Padding(0);
                Padding = new Padding(0);
                TabStop = false;
                UseVisualStyleBackColor = false;
                Cursor = Cursors.Hand;
                SetStyle(ControlStyles.Selectable, false); // no focus rectangle after a click
                FlatAppearance.BorderSize = 0;
                FlatAppearance.MouseOverBackColor = SurfaceCard;
                FlatAppearance.MouseDownBackColor = SurfaceCard;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                if (_selected)
                {
                    using var brush = new SolidBrush(Color.FromArgb(0x4A, 0x90, 0xE2));
                    e.Graphics.FillRectangle(brush, 0, Height - 3, Width, 3);
                }
            }
        }
    }
}
