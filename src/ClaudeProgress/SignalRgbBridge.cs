namespace ClaudeProgress;

/// <summary>
/// Wysyła stan do efektu „Claude Status” w SignalRGB przez jego lokalne API:
/// POST http://localhost:16034/canvas/event?sender=claudeprogress&amp;event=&lt;stan&gt;
/// Stany: working, input, done, idle. Wynik ostatnich prób trafia do %APPDATA%\ClaudeProgress\signalrgb.log.
/// </summary>
internal sealed class SignalRgbBridge : IDisposable
{
    public const string Working = "working";
    public const string Input = "input";
    public const string Done = "done";
    public const string Idle = "idle";

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(2) };
    private readonly string _logPath = Path.Combine(Settings.Folder, "signalrgb.log");
    private string? _lastLogged;

    public bool Enabled { get; set; }

    public async Task SendAsync(string state)
    {
        if (!Enabled)
        {
            return;
        }

        string result;
        try
        {
            var url = $"http://localhost:16034/canvas/event?sender=claudeprogress&event={state}";
            // Pusta treść, żeby żądanie miało Content-Length: 0 (jak w PowerShellu).
            using var content = new StringContent(string.Empty);
            using var response = await _http.PostAsync(url, content).ConfigureAwait(false);
            result = $"HTTP {(int)response.StatusCode}";
        }
        catch (Exception ex)
        {
            result = "błąd: " + ex.GetBaseException().Message;
        }

        Log(state, result);
    }

    private void Log(string state, string result)
    {
        // Zapisujemy tylko zmiany, żeby log nie rósł co 30 sekund.
        var line = $"{state} -> {result}";
        if (line == _lastLogged)
        {
            return;
        }

        _lastLogged = line;
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

    public void Dispose() => _http.Dispose();
}
