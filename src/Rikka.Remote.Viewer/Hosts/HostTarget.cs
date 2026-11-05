using System.Globalization;
using Rikka.Remote.Wire;

namespace Rikka.Remote.Viewer.Hosts;

internal sealed record HostTarget(string Host, ushort Port)
{
    private const int LongestName = 253;
    private const string NamePunctuation = "-._:";

    public string Address => Host.Contains(':', StringComparison.Ordinal) ? $"[{Host}]:{Port}" : $"{Host}:{Port}";

    public static HostTarget Parse(string text)
    {
        text = text.Trim();
        if (text.Length == 0)
        {
            throw new AddressException(AddressProblem.Empty, text);
        }

        string host;
        string? port;
        if (text.StartsWith('['))
        {
            var close = text.IndexOf(']', StringComparison.Ordinal);
            if (close < 0)
            {
                throw new AddressException(AddressProblem.Name, text);
            }

            host = text[1..close];
            var rest = text[(close + 1)..];
            port = rest.Length == 0 ? null : rest.StartsWith(':') ? rest[1..] : throw new AddressException(AddressProblem.Name, text);
        }
        else
        {
            var colon = text.LastIndexOf(':');
            (host, port) = colon >= 0 && !text[..colon].Contains(':', StringComparison.Ordinal) ? (text[..colon], text[(colon + 1)..]) : (text, null);
        }

        var number = Protocol.DefaultPort;
        if (port is not null && (!ushort.TryParse(port, NumberStyles.None, CultureInfo.InvariantCulture, out number) || number == 0))
        {
            throw new AddressException(AddressProblem.Port, port);
        }

        var valid = host.Length is > 0 and <= LongestName && host.All(character => char.IsAsciiLetterOrDigit(character) || NamePunctuation.Contains(character, StringComparison.Ordinal));
        return valid ? new HostTarget(host, number) : throw new AddressException(AddressProblem.Name, host);
    }
}
