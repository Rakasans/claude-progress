using System.Text.Json;

namespace ClaudeProgress;

/// <summary>Ustawienia w %APPDATA%\ClaudeProgress\settings.json (bez tokenu).</summary>
public sealed class Settings
{
    public string Owner { get; set; } = "Rakasans";
    public string Repo { get; set; } = "claude-progress";
    public int PollSeconds { get; set; } = 30;

    /// <summary>Sterowanie wentylatorem przez efekt „Claude Status” w SignalRGB.</summary>
    public bool SignalRgb { get; set; } = true;

    /// <summary>Jak długo świeci zielony po zakończeniu zadania.</summary>
    public int DoneGlowSeconds { get; set; } = 120;

    public static string Folder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClaudeProgress");

    private static string FilePath => Path.Combine(Folder, "settings.json");

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var loaded = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath));
                if (loaded != null)
                {
                    loaded.PollSeconds = Math.Clamp(loaded.PollSeconds, 10, 3600);
                    return loaded;
                }
            }
        }
        catch
        {
            // Uszkodzony plik: wracamy do domyślnych.
        }

        var settings = new Settings();
        settings.Save();
        return settings;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // Brak zapisu ustawień nie blokuje działania aplikacji.
        }
    }
}
