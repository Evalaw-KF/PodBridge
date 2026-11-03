using Microsoft.Win32;

namespace PodBridge.App.Services;

/// <summary>Adds or removes PodBridge from the current user's "Run at sign-in" list (no admin rights needed).</summary>
internal static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "PodBridge";

    public static void Apply(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
                            ?? Registry.CurrentUser.CreateSubKey(RunKey);
            if (key is null)
            {
                return;
            }

            if (enabled)
            {
                var exe = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exe))
                {
                    key.SetValue(ValueName, $"\"{exe}\" --background");
                }
            }
            else if (key.GetValue(ValueName) is not null)
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        catch (Exception ex)
        {
            Log.Error("Could not update the start-with-Windows setting", ex);
        }
    }
}
