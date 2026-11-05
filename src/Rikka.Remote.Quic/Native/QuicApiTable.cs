using System.Runtime.InteropServices;

namespace Rikka.Remote.Quic.Native;

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct QuicApiTable
{
    public delegate* unmanaged[Cdecl]<nint, void*, void> SetContext;
    public delegate* unmanaged[Cdecl]<nint, void*> GetContext;
    public delegate* unmanaged[Cdecl]<nint, void*, void*, void> SetCallbackHandler;
    public delegate* unmanaged[Cdecl]<nint, uint, uint, void*, int> SetParam;
    public delegate* unmanaged[Cdecl]<nint, uint, uint*, void*, int> GetParam;
    public delegate* unmanaged[Cdecl]<QuicRegistrationConfig*, nint*, int> RegistrationOpen;
    public delegate* unmanaged[Cdecl]<nint, void> RegistrationClose;
    public delegate* unmanaged[Cdecl]<nint, int, ulong, void> RegistrationShutdown;
    public delegate* unmanaged[Cdecl]<nint, QuicBuffer*, uint, QuicSettings*, uint, void*, nint*, int> ConfigurationOpen;
    public delegate* unmanaged[Cdecl]<nint, void> ConfigurationClose;
    public delegate* unmanaged[Cdecl]<nint, QuicCredentialConfig*, int> ConfigurationLoadCredential;
    public void* ListenerOpen;
    public void* ListenerClose;
    public void* ListenerStart;
    public void* ListenerStop;
    public delegate* unmanaged[Cdecl]<nint, delegate* unmanaged[Cdecl]<nint, void*, QuicConnectionEvent*, int>, void*, nint*, int> ConnectionOpen;
    public delegate* unmanaged[Cdecl]<nint, void> ConnectionClose;
    public delegate* unmanaged[Cdecl]<nint, int, ulong, void> ConnectionShutdown;
    public delegate* unmanaged[Cdecl]<nint, nint, ushort, byte*, ushort, int> ConnectionStart;
    public void* ConnectionSetConfiguration;
    public void* ConnectionSendResumptionTicket;
    public delegate* unmanaged[Cdecl]<nint, QuicStreamOpenFlags, delegate* unmanaged[Cdecl]<nint, void*, QuicStreamEvent*, int>, void*, nint*, int> StreamOpen;
    public delegate* unmanaged[Cdecl]<nint, void> StreamClose;
    public delegate* unmanaged[Cdecl]<nint, QuicStreamStartFlags, int> StreamStart;
    public delegate* unmanaged[Cdecl]<nint, QuicStreamShutdownFlags, ulong, int> StreamShutdown;
    public delegate* unmanaged[Cdecl]<nint, QuicBuffer*, uint, QuicSendFlags, void*, int> StreamSend;
    public delegate* unmanaged[Cdecl]<nint, ulong, void> StreamReceiveComplete;
    public delegate* unmanaged[Cdecl]<nint, byte, int> StreamReceiveSetEnabled;
    public delegate* unmanaged[Cdecl]<nint, QuicBuffer*, uint, QuicSendFlags, void*, int> DatagramSend;
}
