namespace PodBridge.Core.Audio;

/// <summary>A snapshot of the audio situation, used by the switching decisions.</summary>
public sealed class OutputContext
{
    private readonly HashSet<string> _airPodsIds;

    public OutputContext(IReadOnlyList<EndpointInfo> endpoints, string? defaultId, string? nameMatch)
    {
        Endpoints = endpoints;
        DefaultId = defaultId;
        var matches = EndpointMatcher.FindAll(endpoints, nameMatch);
        _airPodsIds = matches.Select(m => m.Id).ToHashSet();
        AirPods = matches.FirstOrDefault();
        DefaultIsAirPods = defaultId is not null && _airPodsIds.Contains(defaultId);
    }

    public IReadOnlyList<EndpointInfo> Endpoints { get; }

    /// <summary>Id of the current default render endpoint, or null if there is none.</summary>
    public string? DefaultId { get; }

    /// <summary>The best AirPods endpoint (may be unplugged), or null if none matches.</summary>
    public EndpointInfo? AirPods { get; }

    /// <summary>True if the default endpoint is any endpoint matching the AirPods name (music or call profile).</summary>
    public bool DefaultIsAirPods { get; }

    public EndpointInfo? Find(string? id) =>
        id is null ? null : Endpoints.FirstOrDefault(e => e.Id == id);

    public bool IsAirPodsId(string id) => _airPodsIds.Contains(id);

    /// <summary>True if the endpoint exists, is active, and is not one of the AirPods endpoints.</summary>
    public bool IsUsableSpeaker(string? id) => Find(id) is { IsActive: true } e && !IsAirPodsId(e.Id);
}
