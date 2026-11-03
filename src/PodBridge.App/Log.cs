namespace PodBridge.App;

/// <summary>Tiny local-only error log (%LOCALAPPDATA%\PodBridge\error.log). Nothing is ever sent anywhere.</summary>
internal static class Log
{
    private static readonly object Gate = new();

    public static void Error(string message, Exception? ex = null)
    {
        try
        {
            var dir = PodBridge.Core.Settings.SettingsStore.DefaultDirectory();
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "error.log");

            lock (Gate)
            {
                // Keep the file small: start over once it passes 256 KB.
                if (File.Exists(path) && new FileInfo(path).Length > 256 * 1024)
                {
                    File.Delete(path);
                }
                File.AppendAllText(path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{(ex is null ? "" : "\n" + ex)}\n");
            }
        }
        catch
        {
            // Logging must never throw.
        }
    }
}
