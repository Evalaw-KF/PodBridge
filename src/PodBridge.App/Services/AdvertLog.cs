using System.Text;
using PodBridge.Core.Bluetooth;
using PodBridge.Core.Settings;

namespace PodBridge.App.Services;

/// <summary>
/// Diagnostics: keeps the last few distinct adverts in memory (for the Settings page) and appends
/// them to %LOCALAPPDATA%\PodBridge\adverts.log (capped at about 500 lines) so the file can be sent
/// for bit-mapping checks. Local only. Must be used on the UI thread.
/// </summary>
internal sealed class AdvertLog
{
    private const int Capacity = 20;
    private const int MaxFileLines = 500;

    private readonly List<string> _lines = new(); // newest first
    private byte[]? _last;
    private int _fileLines = -1; // -1 = not counted yet

    public static string FilePath => Path.Combine(SettingsStore.DefaultDirectory(), "adverts.log");

    /// <summary>Raised after a new distinct advert was recorded.</summary>
    public event Action? Changed;

    /// <summary>The recorded entries as text, newest first, one per line.</summary>
    public string Text => string.Join(Environment.NewLine, _lines);

    public void Record(ProximityAdvert advert, int rssi)
    {
        var raw = advert.Raw;
        if (raw.Length == 0 || (_last is not null && AdvertFormatter.SameHeader(_last, raw)))
        {
            return;
        }
        _last = raw;

        var line = $"{DateTime.Now:HH:mm:ss.fff} {rssi} dBm | {AdvertFormatter.Format(raw)}";
        _lines.Insert(0, line);
        if (_lines.Count > Capacity)
        {
            _lines.RemoveAt(_lines.Count - 1);
        }

        AppendToFile($"{DateTime.Now:yyyy-MM-dd} {line}");
        Changed?.Invoke();
    }

    private void AppendToFile(string line)
    {
        try
        {
            var path = FilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            if (_fileLines < 0)
            {
                _fileLines = File.Exists(path) ? File.ReadAllLines(path).Length : 0;
            }

            if (_fileLines >= MaxFileLines)
            {
                // Over the cap: keep the newest lines and start again from there.
                var keep = File.ReadAllLines(path).TakeLast(MaxFileLines - 100).ToList();
                File.WriteAllLines(path, keep, Encoding.UTF8);
                _fileLines = keep.Count;
            }

            File.AppendAllText(path, line + Environment.NewLine, Encoding.UTF8);
            _fileLines++;
        }
        catch (Exception ex)
        {
            Log.Error("Could not write adverts.log", ex);
            _fileLines = -1;
        }
    }
}
