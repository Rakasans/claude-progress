using System.Drawing.Drawing2D;
using System.Drawing.Text;
using Microsoft.Win32;

namespace ClaudeProgress;

/// <summary>Ikona w zasobniku, cykliczne sprawdzanie repozytorium i powiadomienia.</summary>
internal sealed class TrayApp : ApplicationContext
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValue = "ClaudeProgress";

    private readonly Settings _settings;
    private readonly NotifyIcon _tray;
    private readonly TasksForm _form;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly ToolStripMenuItem _autostartItem;

    private readonly Icon _iconIdle = MakeIcon(Color.FromArgb(128, 128, 128));
    private readonly Icon _iconBusy = MakeIcon(Color.FromArgb(0, 102, 204));
    private readonly Icon _iconAttention = MakeIcon(Color.FromArgb(230, 126, 0));
    private readonly Icon _iconError = MakeIcon(Color.FromArgb(200, 40, 40));

    private GitHubClient? _client;
    private readonly Dictionary<int, string> _lastKeys = new();
    private bool _baselined;
    private bool _polling;
    private bool _askedForToken;
    private string? _balloonUrl;

    private readonly SignalRgbBridge _rgb = new();
    private readonly System.Windows.Forms.Timer _doneTimer;
    private readonly ToolStripMenuItem _rgbItem;
    private DateTime _doneUntil = DateTime.MinValue;
    private int _waiting;
    private int _running;

    public TrayApp()
    {
        _settings = Settings.Load();
        _rgb.Enabled = _settings.SignalRgb;
        _form = new TasksForm();
        // Wymusza utworzenie uchwytu okna na wątku UI.
        _ = _form.Handle;

        _autostartItem = new ToolStripMenuItem("Uruchamiaj z Windows", null, (_, _) => ToggleAutostart())
        {
            Checked = IsAutostartEnabled(),
        };

        _rgbItem = new ToolStripMenuItem("Wentylator RGB (SignalRGB)", null, async (_, _) => await ToggleRgbAsync())
        {
            Checked = _settings.SignalRgb,
        };

        // Po upływie czasu zielonego przelicza stan wentylatora.
        _doneTimer = new System.Windows.Forms.Timer();
        _doneTimer.Tick += (_, _) =>
        {
            _doneTimer.Stop();
            UpdateRgb();
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("Pokaż zadania", null, (_, _) => _form.ShowNearTray());
        menu.Items.Add("Odśwież teraz", null, async (_, _) => await PollAsync());
        menu.Items.Add("Otwórz repozytorium", null, (_, _) =>
            TasksForm.OpenUrl($"https://github.com/{_settings.Owner}/{_settings.Repo}/issues"));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Ustaw token GitHub…", null, async (_, _) => { if (PromptForToken()) await PollAsync(); });
        menu.Items.Add(_autostartItem);
        menu.Items.Add(_rgbItem);
        menu.Items.Add("Otwórz folder ustawień", null, (_, _) =>
        {
            Directory.CreateDirectory(Settings.Folder);
            TasksForm.OpenUrl(Settings.Folder);
        });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Zakończ", null, (_, _) => ExitThread());

        _tray = new NotifyIcon
        {
            Icon = _iconIdle,
            Text = "Claude Progress",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _tray.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                if (_form.Visible) _form.Hide(); else _form.ShowNearTray();
            }
        };
        _tray.BalloonTipClicked += (_, _) =>
        {
            if (_balloonUrl != null) TasksForm.OpenUrl(_balloonUrl);
        };

        _timer = new System.Windows.Forms.Timer { Interval = _settings.PollSeconds * 1000 };
        _timer.Tick += async (_, _) => await PollAsync();

        var token = CredentialStore.Read();
        if (string.IsNullOrEmpty(token))
        {
            if (!PromptForToken())
            {
                SetError("Brak tokenu — kliknij prawym i wybierz „Ustaw token GitHub…”.");
            }
        }
        else
        {
            CreateClient(token);
        }

        _timer.Start();
        _ = PollAsync();
    }

    private void CreateClient(string token)
    {
        _client?.Dispose();
        _client = new GitHubClient(_settings.Owner, _settings.Repo, token);
    }

    private bool PromptForToken()
    {
        using var dialog = new TokenDialog(_settings.Owner, _settings.Repo);
        if (dialog.ShowDialog() != DialogResult.OK || dialog.Token.Length == 0)
        {
            return false;
        }

        try
        {
            CredentialStore.Write(dialog.Token);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Nie udało się zapisać tokenu: " + ex.Message, "Claude Progress",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }

        CreateClient(dialog.Token);
        _askedForToken = false;
        return true;
    }

    private async Task PollAsync()
    {
        if (_polling || _client == null)
        {
            return;
        }

        _polling = true;
        try
        {
            var all = await _client.FetchAsync();
            ApplyResults(all);
        }
        catch (UnauthorizedException ex)
        {
            SetError(ex.Message);
            if (!_askedForToken)
            {
                _askedForToken = true;
                Notify("Claude Progress: problem z tokenem", ex.Message + " Kliknij prawym na ikonę, żeby ustawić nowy.",
                    ToolTipIcon.Error, null);
            }
        }
        catch (Exception ex)
        {
            // Chwilowy brak sieci itp. — spróbujemy przy następnym sprawdzeniu.
            SetError("Błąd połączenia: " + ex.Message);
        }
        finally
        {
            _polling = false;
        }
    }

    private void ApplyResults(List<TaskItem> all)
    {
        var events = new List<TaskItem>();
        foreach (var item in all)
        {
            // Zmiana pytania przy tym samym stanie needs-input też powiadamia.
            var key = item.State == TaskState.NeedsInput ? item.State + "\n" + item.Body : item.State;
            _lastKeys.TryGetValue(item.Number, out var previous);

            if (_baselined && previous != key && item.State is TaskState.NeedsInput or TaskState.Done)
            {
                events.Add(item);
            }

            _lastKeys[item.Number] = key;
        }
        _baselined = true;

        // Widoczne: wszystkie otwarte + zakończone w ciągu ostatnich 24 h.
        var cutoff = DateTime.Now.AddHours(-24);
        var visible = all
            .Where(i => i.State is TaskState.InProgress or TaskState.NeedsInput or TaskState.Unknown
                        || (i.State == TaskState.Done && i.UpdatedAt >= cutoff))
            .OrderBy(i => i.State switch
            {
                TaskState.NeedsInput => 0,
                TaskState.InProgress => 1,
                TaskState.Unknown => 2,
                _ => 3,
            })
            .ThenByDescending(i => i.UpdatedAt)
            .ToList();

        _form.SetItems(visible);
        _form.SetStatus($"Ostatnie sprawdzenie: {DateTime.Now:HH:mm:ss}");

        var waiting = visible.Count(i => i.State == TaskState.NeedsInput);
        var running = visible.Count(i => i.State == TaskState.InProgress);
        _waiting = waiting;
        _running = running;

        if (events.Any(e => e.State == TaskState.Done))
        {
            var glow = Math.Max(5, _settings.DoneGlowSeconds);
            _doneUntil = DateTime.Now.AddSeconds(glow);
            _doneTimer.Stop();
            _doneTimer.Interval = glow * 1000 + 500;
            _doneTimer.Start();
        }

        // Wysyłane przy każdym sprawdzeniu, żeby efekt odzyskał stan po restarcie SignalRGB.
        UpdateRgb();

        _tray.Icon = waiting > 0 ? _iconAttention : running > 0 ? _iconBusy : _iconIdle;
        var tooltip = waiting > 0 || running > 0
            ? $"Claude: {running} w toku, {waiting} czeka na Ciebie"
            : "Claude: brak aktywnych zadań";
        _tray.Text = TasksForm.Truncate(tooltip, 63);

        ShowEvents(events);
    }

    private void ShowEvents(List<TaskItem> events)
    {
        if (events.Count == 0)
        {
            return;
        }

        if (events.Count > 1)
        {
            var asks = events.Count(e => e.State == TaskState.NeedsInput);
            var dones = events.Count - asks;
            var parts = new List<string>();
            if (asks > 0) parts.Add($"{asks} czeka na Twoją decyzję");
            if (dones > 0) parts.Add($"{dones} zakończone");
            Notify("Claude: kilka zmian", string.Join(", ", parts) + ". Kliknij, żeby zobaczyć.",
                asks > 0 ? ToolTipIcon.Warning : ToolTipIcon.Info,
                $"https://github.com/{_settings.Owner}/{_settings.Repo}/issues");
            return;
        }

        var item = events[0];
        var detail = TasksForm.FirstLine(item.Body);
        if (item.State == TaskState.NeedsInput)
        {
            Notify("Claude czeka na Ciebie: " + item.Name,
                detail.Length > 0 ? detail : "Potrzebna Twoja decyzja.",
                ToolTipIcon.Warning, item.Url);
        }
        else
        {
            Notify("Zakończone: " + item.Name,
                detail.Length > 0 ? detail : "Zadanie skończone.",
                ToolTipIcon.Info, item.Url);
        }
    }

    private void Notify(string title, string text, ToolTipIcon icon, string? url)
    {
        _balloonUrl = url;
        _tray.ShowBalloonTip(10_000, TasksForm.Truncate(title, 63), TasksForm.Truncate(text, 255), icon);
    }

    private void SetError(string message)
    {
        _tray.Icon = _iconError;
        _tray.Text = TasksForm.Truncate("Claude Progress: " + message, 63);
        _form.SetStatus(message);
    }

    private string CurrentRgbState() =>
        _waiting > 0 ? SignalRgbBridge.Input
        : _running > 0 ? SignalRgbBridge.Working
        : DateTime.Now < _doneUntil ? SignalRgbBridge.Done
        : SignalRgbBridge.Idle;

    private void UpdateRgb() => _ = _rgb.SendAsync(CurrentRgbState());

    private async Task ToggleRgbAsync()
    {
        if (_settings.SignalRgb)
        {
            // Przed wyłączeniem przywracamy kolor bazowy.
            await _rgb.SendAsync(SignalRgbBridge.Idle);
        }

        _settings.SignalRgb = !_settings.SignalRgb;
        _settings.Save();
        _rgb.Enabled = _settings.SignalRgb;
        _rgbItem.Checked = _settings.SignalRgb;
        UpdateRgb();
    }

    private static bool IsAutostartEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(RunValue) != null;
    }

    private void ToggleAutostart()
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (IsAutostartEnabled())
        {
            key.DeleteValue(RunValue, false);
        }
        else if (Environment.ProcessPath is string path)
        {
            key.SetValue(RunValue, $"\"{path}\"");
        }
        _autostartItem.Checked = IsAutostartEnabled();
    }

    private static Icon MakeIcon(Color color)
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.Transparent);
            using var brush = new SolidBrush(color);
            g.FillEllipse(brush, 1, 1, 30, 30);
            using var font = new Font("Segoe UI", 17f, FontStyle.Bold, GraphicsUnit.Pixel);
            using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("C", font, Brushes.White, new RectangleF(0, 0, 32, 32), format);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    protected override void ExitThreadCore()
    {
        _timer.Stop();
        _doneTimer.Stop();
        // Przy zamknięciu wentylator wraca do koloru bazowego.
        _rgb.SendAsync(SignalRgbBridge.Idle).Wait(2500);
        _rgb.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        _client?.Dispose();
        _form.Dispose();
        base.ExitThreadCore();
    }
}
