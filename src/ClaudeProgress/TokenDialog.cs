using System.Diagnostics;

namespace ClaudeProgress;

/// <summary>Okno do wklejenia tokenu GitHub (fine-grained, Issues: read-only).</summary>
internal sealed class TokenDialog : Form
{
    private readonly TextBox _tokenBox;

    public string Token => _tokenBox.Text.Trim();

    public TokenDialog(string owner, string repo)
    {
        Text = "Claude Progress — token GitHub";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(460, 190);
        Font = new Font("Segoe UI", 9f);
        TopMost = true;

        var info = new Label
        {
            Left = 12, Top = 12, Width = 436, Height = 60,
            Text = $"Wklej token GitHub (fine-grained) z dostępem tylko do repozytorium {owner}/{repo} " +
                   "i uprawnieniem Issues: Read-only. Token trafi do Menedżera poświadczeń Windows.",
        };

        var link = new LinkLabel
        {
            Left = 12, Top = 74, Width = 436, Height = 20,
            Text = "Utwórz token na github.com",
        };
        link.LinkClicked += (_, _) => Process.Start(new ProcessStartInfo(
            "https://github.com/settings/personal-access-tokens/new") { UseShellExecute = true });

        _tokenBox = new TextBox
        {
            Left = 12, Top = 102, Width = 436,
            UseSystemPasswordChar = true,
            PlaceholderText = "github_pat_...",
        };

        var ok = new Button { Text = "Zapisz", Left = 292, Top = 146, Width = 75, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Anuluj", Left = 373, Top = 146, Width = 75, DialogResult = DialogResult.Cancel };

        AcceptButton = ok;
        CancelButton = cancel;
        Controls.AddRange(new Control[] { info, link, _tokenBox, ok, cancel });
    }
}
