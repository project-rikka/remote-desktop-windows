using Rikka.Remote.Wire;

namespace Rikka.Remote.Viewer.Input;

internal interface IInputSink
{
    void Send(in InputEvent input);

    void Move(Position at);
}
