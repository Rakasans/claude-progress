using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ClaudeProgress;

public static class TaskState
{
    public const string InProgress = "in-progress";
    public const string NeedsInput = "needs-input";
    public const string Done = "done";
    public const string Closed = "closed";
    public const string Unknown = "";
}

public sealed record TaskItem(
    int Number,
    string Name,
    int? Step,
    int? Total,
    string State,
    string Body,
    string Url,
    DateTime UpdatedAt);

public sealed class UnauthorizedException : Exception
{
    public UnauthorizedException(string message) : base(message) { }
}

/// <summary>Czyta issues z repozytorium kanału. Jedno zapytanie na sprawdzenie, z ETag (304 nie zużywa limitu).</summary>
public sealed partial class GitHubClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly string _owner;
    private readonly string _repo;
    private EntityTagHeaderValue? _etag;
    private List<TaskItem> _last = new();

    [GeneratedRegex(@"\[\s*(\d+)\s*/\s*(\d+)\s*\]")]
    private static partial Regex ProgressRegex();

    public GitHubClient(string owner, string repo, string token)
    {
        _owner = owner;
        _repo = repo;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("ClaudeProgress/1.0");
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        _http.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public string IssuesUrl => $"https://github.com/{_owner}/{_repo}/issues";

    public async Task<List<TaskItem>> FetchAsync(CancellationToken ct = default)
    {
        var url = $"https://api.github.com/repos/{_owner}/{_repo}/issues?state=all&sort=updated&direction=desc&per_page=50";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (_etag != null)
        {
            request.Headers.IfNoneMatch.Add(_etag);
        }

        using var response = await _http.SendAsync(request, ct);

        if (response.StatusCode == HttpStatusCode.NotModified)
        {
            return _last;
        }

        if (response.StatusCode is HttpStatusCode.Unauthorized)
        {
            throw new UnauthorizedException("Token jest nieprawidłowy albo wygasł.");
        }

        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden)
        {
            throw new UnauthorizedException($"Brak dostępu do {_owner}/{_repo} (HTTP {(int)response.StatusCode}). Sprawdź uprawnienia tokenu.");
        }

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        var items = new List<TaskItem>();
        foreach (var issue in doc.RootElement.EnumerateArray())
        {
            if (issue.TryGetProperty("pull_request", out _))
            {
                continue;
            }

            items.Add(Parse(issue));
        }

        _etag = response.Headers.ETag;
        _last = items;
        return items;
    }

    private static TaskItem Parse(JsonElement issue)
    {
        var title = issue.GetProperty("title").GetString() ?? "";
        var body = issue.TryGetProperty("body", out var b) && b.ValueKind == JsonValueKind.String ? b.GetString() ?? "" : "";
        var closed = issue.GetProperty("state").GetString() == "closed";

        var labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var label in issue.GetProperty("labels").EnumerateArray())
        {
            var name = label.TryGetProperty("name", out var n) ? n.GetString() : null;
            if (name != null)
            {
                labels.Add(name);
            }
        }

        string state;
        if (labels.Contains(TaskState.Done)) state = TaskState.Done;
        else if (closed) state = TaskState.Closed;
        else if (labels.Contains(TaskState.NeedsInput)) state = TaskState.NeedsInput;
        else if (labels.Contains(TaskState.InProgress)) state = TaskState.InProgress;
        else state = TaskState.Unknown;

        int? step = null, total = null;
        var match = ProgressRegex().Match(title);
        if (match.Success)
        {
            step = int.Parse(match.Groups[1].Value);
            total = int.Parse(match.Groups[2].Value);
            if (total <= 0) { step = null; total = null; }
        }

        var cleanName = ProgressRegex().Replace(title, "").Trim();

        return new TaskItem(
            issue.GetProperty("number").GetInt32(),
            cleanName,
            step,
            total,
            state,
            body.Trim(),
            issue.GetProperty("html_url").GetString() ?? "",
            issue.GetProperty("updated_at").GetDateTime().ToLocalTime());
    }

    public void Dispose() => _http.Dispose();
}
