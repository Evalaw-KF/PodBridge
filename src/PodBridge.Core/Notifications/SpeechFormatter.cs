using System.Text;
using System.Text.RegularExpressions;

namespace PodBridge.Core.Notifications;

/// <summary>Builds the sentence that is spoken for a notification.</summary>
public static partial class SpeechFormatter
{
    public const int MaxLength = 300;

    [GeneratedRegex(@"https?://\S+", RegexOptions.IgnoreCase)]
    private static partial Regex UrlPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    /// <summary>"Messages. Anna. See you at 5" (full) or "Messages. Anna" (app name and title only).</summary>
    public static string Format(NotificationInfo info, bool fullText)
    {
        var parts = new List<string>();
        Add(parts, info.AppName);
        Add(parts, info.Title);
        if (fullText)
        {
            Add(parts, info.Body);
        }

        var sb = new StringBuilder();
        foreach (var part in parts)
        {
            if (sb.Length > 0)
            {
                sb.Append(EndsWithPunctuation(sb) ? " " : ". ");
            }
            sb.Append(part);
        }

        string text = sb.ToString();
        if (text.Length > MaxLength)
        {
            text = text[..MaxLength].TrimEnd() + "…";
        }
        return text;
    }

    private static void Add(List<string> parts, string? raw)
    {
        string clean = Clean(raw);
        if (clean.Length == 0)
        {
            return;
        }
        if (parts.Any(p => string.Equals(p, clean, StringComparison.OrdinalIgnoreCase)))
        {
            return; // title equal to the app name or text: say it once
        }
        parts.Add(clean);
    }

    /// <summary>Removes web addresses (they sound awful) and control characters, and collapses spaces.</summary>
    public static string Clean(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }
        string text = UrlPattern().Replace(raw, " link ");
        text = new string(text.Where(c => !char.IsControl(c) || char.IsWhiteSpace(c)).ToArray());
        return Whitespace().Replace(text, " ").Trim();
    }

    private static bool EndsWithPunctuation(StringBuilder sb) =>
        sb.Length > 0 && sb[^1] is '.' or '!' or '?' or ':' or ';' or '…';
}
