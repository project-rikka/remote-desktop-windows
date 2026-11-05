using System.Buffers;

namespace Rikka.Remote.Link;

public sealed class Cauchy
{
    public const int MostShards = 255;

    private readonly byte[] rows;

    private Cauchy(int data, int parity)
    {
        Data = data;
        Parity = parity;
        rows = new byte[parity * data];
        for (var row = 0; row < parity; row++)
        {
            for (var column = 0; column < data; column++)
            {
                rows[(row * data) + column] = GaloisField.Inverse((byte)((data + row) ^ column));
            }
        }
    }

    public int Data { get; }

    public int Parity { get; }

    public static Cauchy Create(int data, int parity)
    {
        if (data <= 0)
        {
            throw new FecException(FecError.NoData, data, 1);
        }

        return parity < 0 || data + parity > MostShards ? throw new FecException(FecError.TooMany, data + parity, MostShards) : new Cauchy(data, parity);
    }

    public void Encode(ReadOnlySpan<byte> data, Span<byte> parity, int shardBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(data.Length, Data * shardBytes);
        ArgumentOutOfRangeException.ThrowIfLessThan(parity.Length, Parity * shardBytes);
        for (var row = 0; row < Parity; row++)
        {
            var output = parity.Slice(row * shardBytes, shardBytes);
            output.Clear();
            for (var column = 0; column < Data; column++)
            {
                GaloisKernel.MultiplyAdd(rows[(row * Data) + column], data.Slice(column * shardBytes, shardBytes), output);
            }
        }
    }

    public void Reconstruct(Span<byte> shards, ReadOnlySpan<bool> present, int shardBytes)
    {
        var total = Data + Parity;
        ArgumentOutOfRangeException.ThrowIfLessThan(shards.Length, total * shardBytes);
        ArgumentOutOfRangeException.ThrowIfLessThan(present.Length, total);
        Span<int> chosen = stackalloc int[Data];
        var count = 0;
        for (var index = 0; index < total && count < Data; index++)
        {
            if (present[index])
            {
                chosen[count++] = index;
            }
        }

        if (count < Data)
        {
            throw new FecException(FecError.Short, count, Data);
        }

        if (!present[..Data].Contains(false))
        {
            return;
        }

        var size = Data * Data;
        var matrix = ArrayPool<byte>.Shared.Rent(size);
        var inverse = ArrayPool<byte>.Shared.Rent(size);
        try
        {
            FillDecodingMatrix(matrix.AsSpan(0, size), chosen);
            Invert(matrix.AsSpan(0, size), inverse.AsSpan(0, size));
            for (var row = 0; row < Data; row++)
            {
                if (present[row])
                {
                    continue;
                }

                var output = shards.Slice(row * shardBytes, shardBytes);
                output.Clear();
                for (var column = 0; column < Data; column++)
                {
                    GaloisKernel.MultiplyAdd(inverse[(row * Data) + column], shards.Slice(chosen[column] * shardBytes, shardBytes), output);
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(matrix);
            ArrayPool<byte>.Shared.Return(inverse);
        }
    }

    private void FillDecodingMatrix(Span<byte> matrix, ReadOnlySpan<int> chosen)
    {
        matrix.Clear();
        for (var row = 0; row < Data; row++)
        {
            var target = matrix.Slice(row * Data, Data);
            var index = chosen[row];
            if (index < Data)
            {
                target[index] = 1;
            }
            else
            {
                rows.AsSpan((index - Data) * Data, Data).CopyTo(target);
            }
        }
    }

    private void Invert(Span<byte> work, Span<byte> inverse)
    {
        var size = Data;
        inverse.Clear();
        for (var index = 0; index < size; index++)
        {
            inverse[(index * size) + index] = 1;
        }

        for (var column = 0; column < size; column++)
        {
            var pivot = column;
            while (work[(pivot * size) + column] == 0)
            {
                pivot++;
            }

            if (pivot != column)
            {
                Swap(work, pivot, column, size);
                Swap(inverse, pivot, column, size);
            }

            var scale = GaloisField.Inverse(work[(column * size) + column]);
            for (var item = 0; item < size; item++)
            {
                work[(column * size) + item] = GaloisField.Multiply(work[(column * size) + item], scale);
                inverse[(column * size) + item] = GaloisField.Multiply(inverse[(column * size) + item], scale);
            }

            for (var row = 0; row < size; row++)
            {
                var factor = work[(row * size) + column];
                if (row == column || factor == 0)
                {
                    continue;
                }

                GaloisKernel.MultiplyAdd(factor, work.Slice(column * size, size), work.Slice(row * size, size));
                GaloisKernel.MultiplyAdd(factor, inverse.Slice(column * size, size), inverse.Slice(row * size, size));
            }
        }
    }

    private static void Swap(Span<byte> matrix, int first, int second, int size)
    {
        var a = matrix.Slice(first * size, size);
        var b = matrix.Slice(second * size, size);
        for (var item = 0; item < size; item++)
        {
            (a[item], b[item]) = (b[item], a[item]);
        }
    }
}
