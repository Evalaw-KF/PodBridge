using PodBridge.Core.Bluetooth;
using static PodBridge.Core.Tests.AdvertBuilder;

namespace PodBridge.Core.Tests;

public class ParserTests
{
    [Fact]
    public void Decodes_a_typical_AirPods_Pro_2_advert()
    {
        // Not flipped: low nibble = left, high nibble = right. podBytes 0x78 => right 70, left 80.
        var advert = Parse(Build(model: 0x1420, podBytes: 0x78, caseNibble: 0x5));

        Assert.Equal(AppleDeviceModel.AirPodsPro2Lightning, advert.Model);
        Assert.Equal(0x1420, advert.RawModelId);
        Assert.Equal(80, advert.LeftBattery);
        Assert.Equal(70, advert.RightBattery);
        Assert.Equal(50, advert.CaseBattery);
        Assert.False(advert.Flipped);
    }

    [Fact]
    public void Flipped_swaps_left_and_right()
    {
        var advert = Parse(Build(statusHigh: Flipped, podBytes: 0x78));

        Assert.True(advert.Flipped);
        Assert.Equal(70, advert.LeftBattery);
        Assert.Equal(80, advert.RightBattery);
    }

    [Theory]
    [InlineData(0x0, 0)]
    [InlineData(0x1, 10)]
    [InlineData(0x5, 50)]
    [InlineData(0x9, 90)]
    [InlineData(0xA, 100)]
    public void Battery_nibbles_map_to_tens(int nibble, int expected)
    {
        Assert.Equal(expected, ProximityPairingParser.DecodeBatteryNibble(nibble));
    }

    [Theory]
    [InlineData(0xB)]
    [InlineData(0xC)]
    [InlineData(0xE)]
    [InlineData(0xF)]
    public void Battery_nibbles_above_ten_are_unknown(int nibble)
    {
        Assert.Null(ProximityPairingParser.DecodeBatteryNibble(nibble));
    }

    [Fact]
    public void Nibble_0xF_means_unknown_for_each_slot()
    {
        // low nibble (left) = F, high nibble (right) = 6
        var advert = Parse(Build(podBytes: 0x6F, caseNibble: 0xF));

        Assert.Null(advert.LeftBattery);
        Assert.Equal(60, advert.RightBattery);
        Assert.Null(advert.CaseBattery);
    }

    [Fact]
    public void Full_battery_is_100_percent()
    {
        var advert = Parse(Build(podBytes: 0xAA, caseNibble: 0xA));
        Assert.Equal(100, advert.LeftBattery);
        Assert.Equal(100, advert.RightBattery);
        Assert.Equal(100, advert.CaseBattery);
    }

    [Theory]
    [InlineData(0x0, false, false, false)]
    [InlineData(0x1, true, false, false)]
    [InlineData(0x2, false, true, false)]
    [InlineData(0x4, false, false, true)]
    [InlineData(0x7, true, true, true)]
    public void Charging_bits_not_flipped(int bits, bool left, bool right, bool caseCharging)
    {
        var advert = Parse(Build(chargeNibble: (byte)bits));
        Assert.Equal(left, advert.LeftCharging);
        Assert.Equal(right, advert.RightCharging);
        Assert.Equal(caseCharging, advert.CaseCharging);
    }

    [Theory]
    [InlineData(0x1, false, true, false)]
    [InlineData(0x2, true, false, false)]
    [InlineData(0x4, false, false, true)]
    public void Charging_bits_swap_for_buds_when_flipped_but_not_for_the_case(int bits, bool left, bool right, bool caseCharging)
    {
        var advert = Parse(Build(statusHigh: Flipped, chargeNibble: (byte)bits));
        Assert.Equal(left, advert.LeftCharging);
        Assert.Equal(right, advert.RightCharging);
        Assert.Equal(caseCharging, advert.CaseCharging);
    }

    [Theory]
    [InlineData(0x00, false, false)]
    [InlineData(0x02, true, false)]
    [InlineData(0x08, false, true)]
    [InlineData(0x0A, true, true)]
    public void In_ear_bits_not_flipped(int bits, bool left, bool right)
    {
        var advert = Parse(Build(inEarBits: (byte)bits));
        Assert.Equal(left, advert.LeftInEar);
        Assert.Equal(right, advert.RightInEar);
    }

    [Theory]
    [InlineData(0x02, true, false)]
    [InlineData(0x08, false, true)]
    [InlineData(0x0A, true, true)]
    public void In_ear_bits_do_not_swap_when_flipped(int bits, bool left, bool right)
    {
        var advert = Parse(Build(statusHigh: Flipped, inEarBits: (byte)bits));
        Assert.Equal(left, advert.LeftInEar);
        Assert.Equal(right, advert.RightInEar);
    }

    [Fact]
    public void Left_bud_in_ear_with_right_bud_charging_in_case()
    {
        // Real-world shape: left broadcasting (not flipped), left in ear (0x02), right charging (bit 0x2).
        var advert = Parse(Build(inEarBits: 0x02, chargeNibble: 0x2));
        Assert.True(advert.LeftInEar);
        Assert.False(advert.RightInEar);
        Assert.False(advert.LeftCharging);
        Assert.True(advert.RightCharging);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_charging_bud_is_never_reported_in_ear(bool flipped)
    {
        // Both in-ear bits set, both buds charging: they are in the case.
        var advert = Parse(Build(statusHigh: flipped ? Flipped : NotFlipped, inEarBits: 0x0A, chargeNibble: 0x3));
        Assert.False(advert.LeftInEar);
        Assert.False(advert.RightInEar);
    }

    [Fact]
    public void Decodes_a_full_27_byte_payload_byte_for_byte()
    {
        // 07 19 01 | 14 20 | 2B | 89 | 3A | 01 ... (company ID already stripped)
        // status 0x2B: 0x20 set (left broadcasts), 0x02 left in ear, 0x08 right in ear.
        // pods 0x89: low nibble 9 = left 90 %, high nibble 8 = right 80 %.
        // 0x3A: charge nibble 3 = both buds charging bits, case 10 = 100 %.
        // lid 0x01: closed bit clear -> open, counter 1.
        var data = new byte[27];
        new byte[] { 0x07, 0x19, 0x01, 0x14, 0x20, 0x2B, 0x89, 0x3A, 0x01, 0x00, 0x00 }.CopyTo(data, 0);
        var advert = Parse(data);

        Assert.Equal(AppleDeviceModel.AirPodsPro2Lightning, advert.Model);
        Assert.False(advert.Flipped);
        Assert.Equal(90, advert.LeftBattery);
        Assert.Equal(80, advert.RightBattery);
        Assert.Equal(100, advert.CaseBattery);
        Assert.True(advert.LeftCharging);
        Assert.True(advert.RightCharging);
        Assert.False(advert.CaseCharging);
        Assert.False(advert.LeftInEar);   // charging wins over the in-ear bit
        Assert.False(advert.RightInEar);
        Assert.True(advert.LidOpen);
    }

    [Fact]
    public void Lid_open_when_bit_is_clear()
    {
        Assert.True(Parse(Build(lid: LidOpen)).LidOpen);
        Assert.False(Parse(Build(lid: LidClosed)).LidOpen);
    }

    [Fact]
    public void Lid_ignores_other_bits_in_the_lid_byte()
    {
        Assert.True(Parse(Build(lid: 0x05)).LidOpen);   // counter bits only
        Assert.False(Parse(Build(lid: 0x0D)).LidOpen);
    }

    [Theory]
    [InlineData(0x0220, AppleDeviceModel.AirPods1)]
    [InlineData(0x0F20, AppleDeviceModel.AirPods2)]
    [InlineData(0x1320, AppleDeviceModel.AirPods3)]
    [InlineData(0x1920, AppleDeviceModel.AirPods4)]
    [InlineData(0x1B20, AppleDeviceModel.AirPods4Anc)]
    [InlineData(0x0E20, AppleDeviceModel.AirPodsPro)]
    [InlineData(0x1420, AppleDeviceModel.AirPodsPro2Lightning)]
    [InlineData(0x2420, AppleDeviceModel.AirPodsPro2UsbC)]
    [InlineData(0x2720, AppleDeviceModel.AirPodsPro3)]
    [InlineData(0x0A20, AppleDeviceModel.AirPodsMax)]
    [InlineData(0x1F20, AppleDeviceModel.AirPodsMaxUsbC)]
    [InlineData(0x0B20, AppleDeviceModel.PowerbeatsPro)]
    [InlineData(0x0520, AppleDeviceModel.BeatsX)]
    [InlineData(0x0620, AppleDeviceModel.BeatsSolo3)]
    [InlineData(0x0920, AppleDeviceModel.BeatsStudio3)]
    [InlineData(0x1220, AppleDeviceModel.BeatsFitPro)]
    [InlineData(0x1620, AppleDeviceModel.BeatsStudioBudsPlus)]
    public void Known_model_ids(ushort id, AppleDeviceModel expected)
    {
        var advert = Parse(Build(model: id));
        Assert.Equal(expected, advert.Model);
    }

    [Theory]
    [InlineData(0x0000)]
    [InlineData(0xFFFF)]
    [InlineData(0x7777)]
    public void Unrecognised_model_ids_are_Unknown_but_still_parse(ushort id)
    {
        var advert = Parse(Build(model: id, podBytes: 0x99));
        Assert.Equal(AppleDeviceModel.Unknown, advert.Model);
        Assert.Equal(id, advert.RawModelId);
        Assert.Equal(90, advert.LeftBattery);
    }

    [Fact]
    public void Every_known_model_has_a_friendly_name()
    {
        foreach (var model in Enum.GetValues<AppleDeviceModel>().Where(m => m != AppleDeviceModel.Unknown))
        {
            Assert.DoesNotContain("Apple / Beats device", AppleDeviceCatalog.GetDisplayName(model));
        }
    }

    [Fact]
    public void Kinds_are_assigned()
    {
        Assert.Equal(DeviceKind.Earbuds, AppleDeviceCatalog.GetKind(AppleDeviceModel.AirPodsPro2UsbC));
        Assert.Equal(DeviceKind.Headphones, AppleDeviceCatalog.GetKind(AppleDeviceModel.AirPodsMax));
        Assert.Equal(DeviceKind.Unknown, AppleDeviceCatalog.GetKind(AppleDeviceModel.Unknown));
    }

    [Fact]
    public void Accepts_a_short_payload_that_still_covers_the_lid_byte()
    {
        var full = Build();
        var shortened = full[..9];
        Assert.True(ProximityPairingParser.TryParse(shortened, out var advert));
        Assert.Equal(80, advert.LeftBattery);
    }

    [Fact]
    public void Rejects_too_short_payloads()
    {
        Assert.False(ProximityPairingParser.TryParse(Build()[..8], out _));
        Assert.False(ProximityPairingParser.TryParse(ReadOnlySpan<byte>.Empty, out _));
    }

    [Fact]
    public void Rejects_other_apple_message_types()
    {
        var data = Build();
        data[0] = 0x10; // "nearby info", not proximity pairing
        Assert.False(ProximityPairingParser.TryParse(data, out _));
    }

    [Fact]
    public void Rejects_implausibly_small_declared_length()
    {
        var data = Build();
        data[1] = 0x03;
        Assert.False(ProximityPairingParser.TryParse(data, out _));
    }

    [Fact]
    public void WithCompanyId_requires_Apple_prefix_and_strips_it()
    {
        var payload = Build(model: 0x0E20);
        var withId = new byte[] { 0x4C, 0x00 }.Concat(payload).ToArray();

        Assert.True(ProximityPairingParser.TryParseWithCompanyId(withId, out var advert));
        Assert.Equal(AppleDeviceModel.AirPodsPro, advert.Model);

        var wrongCompany = new byte[] { 0x06, 0x00 }.Concat(payload).ToArray();
        Assert.False(ProximityPairingParser.TryParseWithCompanyId(wrongCompany, out _));
        Assert.False(ProximityPairingParser.TryParseWithCompanyId(new byte[] { 0x4C }, out _));
    }

    [Fact]
    public void Garbage_input_never_throws()
    {
        var rng = new Random(1234);
        for (int i = 0; i < 2000; i++)
        {
            var data = new byte[rng.Next(0, 40)];
            rng.NextBytes(data);
            if (data.Length > 0 && i % 2 == 0)
            {
                data[0] = 0x07;
            }
            ProximityPairingParser.TryParse(data, out _);
            ProximityPairingParser.TryParseWithCompanyId(data, out _);
        }
    }

    private static byte[] Real(string hex)
    {
        var data = new byte[27];
        var bytes = Convert.FromHexString(hex.Replace(" ", ""));
        bytes.CopyTo(data, 0);
        return data;
    }

    // Real adverts captured from AirPods Pro 2 USB-C (0x2420), padded to 27 bytes.
    // Args: payload, leftCharging, rightCharging, leftInEar, rightInEar, bothInCase, caseBattery
    [Theory]
    [InlineData("07 19 01 24 20 55 AA B1 31 00 04", true, true, false, false, true, 10)]   // A1
    [InlineData("07 19 01 24 20 35 AA B1 11 00 04", true, true, false, false, true, 10)]   // A2
    [InlineData("07 19 01 24 20 51 AA 91 31 00 04", false, true, false, false, false, 10)] // B1
    [InlineData("07 19 01 24 20 53 AA 91 31 00 04", false, true, true, false, false, 10)]  // B2
    [InlineData("07 19 01 24 20 33 AA A1 11 00 04", false, true, true, false, false, 10)]  // B3
    [InlineData("07 19 01 24 20 23 AA 8F 11 00 04", false, false, true, false, false, null)] // C1
    [InlineData("07 19 01 24 20 2B AA 8F 11 00 04", false, false, true, true, false, null)]  // C2
    public void Real_device_adverts(string hex, bool lc, bool rc, bool lear, bool rear, bool both, int? caseBattery)
    {
        var advert = Parse(Real(hex));
        Assert.Equal(100, advert.LeftBattery);
        Assert.Equal(100, advert.RightBattery);
        Assert.Equal(lc, advert.LeftCharging);
        Assert.Equal(rc, advert.RightCharging);
        Assert.Equal(lear, advert.LeftInEar);
        Assert.Equal(rear, advert.RightInEar);
        Assert.Equal(both, advert.BothInCase);
        Assert.True(advert.LidOpen);
        Assert.Equal(caseBattery, advert.CaseBattery);
    }
}
