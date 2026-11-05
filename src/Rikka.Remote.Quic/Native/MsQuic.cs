using System.Runtime.InteropServices;

namespace Rikka.Remote.Quic.Native;

internal static unsafe partial class MsQuic
{
    private const string Library = "msquic.dll";
    private const uint ApiVersion = 2;

    public static QuicApiTable* Open()
    {
        QuicApiTable* api;
        var status = MsQuicOpenVersion(ApiVersion, &api);
        return QuicStatus.Failed(status) ? throw new QuicException("cannot open msquic", status) : api;
    }

    public static void Close(QuicApiTable* api) => MsQuicClose(api);

    [LibraryImport(Library)]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    private static partial int MsQuicOpenVersion(uint version, QuicApiTable** api);

    [LibraryImport(Library)]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    private static partial void MsQuicClose(void* api);
}
