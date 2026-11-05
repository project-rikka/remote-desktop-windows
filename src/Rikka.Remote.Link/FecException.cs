namespace Rikka.Remote.Link;

public sealed class FecException : Exception
{
    public FecException()
    {
    }

    public FecException(string message)
        : base(message)
    {
    }

    public FecException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public FecException(FecError error, int have, int need)
        : base(Describe(error, have, need))
    {
        Error = error;
        Have = have;
        Need = need;
    }

    public FecError Error { get; }

    public int Have { get; }

    public int Need { get; }

    private static string Describe(FecError error, int have, int need) => error switch
    {
        FecError.TooMany => $"{have} shards are more than {need}",
        FecError.NoData => "a block needs at least one data shard",
        _ => $"only {have} of the {need} shards needed arrived",
    };
}
