namespace PodBridge.Core.Audio;

/// <summary>Finds the AirPods among the audio endpoints by matching names.</summary>
public static class EndpointMatcher
{
    /// <summary>True if the endpoint's friendly name contains the pattern (case-insensitive). An empty pattern matches nothing.</summary>
    public static bool IsMatch(string? endpointName, string? pattern)
    {
        if (string.IsNullOrWhiteSpace(endpointName) || string.IsNullOrWhiteSpace(pattern))
        {
            return false;
        }
        return Normalize(endpointName).Contains(Normalize(pattern), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The "Headset" endpoint is the low-quality phone-call (hands-free) profile of the same earbuds.
    /// </summary>
    public static bool LooksLikeHandsFree(string name) =>
        name.Contains("Hands-Free", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Hands Free", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("Headset", StringComparison.OrdinalIgnoreCase);

    /// <summary>All endpoints (active or unplugged) whose name matches, best first: active before unplugged, music profile before call profile.</summary>
    public static IReadOnlyList<EndpointInfo> FindAll(IEnumerable<EndpointInfo> endpoints, string? pattern) =>
        endpoints
            .Where(e => (e.IsActive || e.IsUnplugged) && IsMatch(e.Name, pattern))
            .OrderByDescending(e => e.IsActive)
            .ThenBy(e => LooksLikeHandsFree(e.Name))
            .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>The single best matching endpoint, or null.</summary>
    public static EndpointInfo? Pick(IEnumerable<EndpointInfo> endpoints, string? pattern) =>
        FindAll(endpoints, pattern).FirstOrDefault();

    // Windows sometimes uses the typographic apostrophe in device names; treat it like a plain one.
    private static string Normalize(string s) => s.Replace('’', '\'').Trim();
}
