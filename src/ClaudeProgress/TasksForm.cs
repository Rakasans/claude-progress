using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ClaudeProgress;

/// <summary>Okienko z listą zadań i paskami postępu.</summary>
internal sealed class TasksForm : Form
{
    private readonly FlowLayoutPanel _list;
    private readonly Label _status;
    private IReadOnlyList<TaskItem> _items = Array.Empty<TaskItem>();

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    private const int PBM_SETSTATE = 0x0410;
    private const int PBST_PAUSED = 0x0003; // żółty pasek

    public TasksForm()
    {
        Text = "Claude — postęp zadań";
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 9f);
        MinimumSize = new Size(320, 200);
        Size = new Size(420, 380);
        BackColor = SystemColors.Window;

        _status = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 24,
            Padding = new Padding(8, 4, 8, 4),
            ForeColor = SystemColors.GrayText,
            Text = "Sprawdzam...",
        };

        _list = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(8),
        };
        _list.Resize += (_, _) => ResizeRows();

        Controls.Add(_list);
        Controls.Add(_status);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // Zamknięcie okna chowa je do zasobnika.
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnFormClosing(e);
    }

    public void ShowNearTray()
    {
        var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 720);
        Location = new Point(area.Right - Width - 12, area.Bottom - Height - 12);
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    public void SetStatus(string text) => _status.Text = text;

    public void SetItems(IReadOnlyList<TaskItem> items)
    {
        _items = items;
        _list.SuspendLayout();
        foreach (Control c in _list.Controls)
        {
            c.Dispose();
        }
        _list.Controls.Clear();

        if (items.Count == 0)
        {
            _list.Controls.Add(new Label
            {
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
                Margin = new Padding(4, 12, 4, 4),
                Text = "Brak aktywnych zadań.",
            });
        }
        else
        {
            foreach (var item in items)
            {
                _list.Controls.Add(BuildRow(item));
            }
        }

        _list.ResumeLayout();
        ResizeRows();
    }

    private int RowWidth => Math.Max(200, _list.ClientSize.Width - _list.Padding.Horizontal - 6);

    private void ResizeRows()
    {
        foreach (Control c in _list.Controls)
        {
            if (c is Panel)
            {
                c.Width = RowWidth;
            }
        }
    }

    private Control BuildRow(TaskItem item)
    {
        var row = new Panel
        {
            Width = RowWidth,
            Height = 64,
            Margin = new Padding(0, 0, 0, 8),
            Cursor = Cursors.Hand,
        };

        var (statusText, statusColor) = item.State switch
        {
            TaskState.NeedsInput => ("Czeka na Ciebie", Color.FromArgb(196, 110, 0)),
            TaskState.Done => ("Zakończone", Color.FromArgb(30, 130, 60)),
            TaskState.InProgress => ("W toku", Color.FromArgb(0, 102, 204)),
            _ => ("Otwarte", SystemColors.GrayText),
        };

        if (item.Total is int total && item.Step is int step)
        {
            statusText += $" · {Math.Min(step, total)}/{total}";
        }

        var name = new Label
        {
            Left = 0, Top = 0, Height = 20,
            Width = row.Width - 130,
            Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right,
            Font = new Font(Font, FontStyle.Bold),
            AutoEllipsis = true,
            Text = item.Name.Length > 0 ? item.Name : $"Zadanie #{item.Number}",
        };

        var status = new Label
        {
            Top = 0, Height = 20, Width = 125,
            Left = row.Width - 125,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            TextAlign = ContentAlignment.TopRight,
            ForeColor = statusColor,
            Text = statusText,
        };

        var bar = new ProgressBar
        {
            Left = 0, Top = 22, Height = 12,
            Width = row.Width,
            Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right,
            Minimum = 0,
            Maximum = 100,
        };

        if (item.State == TaskState.Done)
        {
            bar.Value = 100;
        }
        else if (item.Total is int t && item.Step is int s)
        {
            bar.Value = Math.Clamp((int)Math.Round(100.0 * s / t), 0, 100);
        }
        else if (item.State == TaskState.InProgress)
        {
            bar.Style = ProgressBarStyle.Marquee;
            bar.MarqueeAnimationSpeed = 40;
        }

        if (item.State == TaskState.NeedsInput)
        {
            bar.HandleCreated += (_, _) => SendMessage(bar.Handle, PBM_SETSTATE, (IntPtr)PBST_PAUSED, IntPtr.Zero);
        }

        var detail = new Label
        {
            Left = 0, Top = 38, Height = 20,
            Width = row.Width,
            Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right,
            ForeColor = SystemColors.GrayText,
            AutoEllipsis = true,
            Text = FirstLine(item.Body) is { Length: > 0 } line
                ? line
                : $"Aktualizacja {item.UpdatedAt:HH:mm}",
        };

        row.Controls.AddRange(new Control[] { name, status, bar, detail });

        void Open(object? sender, EventArgs e) => OpenUrl(item.Url);
        row.Click += Open;
        foreach (Control c in row.Controls)
        {
            c.Click += Open;
            c.Cursor = Cursors.Hand;
        }

        var tip = new ToolTip();
        tip.SetToolTip(detail, item.Body.Length > 0 ? Truncate(item.Body, 500) : "Kliknij, żeby otworzyć issue");

        return row;
    }

    internal static string FirstLine(string text)
    {
        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.Trim().TrimStart('#', '>', '*', '-', ' ');
            if (trimmed.Length > 0)
            {
                return trimmed;
            }
        }
        return "";
    }

    internal static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..(max - 1)] + "…";

    internal static void OpenUrl(string url)
    {
        if (string.IsNullOrEmpty(url)) return;
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // Brak przeglądarki domyślnej: ignorujemy.
        }
    }
}
