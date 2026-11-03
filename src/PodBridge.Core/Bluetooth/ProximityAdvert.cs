namespace PodBridge.Core.Bluetooth;

/// <summary>
/// One decoded Apple proximity-pairing advertisement. Battery values are percentages
/// (0-100 in steps of 10) or <c>null</c> when the device reports "unknown".
/// Left/right are already corrected for the "flipped" primary-bud handling.
/// </summary>
public sealed record ProximityAdvert
{
    public required ushort RawModelId { get; init; }
    public required AppleDeviceModel Model { get; init; }

    /// <summary>The manufacturer payload as received (company ID already removed), for diagnostics.</summary>
    public byte[] Raw { get; init; } = Array.Empty<byte>();

    public int? LeftBattery { get; init; }
    public int? RightBattery { get; init; }
    public int? CaseBattery { get; init; }

    public bool LeftCharging { get; init; }
    public bool RightCharging { get; init; }
    public bool CaseCharging { get; init; }

    public bool LeftInEar { get; init; }
    public bool RightInEar { get; init; }

    /// <summary>Status bit 0x04: set only while both buds are in the case. Both buds are then reported not in ear.</summary>
    public bool BothInCase { get; init; }

    public bool LidOpen { get; init; }

    /// <summary>True when the advert's primary bud is the right one, so nibbles were swapped to compensate.</summary>
    public bool Flipped { get; init; }
}
