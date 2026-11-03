namespace PodBridge.Core.Bluetooth;

/// <summary>Known Apple / Beats audio products that broadcast proximity-pairing adverts.</summary>
public enum AppleDeviceModel
{
    Unknown = 0,

    AirPods1,
    AirPods2,
    AirPods3,
    AirPods4,
    AirPods4Anc,
    AirPodsPro,
    AirPodsPro2Lightning,
    AirPodsPro2UsbC,
    AirPodsPro3,
    AirPodsMax,
    AirPodsMaxUsbC,

    PowerbeatsPro,
    Powerbeats3,
    BeatsX,
    BeatsSolo3,
    BeatsSoloPro,
    BeatsStudio3,
    BeatsFlex,
    BeatsStudioBuds,
    BeatsStudioBudsPlus,
    BeatsFitPro,
}

/// <summary>Broad physical shape of a device, which decides what batteries it reports.</summary>
public enum DeviceKind
{
    Unknown,
    /// <summary>Two earbuds and a charging case.</summary>
    Earbuds,
    /// <summary>Single over-ear / on-ear headset with one battery.</summary>
    Headphones,
}

/// <summary>Lookup of model IDs and friendly names.</summary>
public static class AppleDeviceCatalog
{
    private static readonly Dictionary<ushort, AppleDeviceModel> ById = new()
    {
        // IDs are the 16-bit big-endian value of advert bytes 3..4 (for example bytes 0E 20 -> 0x0E20).
        [0x0220] = AppleDeviceModel.AirPods1,
        [0x0F20] = AppleDeviceModel.AirPods2,
        [0x1320] = AppleDeviceModel.AirPods3,
        [0x1920] = AppleDeviceModel.AirPods4,
        [0x1B20] = AppleDeviceModel.AirPods4Anc,
        [0x0E20] = AppleDeviceModel.AirPodsPro,
        [0x1420] = AppleDeviceModel.AirPodsPro2Lightning,
        [0x2420] = AppleDeviceModel.AirPodsPro2UsbC,
        [0x2720] = AppleDeviceModel.AirPodsPro3,
        [0x0A20] = AppleDeviceModel.AirPodsMax,
        [0x1F20] = AppleDeviceModel.AirPodsMaxUsbC,

        [0x0B20] = AppleDeviceModel.PowerbeatsPro,
        [0x0320] = AppleDeviceModel.Powerbeats3,
        [0x0520] = AppleDeviceModel.BeatsX,
        [0x0620] = AppleDeviceModel.BeatsSolo3,
        [0x0C20] = AppleDeviceModel.BeatsSoloPro,
        [0x0920] = AppleDeviceModel.BeatsStudio3,
        [0x1020] = AppleDeviceModel.BeatsFlex,
        [0x1120] = AppleDeviceModel.BeatsStudioBuds,
        [0x1620] = AppleDeviceModel.BeatsStudioBudsPlus,
        [0x1220] = AppleDeviceModel.BeatsFitPro,
    };

    /// <summary>Maps a raw model ID to a model; returns <see cref="AppleDeviceModel.Unknown"/> when not recognised.</summary>
    public static AppleDeviceModel FromId(ushort id) =>
        ById.TryGetValue(id, out var model) ? model : AppleDeviceModel.Unknown;

    public static DeviceKind GetKind(AppleDeviceModel model) => model switch
    {
        AppleDeviceModel.Unknown => DeviceKind.Unknown,
        AppleDeviceModel.AirPodsMax or AppleDeviceModel.AirPodsMaxUsbC
            or AppleDeviceModel.BeatsSolo3 or AppleDeviceModel.BeatsSoloPro
            or AppleDeviceModel.BeatsStudio3 => DeviceKind.Headphones,
        _ => DeviceKind.Earbuds,
    };

    public static string GetDisplayName(AppleDeviceModel model) => model switch
    {
        AppleDeviceModel.AirPods1 => "AirPods (1st generation)",
        AppleDeviceModel.AirPods2 => "AirPods (2nd generation)",
        AppleDeviceModel.AirPods3 => "AirPods (3rd generation)",
        AppleDeviceModel.AirPods4 => "AirPods 4",
        AppleDeviceModel.AirPods4Anc => "AirPods 4 (Active Noise Cancellation)",
        AppleDeviceModel.AirPodsPro => "AirPods Pro",
        AppleDeviceModel.AirPodsPro2Lightning => "AirPods Pro (2nd generation)",
        AppleDeviceModel.AirPodsPro2UsbC => "AirPods Pro (2nd generation, USB-C)",
        AppleDeviceModel.AirPodsPro3 => "AirPods Pro 3",
        AppleDeviceModel.AirPodsMax => "AirPods Max",
        AppleDeviceModel.AirPodsMaxUsbC => "AirPods Max (USB-C)",
        AppleDeviceModel.PowerbeatsPro => "Powerbeats Pro",
        AppleDeviceModel.Powerbeats3 => "Powerbeats3",
        AppleDeviceModel.BeatsX => "BeatsX",
        AppleDeviceModel.BeatsSolo3 => "Beats Solo3",
        AppleDeviceModel.BeatsSoloPro => "Beats Solo Pro",
        AppleDeviceModel.BeatsStudio3 => "Beats Studio3",
        AppleDeviceModel.BeatsFlex => "Beats Flex",
        AppleDeviceModel.BeatsStudioBuds => "Beats Studio Buds",
        AppleDeviceModel.BeatsStudioBudsPlus => "Beats Studio Buds+",
        AppleDeviceModel.BeatsFitPro => "Beats Fit Pro",
        _ => "Apple / Beats device",
    };
}
