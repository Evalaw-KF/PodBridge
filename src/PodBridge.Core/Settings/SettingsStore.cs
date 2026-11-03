using System.Text.Json;
using System.Text.Json.Serialization;

namespace PodBridge.Core.Settings;

/// <summary>Reads and writes <see cref="AppSettings"/> as JSON in a directory chosen by the caller.</summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public SettingsStore(string directory)
    {
        Directory = directory;
        FilePath = Path.Combine(directory, "settings.json");
    }

    public string Directory { get; }
    public string FilePath { get; }

    /// <summary>The standard location: %LOCALAPPDATA%\PodBridge on Windows.</summary>
    public static string DefaultDirectory() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PodBridge");

    /// <summary>Loads settings. A missing, empty or corrupt file yields defaults rather than an error.</summary>
    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return new AppSettings();
            }
            var json = File.ReadAllText(FilePath);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            return (loaded ?? new AppSettings()).Normalized();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            return new AppSettings();
        }
    }

    /// <summary>Saves settings atomically (write a temp file, then move it over the real one).</summary>
    public void Save(AppSettings settings)
    {
        System.IO.Directory.CreateDirectory(Directory);
        var json = JsonSerializer.Serialize(settings.Normalized(), JsonOptions);
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, json);
        File.Move(temp, FilePath, overwrite: true);
    }
}
