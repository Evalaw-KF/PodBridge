using System.Text.RegularExpressions;

namespace PodBridge.Core.Display;

/// <summary>Rules for the optional custom headphone picture. The file lives in the PodBridge data folder.</summary>
public static partial class CustomPicture
{
    public const long MaxBytes = 10 * 1024 * 1024;
    public static readonly string[] Extensions = { ".png", ".jpg", ".jpeg", ".bmp", ".gif" };

    [GeneratedRegex(@"^custom-headphones-\d{1,19}\.(png|jpg|jpeg|bmp|gif)$", RegexOptions.IgnoreCase)]
    private static partial Regex StoredName();

    public static bool IsSupportedExtension(string? extension) =>
        extension is not null && Extensions.Contains(extension.Trim().ToLowerInvariant());

    /// <summary>The name a copied picture gets. Unique each time so Windows never shows an older cached image.</summary>
    public static string StoredFileName(string extension, long uniqueNumber) =>
        $"custom-headphones-{Math.Abs(uniqueNumber)}{extension.Trim().ToLowerInvariant()}";

    /// <summary>True only for names PodBridge itself would create (so a hand-edited settings file cannot point elsewhere).</summary>
    public static bool IsStoredName(string? name) => !string.IsNullOrEmpty(name) && StoredName().IsMatch(name);

    /// <summary>Checks a file the user picked. Returns null if fine, otherwise a message to show.</summary>
    public static string? Validate(string extension, long length)
    {
        if (!IsSupportedExtension(extension))
        {
            return "Please choose a PNG, JPG, BMP or GIF picture.";
        }
        if (length <= 0)
        {
            return "That file is empty.";
        }
        if (length > MaxBytes)
        {
            return "That picture is too big. Please choose one under 10 MB.";
        }
        return null;
    }
}
