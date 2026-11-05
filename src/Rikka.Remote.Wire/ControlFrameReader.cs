namespace Rikka.Remote.Wire;

public sealed class ControlFrameReader
{
    private readonly byte[] header = new byte[ControlHeader.Bytes];
    private int headerFilled;
    private ControlHeader current;
    private byte[]? body;
    private int bodyFilled;

    public bool IsBetweenFrames => headerFilled == 0;

    public int Feed(ReadOnlySpan<byte> input, out ControlFrame? frame)
    {
        frame = null;
        var consumed = 0;
        if (headerFilled < ControlHeader.Bytes)
        {
            var taken = Math.Min(ControlHeader.Bytes - headerFilled, input.Length);
            input[..taken].CopyTo(header.AsSpan(headerFilled));
            headerFilled += taken;
            consumed += taken;
            if (headerFilled < ControlHeader.Bytes)
            {
                return consumed;
            }

            current = ControlHeader.Parse(header);
            body = new byte[current.BodyLength];
            bodyFilled = 0;
        }

        var rest = input[consumed..];
        var copied = Math.Min(current.BodyLength - bodyFilled, rest.Length);
        rest[..copied].CopyTo(body.AsSpan(bodyFilled));
        bodyFilled += copied;
        consumed += copied;
        if (bodyFilled == current.BodyLength)
        {
            frame = new ControlFrame(current.Kind, body!);
            body = null;
            headerFilled = 0;
        }

        return consumed;
    }
}
