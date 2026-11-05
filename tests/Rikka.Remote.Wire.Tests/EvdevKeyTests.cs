namespace Rikka.Remote.Wire.Tests;

public sealed class EvdevKeyTests
{
    public static TheoryData<byte, ushort> Extended() => new()
    {
        { 0x1C, 96 },
        { 0x1D, 97 },
        { 0x35, 98 },
        { 0x37, 99 },
        { 0x38, 100 },
        { 0x47, 102 },
        { 0x48, 103 },
        { 0x49, 104 },
        { 0x4B, 105 },
        { 0x4D, 106 },
        { 0x4F, 107 },
        { 0x50, 108 },
        { 0x51, 109 },
        { 0x52, 110 },
        { 0x53, 111 },
        { 0x5B, 125 },
        { 0x5C, 126 },
        { 0x5D, 127 },
    };

    public static TheoryData<byte, ushort> Special() => new()
    {
        { 0x70, 93 },
        { 0x73, 89 },
        { 0x79, 92 },
        { 0x7B, 94 },
        { 0x7D, 124 },
        { 0x64, 183 },
        { 0x65, 184 },
        { 0x66, 185 },
        { 0x67, 186 },
        { 0x68, 187 },
        { 0x69, 188 },
        { 0x6A, 189 },
        { 0x6B, 190 },
        { 0x6C, 191 },
        { 0x6D, 192 },
        { 0x6E, 193 },
        { 0x76, 194 },
        { 0x72, 123 },
        { 0xF1, 123 },
        { 0x71, 122 },
        { 0xF2, 122 },
    };

    [Fact]
    public void PlainScanCodesMapToThemselves()
    {
        for (var scan = 0x01; scan <= 0x58; scan++)
        {
            Assert.Equal((ushort)scan, EvdevKey.FromScanCode(ScanPrefix.None, (byte)scan));
        }
    }

    [Theory]
    [MemberData(nameof(Extended))]
    public void ExtendedScanCodesFollowTheTable(byte scan, ushort code)
    {
        Assert.Equal(code, EvdevKey.FromScanCode(ScanPrefix.E0, scan));
    }

    [Theory]
    [MemberData(nameof(Special))]
    public void NamedPlainScanCodesFollowTheTable(byte scan, ushort code)
    {
        Assert.Equal(code, EvdevKey.FromScanCode(ScanPrefix.None, scan));
    }

    [Fact]
    public void PauseComesFromItsE1Sequence()
    {
        Assert.Equal((ushort)119, EvdevKey.FromScanCode(ScanPrefix.E1, 0x1D));
    }

    [Theory]
    [InlineData(ScanPrefix.None, 0x00)]
    [InlineData(ScanPrefix.None, 0x59)]
    [InlineData(ScanPrefix.None, 0x7F)]
    [InlineData(ScanPrefix.E0, 0x2A)]
    [InlineData(ScanPrefix.E0, 0x45)]
    [InlineData(ScanPrefix.E1, 0x45)]
    public void UnlistedScanCodesHaveNoKey(ScanPrefix prefix, byte scan)
    {
        Assert.Null(EvdevKey.FromScanCode(prefix, scan));
    }
}
