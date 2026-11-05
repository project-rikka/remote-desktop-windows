namespace Rikka.Remote.Link.Tests;

public sealed class CauchyTests
{
    [Theory]
    [InlineData(1, 1, 7)]
    [InlineData(3, 2, 1200)]
    [InlineData(10, 4, 33)]
    [InlineData(100, 30, 64)]
    [InlineData(200, 55, 17)]
    public void AnyKShardsRestoreTheData(int data, int parity, int length)
    {
        var originals = Shards(data, length, (byte)data);
        var coder = Cauchy.Create(data, parity);
        var encoded = new byte[(data + parity) * length];
        originals.CopyTo(encoded, 0);
        coder.Encode(originals, encoded.AsSpan(data * length), length);
        var random = new Random(data * 31 + parity);

        foreach (var lostFrom in new[] { 0, Math.Max(data - parity, 0) })
        {
            AssertRestored(coder, encoded, originals, Enumerable.Range(lostFrom, parity), length);
        }

        for (var trial = 0; trial < 20; trial++)
        {
            var lostShards = Enumerable.Range(0, data + parity).OrderBy(_ => random.Next()).Take(parity);
            AssertRestored(coder, encoded, originals, lostShards, length);
        }
    }

    [Fact]
    public void ParityMatchesTheDocumentedCauchyMatrix()
    {
        byte[] data = [0x01, 0x02, 0x80];
        var parity = new byte[1];

        Cauchy.Create(3, 1).Encode(data, parity, 1);

        var expected = (byte)0;
        for (var column = 0; column < 3; column++)
        {
            expected ^= Multiply(Inverse((byte)(3 ^ column)), data[column]);
        }

        Assert.Equal(expected, parity[0]);
    }

    [Fact]
    public void TooFewShardsAreRefused()
    {
        var coder = Cauchy.Create(4, 2);
        var shards = new byte[6 * 8];

        var error = Assert.Throws<FecException>(() => coder.Reconstruct(shards, [true, false, false, false, true, true], 8));

        Assert.Equal((FecError.Short, 3, 4), (error.Error, error.Have, error.Need));
        Assert.Equal(FecError.TooMany, Assert.Throws<FecException>(() => Cauchy.Create(200, 56)).Error);
        Assert.Equal(FecError.NoData, Assert.Throws<FecException>(() => Cauchy.Create(0, 1)).Error);
    }

    private static void AssertRestored(Cauchy coder, byte[] encoded, byte[] originals, IEnumerable<int> lostShards, int length)
    {
        var received = (byte[])encoded.Clone();
        var present = Enumerable.Repeat(true, coder.Data + coder.Parity).ToArray();
        foreach (var lost in lostShards)
        {
            present[lost] = false;
            received.AsSpan(lost * length, length).Fill(0xEE);
        }

        coder.Reconstruct(received, present, length);

        Assert.Equal(originals, received.AsSpan(0, coder.Data * length).ToArray());
    }

    private static byte[] Shards(int data, int length, byte seed)
    {
        var bytes = new byte[data * length];
        for (var shard = 0; shard < data; shard++)
        {
            for (var index = 0; index < length; index++)
            {
                bytes[(shard * length) + index] = (byte)((byte)(index * 31) + (byte)(shard ^ seed));
            }
        }

        return bytes;
    }

    private static byte Multiply(byte a, byte b)
    {
        var product = 0;
        int left = a;
        for (int right = b; right != 0; right >>= 1)
        {
            if ((right & 1) != 0)
            {
                product ^= left;
            }

            left <<= 1;
            if ((left & 0x100) != 0)
            {
                left ^= 0x11D;
            }
        }

        return (byte)product;
    }

    private static byte Inverse(byte value)
    {
        for (var candidate = 1; candidate <= 255; candidate++)
        {
            if (Multiply(value, (byte)candidate) == 1)
            {
                return (byte)candidate;
            }
        }

        throw new InvalidOperationException("zero has no inverse");
    }
}
