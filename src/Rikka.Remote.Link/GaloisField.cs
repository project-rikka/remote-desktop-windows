namespace Rikka.Remote.Link;

internal static class GaloisField
{
    public const int Polynomial = 0x11D;
    public const int Order = 256;
    public const int NibbleValues = 16;

    private static readonly byte[] Exponents = new byte[2 * Order];
    private static readonly byte[] Logarithms = new byte[Order];

    static GaloisField()
    {
        var value = 1;
        for (var power = 0; power < Order - 1; power++)
        {
            Exponents[power] = (byte)value;
            Logarithms[value] = (byte)power;
            value <<= 1;
            if ((value & Order) != 0)
            {
                value ^= Polynomial;
            }
        }

        for (var index = Order - 1; index < Exponents.Length; index++)
        {
            Exponents[index] = Exponents[index - (Order - 1)];
        }

        for (var coefficient = 0; coefficient < Order; coefficient++)
        {
            for (var nibble = 0; nibble < NibbleValues; nibble++)
            {
                LowNibbles[(coefficient * NibbleValues) + nibble] = Multiply((byte)coefficient, (byte)nibble);
                HighNibbles[(coefficient * NibbleValues) + nibble] = Multiply((byte)coefficient, (byte)(nibble << 4));
            }
        }
    }

    public static byte[] LowNibbles { get; } = new byte[Order * NibbleValues];

    public static byte[] HighNibbles { get; } = new byte[Order * NibbleValues];

    public static byte Multiply(byte left, byte right) =>
        left == 0 || right == 0 ? (byte)0 : Exponents[Logarithms[left] + Logarithms[right]];

    public static byte Inverse(byte value) => Exponents[Order - 1 - Logarithms[value]];
}
