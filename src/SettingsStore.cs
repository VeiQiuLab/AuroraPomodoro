using System.IO;
using System.Text.Json;

namespace AuroraPomodoro;

/// <summary>
/// Loads/saves <see cref="PomodoroSettings"/> to
/// %LOCALAPPDATA%\AuroraPomodoro\settings.json (or an overridden root for tests).
/// Load/save only — never owns timer state and never becomes a source of truth.
/// Never throws on malformed/invalid content; falls back to defaults.
/// </summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _root;
    private readonly string _file;

    public SettingsStore(string? storageRoot = null)
    {
        _root = storageRoot
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AuroraPomodoro");
        _file = Path.Combine(_root, "settings.json");
    }

    public string FilePath => _file;

    /// <summary>Load settings; missing/corrupt/invalid content yields safe defaults.</summary>
    public PomodoroSettings Load()
    {
        try
        {
            if (!File.Exists(_file)) return PomodoroSettings.CreateDefault();

            string json = File.ReadAllText(_file);
            PomodoroSettings? loaded =
                JsonSerializer.Deserialize<PomodoroSettings>(json, JsonOptions);

            if (loaded is null) return PomodoroSettings.CreateDefault();

            loaded.Sanitize();
            return loaded;
        }
        catch (Exception ex)
        {
            // Diagnosable, non-fatal: fall back to defaults.
            Console.Error.WriteLine("[AuroraPomodoro] settings load failed: " + ex.Message);
            return PomodoroSettings.CreateDefault();
        }
    }

    /// <summary>Save atomically (temp file + move). Never leaves a half-written file.</summary>
    public void Save(PomodoroSettings settings)
    {
        settings.Sanitize();

        Directory.CreateDirectory(_root);

        string temp = _file + ".tmp";
        string json = JsonSerializer.Serialize(settings, JsonOptions);

        File.WriteAllText(temp, json);

        // Atomic replace; File.Move(overwrite) is atomic on the same volume.
        File.Move(temp, _file, overwrite: true);
    }
}
