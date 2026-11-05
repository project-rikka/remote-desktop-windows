using System.Formats.Asn1;

namespace Rikka.Remote.Quic;

internal static class CertificateKey
{
    private const int FieldsBeforeKey = 5;
    private static readonly Asn1Tag Version = new(TagClass.ContextSpecific, 0, isConstructed: true);

    public static ReadOnlySpan<byte> SubjectPublicKeyInfo(ReadOnlySpan<byte> certificate)
    {
        var reader = new AsnReader(certificate.ToArray(), AsnEncodingRules.DER);
        var tbs = reader.ReadSequence().ReadSequence();
        if (tbs.PeekTag().HasSameClassAndValue(Version))
        {
            tbs.ReadEncodedValue();
        }

        for (var field = 0; field < FieldsBeforeKey; field++)
        {
            tbs.ReadEncodedValue();
        }

        return tbs.ReadEncodedValue().Span;
    }
}
