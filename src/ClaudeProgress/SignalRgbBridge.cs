namespace ClaudeProgress;

/// <summary>
/// Wysyła stan do efektu „Claude Status” w SignalRGB przez jego lokalne API:
/// POST http://localhost:16034/canvas/event?sender=claudeprogress&amp;event=&lt;stan&gt;
/// Stany: working, input, done, idle. Błędy są ignorowane (SignalRGB może być wyłączony).
/// </summary>
internal sealed class SignalRgbBridge : IDisposable
{
    public const string Working = "working";
    public const string Input = "input";
    public const string Done = "done";
    public const string Idle = "idle";

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(2) };

    public bool Enabled { get; set; }

    public async Task SendAsync(string state)
    {
        if (!Enabled)
        {
            return;
        }

        try
        {
            var url = $"http://localhost:16034/canvas/event?sender=claudeprogress&event={state}";
            using var response = await _http.PostAsync(url, null).ConfigureAwait(false);
        }
        catch
        {
            // SignalRGB nie działa albo efekt nie jest aktywny — nic nie robimy.
        }
    }

    public void Dispose() => _http.Dispose();
}
