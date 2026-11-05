using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace Rikka.Remote.Link;

internal static class GaloisKernel
{
    private const byte Nibble = 0x0F;

    public static void MultiplyAdd(byte coefficient, ReadOnlySpan<byte> source, Span<byte> destination)
    {
        var length = Math.Min(source.Length, destination.Length);
        switch (coefficient)
        {
            case 0:
                return;
            case 1:
                Xor(source[..length], destination[..length]);
                return;
            default:
                Multiply(coefficient, source[..length], destination[..length]);
                return;
        }
    }

    private static void Xor(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        ref var from = ref MemoryMarshal.GetReference(source);
        ref var to = ref MemoryMarshal.GetReference(destination);
        var done = 0;
        if (Vector256.IsHardwareAccelerated)
        {
            for (; done + Vector256<byte>.Count <= source.Length; done += Vector256<byte>.Count)
            {
                var value = Vector256.LoadUnsafe(ref from, (nuint)done) ^ Vector256.LoadUnsafe(ref to, (nuint)done);
                value.StoreUnsafe(ref to, (nuint)done);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            for (; done + Vector128<byte>.Count <= source.Length; done += Vector128<byte>.Count)
            {
                var value = Vector128.LoadUnsafe(ref from, (nuint)done) ^ Vector128.LoadUnsafe(ref to, (nuint)done);
                value.StoreUnsafe(ref to, (nuint)done);
            }
        }

        for (; done < source.Length; done++)
        {
            Unsafe.Add(ref to, done) ^= Unsafe.Add(ref from, done);
        }
    }

    private static void Multiply(byte coefficient, ReadOnlySpan<byte> source, Span<byte> destination)
    {
        var table = coefficient * GaloisField.NibbleValues;
        var low = Vector128.Create(GaloisField.LowNibbles.AsSpan(table, GaloisField.NibbleValues));
        var high = Vector128.Create(GaloisField.HighNibbles.AsSpan(table, GaloisField.NibbleValues));
        ref var from = ref MemoryMarshal.GetReference(source);
        ref var to = ref MemoryMarshal.GetReference(destination);
        var done = 0;
        if (Avx2.IsSupported)
        {
            var low256 = Vector256.Create(low, low);
            var high256 = Vector256.Create(high, high);
            var mask = Vector256.Create(Nibble);
            for (; done + Vector256<byte>.Count <= source.Length; done += Vector256<byte>.Count)
            {
                var value = Vector256.LoadUnsafe(ref from, (nuint)done);
                var product = Avx2.Shuffle(low256, value & mask) ^ Avx2.Shuffle(high256, Avx2.ShiftRightLogical(value.AsUInt16(), 4).AsByte() & mask);
                (Vector256.LoadUnsafe(ref to, (nuint)done) ^ product).StoreUnsafe(ref to, (nuint)done);
            }
        }

        if (Ssse3.IsSupported)
        {
            var mask = Vector128.Create(Nibble);
            for (; done + Vector128<byte>.Count <= source.Length; done += Vector128<byte>.Count)
            {
                var value = Vector128.LoadUnsafe(ref from, (nuint)done);
                var product = Ssse3.Shuffle(low, value & mask) ^ Ssse3.Shuffle(high, Sse2.ShiftRightLogical(value.AsUInt16(), 4).AsByte() & mask);
                (Vector128.LoadUnsafe(ref to, (nuint)done) ^ product).StoreUnsafe(ref to, (nuint)done);
            }
        }
        else if (AdvSimd.Arm64.IsSupported)
        {
            var mask = Vector128.Create(Nibble);
            for (; done + Vector128<byte>.Count <= source.Length; done += Vector128<byte>.Count)
            {
                var value = Vector128.LoadUnsafe(ref from, (nuint)done);
                var product = AdvSimd.Arm64.VectorTableLookup(low, value & mask) ^ AdvSimd.Arm64.VectorTableLookup(high, AdvSimd.ShiftRightLogical(value, 4));
                (Vector128.LoadUnsafe(ref to, (nuint)done) ^ product).StoreUnsafe(ref to, (nuint)done);
            }
        }

        for (; done < source.Length; done++)
        {
            Unsafe.Add(ref to, done) ^= GaloisField.Multiply(coefficient, Unsafe.Add(ref from, done));
        }
    }
}
