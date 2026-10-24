using System.Diagnostics;

namespace ClaudeProgress;

/// <summary>
/// Pokazuje stan na wentylatorze, przełączając w SignalRGB jeden z czterech efektów
/// (Claude Idle / Praca / Decyzja / Koniec) przez SignalRgbLauncher.exe --url=effect/apply/...
/// Działa bez SignalRGB Pro. Efekt przełączany jest tylko przy zmianie stanu.
/// Wyniki trafiają do %APPDATA%\ClaudeProgress\signalrgb.log.
/// </summary>
internal sealed class SignalRgbBridge : IDisposable
{
    public const string Working = "working";
    public const string Input = "input";
    public const string Done = "done";
    public const string Idle = "idle";

    private static readonly Dictionary<string, string> EffectNames = new()
    {
        [Working] = "Claude Praca",
        [Input] = "Claude Decyzja",
        [Done] = "Claude Koniec",
        [Idle] = "Claude Idle",
    };

    private readonly string _logPath = Path.Combine(Settings.Folder, "signalrgb.log");
    private string? _lastApplied;
    private bool _enabled;

    public bool Enabled
    {
        get => _enabled;
        set
        {
            _enabled = value;
            // Po włączeniu wymuszamy ponowne ustawienie efektu.
            _lastApplied = null;
        }
    }

    public Task SendAsync(string state)
    {
        if (!_enabled || state == _lastApplied || !EffectNames.TryGetValue(state, out var effect))
        {
            return Task.CompletedTask;
        }

        var encoded = Uri.EscapeDataString(effect);
        var launcher = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VortxEngine", "SignalRgbLauncher.exe");

        try
        {
            if (File.Exists(launcher))
            {
                // ?-silentlaunch- nie wyciąga okna SignalRGB na wierzch.
                Process.Start(new ProcessStartInfo(launcher, $"--url=effect/apply/{encoded}?-silentlaunch-")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                })?.Dispose();
                Log($"{state} -> {effect} (launcher)");
            }
            else
            {
                Process.Start(new ProcessStartInfo($"signalrgb://effect/apply/{encoded}") { UseShellExecute = true })?.Dispose();
                Log($"{state} -> {effect} (signalrgb://, brak launchera w {launcher})");
            }

            _lastApplied = state;
        }
        catch (Exception ex)
        {
            Log($"{state} -> błąd: {ex.GetBaseException().Message}");
        }

        return Task.CompletedTask;
    }

    private void Log(string line)
    {
        try
        {
            Directory.CreateDirectory(Settings.Folder);
            File.AppendAllText(_logPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {line}{Environment.NewLine}");
        }
        catch
        {
            // Brak logu nie przeszkadza w działaniu.
        }
    }

    public void Dispose()
    {
    }
}
