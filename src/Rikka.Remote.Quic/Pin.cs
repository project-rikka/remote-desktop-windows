using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

namespace Rikka.Remote.Quic;

public readonly record struct Pin
{
    public const int Bytes = 32;
    private const int GroupDigits = 4;

    private readonly ulong first;
    private readonly ulong second;
    private readonly ulong third;
    private readonly ulong fourth;

    private Pin(ReadOnlySpan<byte> digest)
    {
        first = BinaryPrimitives.ReadUInt64BigEndian(digest);
        second = BinaryPrimitives.ReadUInt64BigEndian(digest[8..]);
        third = BinaryPrimitives.ReadUInt64BigEndian(digest[16..]);
        fourth = BinaryPrimitives.ReadUInt64BigEndian(digest[24..]);
    }

    public static Pin OfPublicKey(ReadOnlySpan<byte> subjectPublicKeyInfo)
    {
        Span<byte> digest = stackalloc byte[Bytes];
        SHA256.HashData(subjectPublicKeyInfo, digest);
        return new Pin(digest);
    }

    public static Pin OfCertificate(ReadOnlySpan<byte> certificate) => OfPublicKey(CertificateKey.SubjectPublicKeyInfo(certificate));

    public static bool TryParse(string? hex, [NotNullWhen(true)] out Pin? pin)
    {
        pin = null;
        if (hex is null || hex.Length != Bytes * 2)
        {
            return false;
        }

        Span<byte> digest = stackalloc byte[Bytes];
        if (Convert.FromHexString(hex, digest, out _, out var written) != System.Buffers.OperationStatus.Done || written != Bytes)
        {
            return false;
        }

        pin = new Pin(digest);
        return true;
    }

    public void CopyTo(Span<byte> destination)
    {
        BinaryPrimitives.WriteUInt64BigEndian(destination, first);
        BinaryPrimitives.WriteUInt64BigEndian(destination[8..], second);
        BinaryPrimitives.WriteUInt64BigEndian(destination[16..], third);
        BinaryPrimitives.WriteUInt64BigEndian(destination[24..], fourth);
    }

    public string ToHex()
    {
        Span<byte> digest = stackalloc byte[Bytes];
        CopyTo(digest);
        return Convert.ToHexStringLower(digest);
    }

    public string ToFingerprint()
    {
        var hex = ToHex();
        return string.Join(':', Enumerable.Range(0, hex.Length / GroupDigits).Select(group => hex.Substring(group * GroupDigits, GroupDigits)));
    }

    public override string ToString() => ToFingerprint();
}
