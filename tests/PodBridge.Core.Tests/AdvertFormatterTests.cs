using PodBridge.Core.Bluetooth;
using static PodBridge.Core.Tests.AdvertBuilder;

namespace PodBridge.Core.Tests;

public class AdvertFormatterTests
{
    [Fact]
    public void Hex_is_space_separated_upper_case()
    {
        Assert.Equal("07 19 0A FF", AdvertFormatter.ToHex(new byte[] { 0x07, 0x19, 0x0A, 0xFF }));
        Assert.Equal("", AdvertFormatter.ToHex(ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void Status_line_lists_set_bits_and_flipped_state()
    {
        var data = Build(statusHigh: NotFlipped, inEarBits: 0x0A, chargeNibble: 0x3);
        Assert.Equal(
            "status=0x2A set[0x02 0x08 0x20] flipped=no | charge=0x3 set[0x01 0x02]",
            AdvertFormatter.DescribeStatus(data));
    }

    [Fact]
    public void Flipped_and_no_bits_set()
    {
        var data = Build(statusHigh: Flipped, inEarBits: 0, chargeNibble: 0);
        Assert.Equal(
            "status=0x00 set[none] flipped=yes | charge=0x0 set[none]",
            AdvertFormatter.DescribeStatus(data));
    }

    [Fact]
    public void Short_payloads_do_not_throw()
    {
        Assert.Equal("status=(payload too short)", AdvertFormatter.DescribeStatus(new byte[] { 0x07, 0x19 }));
        Assert.Equal("status=0x20 set[0x20] flipped=no",
            AdvertFormatter.DescribeStatus(new byte[] { 0, 0, 0, 0, 0, 0x20 }));
    }

    [Fact]
    public void Format_joins_hex_and_status()
    {
        var text = AdvertFormatter.Format(Build());
        Assert.StartsWith("07 19 01 14 20 20 78 05 08 00", text);
        Assert.Contains(" | status=0x20 ", text);
    }

    [Fact]
    public void SameHeader_ignores_the_changing_tail()
    {
        var a = Build();
        var b = Build();
        b[20] = 0x99;
        Assert.True(AdvertFormatter.SameHeader(a, b));

        var c = Build(inEarBits: 0x02);
        Assert.False(AdvertFormatter.SameHeader(a, c));
    }

    [Fact]
    public void Parser_keeps_the_raw_payload()
    {
        var data = Build(inEarBits: 0x02);
        var advert = Parse(data);
        Assert.Equal(data, advert.Raw);
        Assert.NotSame(data, advert.Raw);
    }
}
