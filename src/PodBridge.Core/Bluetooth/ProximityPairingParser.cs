namespace PodBridge.Core.Bluetooth;

/// <summary>
/// Decodes the Apple "proximity pairing" BLE advertisement (manufacturer data, company ID 0x004C,
/// message type 0x07) that AirPods and Beats broadcast.
///
/// Layout of the manufacturer payload (the bytes AFTER the 2-byte company ID):
/// <code>
///  [0] 0x07            message type (proximity pairing)
///  [1] length          number of bytes that follow (0x19 for AirPods)
///  [2] prefix          0x01 = paired, 0x00 = not paired
///  [3] model high byte \  big-endian model ID, e.g. 0x0E20
///  [4] model low byte  /
///  [5] status          bit 0x20 set = left bud is the broadcaster (not flipped);
///                      bit 0x02 = LEFT bud in ear, bit 0x08 = RIGHT bud in ear (NOT swapped when flipped);
///                      bit 0x04 = both buds in the case
///  [6] pod batteries   two nibbles, 0..10 = 0..100 %, 0xF = unknown
///  [7] charge + case   high nibble = charging bits, low nibble = case battery
///  [8] lid             bit 0x08 clear = lid open; low 3 bits are an open/close counter
///  [9] colour
/// </code>
/// The layout started from public community reverse-engineering notes (see the README for caveats).
/// The in-ear finding (0x02 = left, 0x08 = right, independent of the flipped bit) and the 0x04
/// "both in case" bit come from captured real-device adverts (AirPods Pro 2 USB-C, model 0x2420).
/// Battery and charge nibbles DO swap with the flipped bit; the in-ear bits do not.
/// </summary>
public static class ProximityPairingParser
{
    public const ushort AppleCompanyId = 0x004C;
    public const byte ProximityPairingType = 0x07;

    private const int MinimumLength = 9;       // bytes [0]..[8] are needed
    private const int MinimumDeclaredLength = 7;

    // Status byte [5]
    private const byte NotFlippedBit = 0x20;
    // In-ear bits are absolute (captured real data): 0x02 = left, 0x08 = right, flipped or not.
    private const byte InEarLeftBit = 0x02;
    private const byte InEarRightBit = 0x08;
    // Set only while both buds are in the case.
    private const byte BothInCaseBit = 0x04;

    // Charge nibble (high nibble of byte [7])
    private const int ChargeLowBit = 0x1;
    private const int ChargeHighBit = 0x2;
    private const int ChargeCaseBit = 0x4;

    // Lid byte [8]
    private const byte LidClosedBit = 0x08;

    /// <summary>Decodes a battery nibble: 0..10 become 0..100, anything else (such as 0xF) is unknown.</summary>
    public static int? DecodeBatteryNibble(int nibble) =>
        nibble is >= 0 and <= 10 ? nibble * 10 : null;

    /// <summary>
    /// Parses manufacturer data when the buffer starts with the little-endian company ID bytes (4C 00).
    /// </summary>
    public static bool TryParseWithCompanyId(ReadOnlySpan<byte> data, out ProximityAdvert advert)
    {
        advert = null!;
        if (data.Length < 2 || data[0] != 0x4C || data[1] != 0x00)
        {
            return false;
        }
        return TryParse(data[2..], out advert);
    }

    /// <summary>
    /// Parses manufacturer data that has already had the company ID removed
    /// (this is what Windows hands you for a given company ID).
    /// </summary>
    public static bool TryParse(ReadOnlySpan<byte> data, out ProximityAdvert advert)
    {
        advert = null!;

        if (data.Length < MinimumLength ||
            data[0] != ProximityPairingType ||
            data[1] < MinimumDeclaredLength)
        {
            return false;
        }

        ushort modelId = (ushort)((data[3] << 8) | data[4]);
        byte status = data[5];
        byte podBatteries = data[6];
        byte chargeAndCase = data[7];
        byte lid = data[8];

        bool flipped = (status & NotFlippedBit) == 0;

        int highNibble = podBatteries >> 4;
        int lowNibble = podBatteries & 0x0F;

        // Not flipped: left = low nibble, right = high nibble. Flipped: the other way round.
        int leftNibble = flipped ? highNibble : lowNibble;
        int rightNibble = flipped ? lowNibble : highNibble;

        int chargeNibble = chargeAndCase >> 4;
        int caseNibble = chargeAndCase & 0x0F;

        bool lowCharging = (chargeNibble & ChargeLowBit) != 0;
        bool highCharging = (chargeNibble & ChargeHighBit) != 0;
        bool leftCharging = flipped ? highCharging : lowCharging;
        bool rightCharging = flipped ? lowCharging : highCharging;

        int inEar = status & 0x0F;
        // A bud that is charging is sitting in the case, whatever the in-ear bit says
        // (the bit has been seen set for buds in the case).
        bool bothInCase = (status & BothInCaseBit) != 0;
        bool leftInEar = !bothInCase && !leftCharging && (inEar & InEarLeftBit) != 0;
        bool rightInEar = !bothInCase && !rightCharging && (inEar & InEarRightBit) != 0;

        advert = new ProximityAdvert
        {
            Raw = data.ToArray(),
            RawModelId = modelId,
            Model = AppleDeviceCatalog.FromId(modelId),
            Flipped = flipped,
            LeftBattery = DecodeBatteryNibble(leftNibble),
            RightBattery = DecodeBatteryNibble(rightNibble),
            CaseBattery = DecodeBatteryNibble(caseNibble),
            LeftCharging = leftCharging,
            RightCharging = rightCharging,
            CaseCharging = (chargeNibble & ChargeCaseBit) != 0,
            LeftInEar = leftInEar,
            RightInEar = rightInEar,
            BothInCase = bothInCase,
            LidOpen = (lid & LidClosedBit) == 0,
        };
        return true;
    }
}
