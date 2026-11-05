namespace Rikka.Remote.Quic;

public sealed class Trust
{
    private Trust(Pin? expected)
    {
        Expected = expected;
    }

    public Pin? Expected { get; }

    public Pin? Seen { get; private set; }

    public bool Mismatched => Expected is { } expected && Seen is { } seen && expected != seen;

    public static Trust FirstUse() => new(null);

    public static Trust Pinned(Pin pin) => new(pin);

    internal bool Admit(ReadOnlySpan<byte> certificate)
    {
        Seen = Pin.OfCertificate(certificate);
        return !Mismatched;
    }
}
