namespace Rikka.Remote.Wire;

public readonly record struct Viewport(uint Width, uint Height, uint RefreshMhz)
{
    internal const int Bytes = 3 * sizeof(uint);

    internal void Write(ref WireWriter writer)
    {
        writer.U32(Width);
        writer.U32(Height);
        writer.U32(RefreshMhz);
    }

    internal static Viewport Read(ref WireReader reader) => new(reader.U32(), reader.U32(), reader.U32());
}
