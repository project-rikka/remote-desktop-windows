using System.Text;

namespace Rikka.Remote.Wire;

internal static class WireText
{
    public static ReadOnlySpan<char> Fitting(string value)
    {
        if (Encoding.UTF8.GetMaxByteCount(value.Length) <= ushort.MaxValue || Encoding.UTF8.GetByteCount(value) <= ushort.MaxValue)
        {
            return value;
        }

        var end = 0;
        var bytes = 0;
        while (end < value.Length)
        {
            var width = char.IsHighSurrogate(value[end]) && end + 1 < value.Length && char.IsLowSurrogate(value[end + 1]) ? 2 : 1;
            var size = Encoding.UTF8.GetByteCount(value.AsSpan(end, width));
            if (bytes + size > ushort.MaxValue)
            {
                break;
            }

            bytes += size;
            end += width;
        }

        return value.AsSpan(0, end);
    }

    public static int StrLength(string value) => sizeof(ushort) + Encoding.UTF8.GetByteCount(Fitting(value));

    public static int BytesLength(ReadOnlySpan<byte> value) => sizeof(uint) + value.Length;
}
