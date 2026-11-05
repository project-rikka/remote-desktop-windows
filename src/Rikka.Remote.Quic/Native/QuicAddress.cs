using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace Rikka.Remote.Quic.Native;

[StructLayout(LayoutKind.Explicit, Size = 28)]
internal unsafe struct QuicAddress
{
    public const ushort Inet = 2;
    public const ushort Inet6 = 23;

    [FieldOffset(0)]
    public ushort Family;

    [FieldOffset(2)]
    public ushort Port;

    [FieldOffset(4)]
    public fixed byte V4[4];

    [FieldOffset(8)]
    public fixed byte V6[16];

    [FieldOffset(24)]
    public uint ScopeId;

    public static QuicAddress From(IPEndPoint endpoint)
    {
        var address = new QuicAddress { Port = BinaryPrimitives.ReverseEndianness((ushort)endpoint.Port) };
        if (endpoint.AddressFamily == AddressFamily.InterNetworkV6)
        {
            address.Family = Inet6;
            address.ScopeId = (uint)endpoint.Address.ScopeId;
            endpoint.Address.TryWriteBytes(new Span<byte>(address.V6, 16), out _);
        }
        else
        {
            address.Family = Inet;
            endpoint.Address.TryWriteBytes(new Span<byte>(address.V4, 4), out _);
        }

        return address;
    }
}
