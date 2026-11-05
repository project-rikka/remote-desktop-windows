namespace Rikka.Remote.Quic.Native;

[Flags]
internal enum QuicCredentialFlags
{
    None = 0,
    Client = 0x1,
    NoCertificateValidation = 0x4,
    IndicateCertificateReceived = 0x10,
    UsePortableCertificates = 0x4000,
}
