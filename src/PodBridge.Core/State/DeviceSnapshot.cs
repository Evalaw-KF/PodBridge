using PodBridge.Core.Bluetooth;

namespace PodBridge.Core.State;

/// <summary>Smoothed view of the nearby device, ready for the UI to display.</summary>
public sealed record DeviceSnapshot
{
    public required AppleDeviceModel Model { get; init; }
    public required ushort RawModelId { get; init; }

    public int? LeftBattery { get; init; }
    public int? RightBattery { get; init; }
    public int? CaseBattery { get; init; }

    public bool LeftCharging { get; init; }
    public bool RightCharging { get; init; }
    public bool CaseCharging { get; init; }

    public bool LeftInEar { get; init; }
    public bool RightInEar { get; init; }

    public bool LidOpen { get; init; }

    /// <summary>Smoothed signal strength in dBm.</summary>
    public double Rssi { get; init; }

    /// <summary>When the last accepted advert arrived.</summary>
    public DateTimeOffset LastSeen { get; init; }

    public DeviceKind Kind => AppleDeviceCatalog.GetKind(Model);
    public string DisplayName => AppleDeviceCatalog.GetDisplayName(Model);

    /// <summary>Lowest known earbud/headphone battery, used for the tray tooltip and icon. Null if none known.</summary>
    public int? LowestBudBattery
    {
        get
        {
            int? best = null;
            foreach (var v in new[] { LeftBattery, RightBattery })
            {
                if (v is int x && (best is null || x < best))
                {
                    best = x;
                }
            }
            return best;
        }
    }
}
