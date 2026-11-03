using PodBridge.Core.Bluetooth;

namespace PodBridge.Core.Tests;

/// <summary>Builds hand-made proximity-pairing payloads (company ID already stripped).</summary>
internal static class AdvertBuilder
{
    // Status byte: 0x20 bit set means "not flipped".
    public const byte NotFlipped = 0x20;
    public const byte Flipped = 0x00;

    // Lid byte: 0x08 bit set means closed.
    public const byte LidClosed = 0x08;
    public const byte LidOpen = 0x00;

    public static byte[] Build(
        ushort model = 0x1420,
        byte statusHigh = NotFlipped,
        byte inEarBits = 0,
        byte podBytes = 0x78,
        byte chargeNibble = 0,
        byte caseNibble = 0x5,
        byte lid = LidClosed)
    {
        var data = new byte[27];
        data[0] = 0x07;
        data[1] = 0x19;
        data[2] = 0x01;
        data[3] = (byte)(model >> 8);
        data[4] = (byte)(model & 0xFF);
        data[5] = (byte)(statusHigh | (inEarBits & 0x0F));
        data[6] = podBytes;
        data[7] = (byte)((chargeNibble << 4) | (caseNibble & 0x0F));
        data[8] = lid;
        data[9] = 0x00; // colour
        return data;
    }

    public static ProximityAdvert Parse(byte[] data)
    {
        Assert.True(ProximityPairingParser.TryParse(data, out var advert));
        return advert;
    }

    public static ProximityAdvert Make(
        ushort model = 0x1420,
        int? left = 80, int? right = 70, int? caseLevel = 50,
        bool lidOpen = false, bool leftCharging = false, bool rightCharging = false,
        bool caseCharging = false)
    {
        return new ProximityAdvert
        {
            RawModelId = model,
            Model = AppleDeviceCatalog.FromId(model),
            LeftBattery = left,
            RightBattery = right,
            CaseBattery = caseLevel,
            LidOpen = lidOpen,
            LeftCharging = leftCharging,
            RightCharging = rightCharging,
            CaseCharging = caseCharging,
        };
    }
}
