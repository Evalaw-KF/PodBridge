namespace PodBridge.Core.Audio;

/// <summary>Mirrors the DEVICE_STATE_* bit flags of the Windows MMDevice API.</summary>
[Flags]
public enum EndpointState
{
    None = 0,
    Active = 0x1,
    Disabled = 0x2,
    NotPresent = 0x4,
    Unplugged = 0x8,
}

/// <summary>An audio render endpoint as seen by the audio service. Plain data, no Windows types.</summary>
public sealed record EndpointInfo(string Id, string Name, EndpointState State)
{
    public bool IsActive => (State & EndpointState.Active) != 0;
    public bool IsUnplugged => (State & EndpointState.Unplugged) != 0;
}
