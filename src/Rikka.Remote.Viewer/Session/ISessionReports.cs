using Rikka.Remote.Quic;
using Rikka.Remote.Wire;

namespace Rikka.Remote.Viewer.Session;

internal interface ISessionReports
{
    void FirstUse(Pin seen);

    void Connected(string host, string user, Pin? pin);

    void Password(ServerMessage.Refused? refused);

    void TokenRefused();

    void SignedIn(byte[]? token);

    void Display(ServerMessage.Display display);

    void Stream(ServerMessage.Stream stream);

    void CursorShape(ServerMessage.CursorShape shape);

    void Cursor(uint serial);

    void Stats(ServerMessage.Stats stats, ReceiverStatistics local);

    void FileSaved(string name);

    void FileFailed(string reason);

    void PinChanged(Pin seen);

    void Ended(EndReason reason);

    void Failed(string reason);
}
