using Rikka.Remote.Quic.Native;
using Rikka.Remote.Wire;

namespace Rikka.Remote.Quic;

internal sealed unsafe class QuicLibrary
{
    public const string ServerName = "rikka-remote";
    public const uint IdleTimeoutMs = 10_000;
    public const uint KeepAliveIntervalMs = 1_000;
    public const uint HandshakeIdleTimeoutMs = 10_000;
    public const uint InitialRttMs = 30;

    private static readonly Lazy<QuicLibrary> Instance = new(() => new QuicLibrary(), LazyThreadSafetyMode.ExecutionAndPublication);

    private QuicLibrary()
    {
        Api = MsQuic.Open();
        byte* appName = stackalloc byte[32];
        "rikka-remote-viewer\0"u8.CopyTo(new Span<byte>(appName, 32));
        var registrationConfig = new QuicRegistrationConfig { AppName = appName, ExecutionProfile = QuicExecutionProfile.LowLatency };
        nint registration;
        Check(Api->RegistrationOpen(&registrationConfig, &registration), "cannot open a registration");
        Registration = registration;
        Configuration = OpenConfiguration();
    }

    public static QuicLibrary Shared => Instance.Value;

    public QuicApiTable* Api { get; }

    public nint Registration { get; }

    public nint Configuration { get; }

    public static void Check(int status, string what)
    {
        if (QuicStatus.Failed(status))
        {
            throw new QuicException(what, status);
        }
    }

    private nint OpenConfiguration()
    {
        var alpn = Protocol.Alpn;
        byte* alpnBytes = stackalloc byte[alpn.Length];
        alpn.CopyTo(new Span<byte>(alpnBytes, alpn.Length));
        var alpnBuffer = new QuicBuffer { Length = (uint)alpn.Length, Buffer = alpnBytes };
        var settings = new QuicSettings
        {
            IsSetFlags = QuicSettings.HandshakeIdleTimeoutIsSet
                | QuicSettings.IdleTimeoutIsSet
                | QuicSettings.InitialRttIsSet
                | QuicSettings.KeepAliveIntervalIsSet
                | QuicSettings.PeerBidiStreamCountIsSet
                | QuicSettings.PeerUnidiStreamCountIsSet
                | QuicSettings.DatagramReceiveEnabledIsSet,
            HandshakeIdleTimeoutMs = HandshakeIdleTimeoutMs,
            IdleTimeoutMs = IdleTimeoutMs,
            InitialRttMs = InitialRttMs,
            KeepAliveIntervalMs = KeepAliveIntervalMs,
            PeerBidiStreamCount = 0,
            PeerUnidiStreamCount = 0,
            EnabledBits = QuicSettings.DatagramReceiveEnabledBit,
        };
        nint configuration;
        Check(Api->ConfigurationOpen(Registration, &alpnBuffer, 1, &settings, (uint)sizeof(QuicSettings), null, &configuration), "cannot open a configuration");
        var credential = new QuicCredentialConfig
        {
            Flags = QuicCredentialFlags.Client
                | QuicCredentialFlags.NoCertificateValidation
                | QuicCredentialFlags.IndicateCertificateReceived
                | QuicCredentialFlags.UsePortableCertificates,
        };
        Check(Api->ConfigurationLoadCredential(configuration, &credential), "cannot load the client credential");
        return configuration;
    }
}
