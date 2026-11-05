using System.Runtime.InteropServices;

namespace Rikka.Remote.Quic.Native;

[StructLayout(LayoutKind.Explicit, Size = 56)]
internal struct QuicCredentialConfig
{
    [FieldOffset(0)]
    public int Type;

    [FieldOffset(4)]
    public QuicCredentialFlags Flags;
}
