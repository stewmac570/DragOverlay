using CastleOverlayV2.Services;

namespace CastleOverlayV2
{
    /// <summary>
    /// Settings, "Logger WiFi…": the WiFi networks saved on the Big logger. Works over USB (the
    /// first time) or over WiFi once the logger has joined a network. Changes go to the logger
    /// straight away; passwords are sent to it and never shown or stored here.
    /// </summary>
    public sealed class LoggerWifiForm : Form
    {
        private static readonly Color SurfaceWindow = Color.FromArgb(0x13, 0x17, 0x1E);
        private static readonly Color SurfaceCard   = Color.FromArgb(0x1A, 0x1F, 0x27);
        private static readonly Color SurfaceInput  = Color.FromArgb(0x20, 0x26, 0x31);
        private static readonly Color TextPrimary   = Color.FromArgb(0xE6, 0xE9, 0xEF);
        private static readonly Color TextSecond    = Color.FromArgb(0x9A, 0xA3, 0xB2);
        private static readonly Color BorderDef     = Color.FromArgb(0x29, 0x2F, 0x39);

        private readonly Label _status = new();
        private readonly ListBox _networks = new();
        private readonly TextBox _name = new();
        private readonly TextBox _password = new();
        private readonly TextBox _updatePassword = new();
        private readonly Button _add;
        private readonly Button _remove;
        private readonly Button _saveUpdatePassword;
        private readonly Button _refresh;
        private MiniLoggerUsb? _logger;
        private BigLoggerWifiSettings? _wifi;
        private readonly SemaphoreSlim _busy = new(1, 1);

        public LoggerWifiForm()
        {
            Text = "Logger WiFi";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            ClientSize = new Size(460, 470);
            Font = new Font("Segoe UI", 9f);
            BackColor = SurfaceWindow;
            ForeColor = TextPrimary;
            HandleCreated += (_, _) => Utils.WindowsDarkTitleBar.Apply(Handle);

            _add = MakeButton("Add");
            _remove = MakeButton("Remove");
            _saveUpdatePassword = MakeButton("Save");
            _refresh = MakeButton("Refresh");
            BuildLayout();

            _add.Click += async (_, _) => await AddNetworkAsync();
            _remove.Click += async (_, _) => await RemoveNetworkAsync();
            _saveUpdatePassword.Click += async (_, _) => await SaveUpdatePasswordAsync();
            _refresh.Click += async (_, _) => await ConnectAsync();
            Shown += async (_, _) => await ConnectAsync();
            FormClosed += (_, _) => _logger?.Dispose();
        }

        private void BuildLayout()
        {
            int y = 12;
            _status.SetBounds(12, y, 436, 54);
            _status.ForeColor = TextSecond;
            Controls.Add(_status);
            y += 62;

            Controls.Add(SectionLabel("Saved on the logger", 12, y));
            y += 22;
            _networks.SetBounds(12, y, 340, 100);
            _networks.BackColor = SurfaceInput;
            _networks.ForeColor = TextPrimary;
            _networks.BorderStyle = BorderStyle.FixedSingle;
            Controls.Add(_networks);
            _remove.SetBounds(360, y, 88, 28);
            Controls.Add(_remove);
            y += 112;

            Controls.Add(SectionLabel("Add a network (2.4 GHz: house WiFi or phone hotspot)", 12, y));
            y += 22;
            AddField("Name", _name, ref y, password: false);
            AddField("Password", _password, ref y, password: true);
            _add.SetBounds(360, y - 32, 88, 28);
            Controls.Add(_add);
            y += 8;

            Controls.Add(SectionLabel("Firmware update password (6 or more characters)", 12, y));
            y += 22;
            AddField("Password", _updatePassword, ref y, password: true);
            _saveUpdatePassword.SetBounds(360, y - 32, 88, 28);
            Controls.Add(_saveUpdatePassword);
            y += 8;

            var note = new Label
            {
                Text = "The logger turns its WiFi on 15 s after the throttle stops at neutral, and off the moment it moves.",
                ForeColor = TextSecond,
            };
            note.SetBounds(12, y, 436, 36);
            Controls.Add(note);

            _refresh.SetBounds(264, 428, 88, 28);
            Controls.Add(_refresh);
            var close = MakeButton("Close");
            close.SetBounds(360, 428, 88, 28);
            close.DialogResult = DialogResult.Cancel;
            Controls.Add(close);
            CancelButton = close;
        }

        private void AddField(string label, TextBox box, ref int y, bool password)
        {
            var l = new Label { Text = label, ForeColor = TextSecond, TextAlign = ContentAlignment.MiddleLeft };
            l.SetBounds(12, y, 80, 26);
            Controls.Add(l);
            box.SetBounds(92, y, 260, 26);
            box.BackColor = SurfaceInput;
            box.ForeColor = TextPrimary;
            box.BorderStyle = BorderStyle.FixedSingle;
            box.UseSystemPasswordChar = password;
            Controls.Add(box);
            y += 32;
        }

        private Label SectionLabel(string text, int x, int y)
        {
            var l = new Label { Text = text, AutoSize = true, Font = new Font(Font, FontStyle.Bold), ForeColor = TextSecond };
            l.Location = new Point(x, y);
            return l;
        }

        private static Button MakeButton(string text)
        {
            var b = new Button
            {
                Text = text,
                FlatStyle = FlatStyle.Flat,
                BackColor = SurfaceCard,
                ForeColor = TextPrimary,
                UseVisualStyleBackColor = false,
            };
            b.FlatAppearance.BorderColor = BorderDef;
            b.FlatAppearance.MouseOverBackColor = SurfaceInput;
            b.FlatAppearance.MouseDownBackColor = SurfaceInput;
            return b;
        }

        private void SetEnabled(bool on)
        {
            foreach (Control c in new Control[] { _add, _remove, _saveUpdatePassword, _networks, _name, _password, _updatePassword })
                c.Enabled = on;
        }

        // ---------- Talking to the logger (off the UI thread) ----------

        private async Task ConnectAsync()
        {
            if (!await _busy.WaitAsync(0)) return;
            SetEnabled(false);
            _refresh.Enabled = false;
            _status.Text = "Looking for the Big logger on USB, then on WiFi…";
            try
            {
                _logger?.Dispose();
                _logger = new MiniLoggerUsb();
                MiniLoggerUsb logger = _logger;
                var result = await Task.Run(() => logger.Connect());
                if (result.Status != MiniLoggerConnectStatus.Connected)
                {
                    _status.Text = result.Status == MiniLoggerConnectStatus.PortBusy
                        ? "The logger's USB port is busy. Close the other program using it, then Refresh."
                        : "No Big logger found. Plug it in by USB (car power off, red pin out), or have it on WiFi, then Refresh.";
                    return;
                }
                if (!logger.IsBigLogger)
                {
                    _status.Text = "That is a Mini or TC logger. Only the Big logger has WiFi.";
                    return;
                }
                _wifi = new BigLoggerWifiSettings(logger);
                BigLoggerWifiSettings wifi = _wifi;
                if (!await Task.Run(() => wifi.Supported()))
                {
                    _status.Text = "This Big logger's firmware has no WiFi yet. Load the WiFi firmware (rc-logger) over USB first.";
                    return;
                }
                await ReloadAsync();
                SetEnabled(true);
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException)
            {
                _status.Text = "Lost the logger: " + ex.Message;
            }
            finally
            {
                _refresh.Enabled = true;
                _busy.Release();
            }
        }

        private async Task ReloadAsync()
        {
            BigLoggerWifiSettings wifi = _wifi!;
            string via = _logger?.ConnectedName is string name && LoggerWifi.IsWifiName(name)
                ? "over WiFi at " + name[LoggerWifi.Prefix.Length..]
                : "on USB " + _logger?.ConnectedName;
            var (status, networks) = await Task.Run(() => (wifi.Status(), wifi.Networks()));
            _status.Text = $"Big logger {via}.\n{status}";
            _networks.Items.Clear();
            foreach (string n in networks) _networks.Items.Add(n);
        }

        private async Task RunAsync(Func<BigLoggerWifiSettings, string> action, Action? onOk = null)
        {
            if (_wifi is null || !await _busy.WaitAsync(0)) return;
            SetEnabled(false);
            try
            {
                BigLoggerWifiSettings wifi = _wifi;
                string reply = await Task.Run(() => action(wifi));
                if (reply.StartsWith("OK", StringComparison.Ordinal))
                {
                    onOk?.Invoke();
                    await ReloadAsync();
                }
                else
                {
                    MessageBox.Show(this, reply, "Logger WiFi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException)
            {
                _status.Text = "Lost the logger: " + ex.Message + " Press Refresh.";
            }
            finally
            {
                SetEnabled(_wifi is not null);
                _busy.Release();
            }
        }

        private async Task AddNetworkAsync()
        {
            string name = _name.Text.Trim();
            if (name.Length == 0) { MessageBox.Show(this, "Type the network name.", "Logger WiFi"); return; }
            string password = _password.Text;
            await RunAsync(w => w.Add(name, password), () => { _name.Clear(); _password.Clear(); });
        }

        private async Task RemoveNetworkAsync()
        {
            if (_networks.SelectedItem is not string name) { MessageBox.Show(this, "Pick a network in the list.", "Logger WiFi"); return; }
            await RunAsync(w => w.Remove(name));
        }

        private async Task SaveUpdatePasswordAsync()
        {
            string password = _updatePassword.Text;
            await RunAsync(w => w.SetUpdatePassword(password), () =>
            {
                // Kept on this PC too, where rc-logger's tools\big-logger-wifi.ps1 -Update reads it.
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BigLogger");
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "ota.txt"), password);
                _updatePassword.Clear();
            });
        }
    }
}
