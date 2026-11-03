using System.Text;

namespace PodBridge.Core.Bluetooth;

/// <summary>
/// Formats a raw proximity-pairing payload for humans, so the bit mapping in
/// <see cref="ProximityPairingParser"/> can be checked against what a real device sends.
/// </summary>
public static class AdvertFormatter
{
    /// <summary>
    /// Bytes [0]..[9] are the decoded header. Everything after that looks like an encrypted
    /// blob that changes on every advert, so it is ignored when deciding if two adverts differ.
    /// </summary>
    public const int HeaderLength = 10;

    private const int StatusIndex = 5;
    private const int ChargeIndex = 7;

    /// <summary>Space-separated upper-case hex, such as "07 19 01 24 20".</summary>
    public static string ToHex(ReadOnlySpan<byte> data) => Convert.ToHexString(data).InsertSpaces();

    /// <summary>True when both payloads have the same header bytes (the changing tail is ignored).</summary>
    public static bool SameHeader(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
    {
        var headerA = a[..Math.Min(a.Length, HeaderLength)];
        var headerB = b[..Math.Min(b.Length, HeaderLength)];
        return headerA.SequenceEqual(headerB);
    }

    /// <summary>
    /// One line describing the status byte [5] and the charge nibble of byte [7], for example
    /// <c>status=0x2A set[0x02 0x08 0x20] flipped=no | charge=0x2 set[0x2]</c>.
    /// </summary>
    public static string DescribeStatus(ReadOnlySpan<byte> data)
    {
        if (data.Length <= StatusIndex)
        {
            return "status=(payload too short)";
        }

        byte status = data[StatusIndex];
        var sb = new StringBuilder();
        sb.Append($"status=0x{status:X2} set[{SetBits(status, 8)}] flipped={((status & 0x20) == 0 ? "yes" : "no")}");

        if (data.Length > ChargeIndex)
        {
            int charge = data[ChargeIndex] >> 4;
            sb.Append($" | charge=0x{charge:X} set[{SetBits(charge, 4)}]");
        }
        return sb.ToString();
    }

    /// <summary>The hex line followed by the decoded status line, separated by " | ".</summary>
    public static string Format(ReadOnlySpan<byte> data) => $"{ToHex(data)} | {DescribeStatus(data)}";

    // Lists the set bits as 0x01, 0x02 ... (lowest first), or "none".
    private static string SetBits(int value, int width)
    {
        var parts = new List<string>();
        for (int i = 0; i < width; i++)
        {
            int bit = 1 << i;
            if ((value & bit) != 0)
            {
                parts.Add($"0x{bit:X2}");
            }
        }
        return parts.Count == 0 ? "none" : string.Join(' ', parts);
    }

    private static string InsertSpaces(this string hex)
    {
        var sb = new StringBuilder(hex.Length + hex.Length / 2);
        for (int i = 0; i < hex.Length; i += 2)
        {
            if (i > 0)
            {
                sb.Append(' ');
            }
            sb.Append(hex, i, 2);
        }
        return sb.ToString();
    }
}
