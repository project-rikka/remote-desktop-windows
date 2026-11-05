namespace Rikka.Remote.Viewer.Hosts;

internal sealed class AddressException : Exception
{
    public AddressException()
    {
    }

    public AddressException(string message)
        : base(message)
    {
    }

    public AddressException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public AddressException(AddressProblem problem, string text)
        : base($"{problem}: {text}")
    {
        Problem = problem;
        Text = text;
    }

    public AddressProblem Problem { get; }

    public string Text { get; } = string.Empty;
}
