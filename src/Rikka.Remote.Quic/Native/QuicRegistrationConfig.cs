using System.Runtime.InteropServices;

namespace Rikka.Remote.Quic.Native;

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct QuicRegistrationConfig
{
    public byte* AppName;
    public QuicExecutionProfile ExecutionProfile;
}
