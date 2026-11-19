namespace Luggage;

public sealed class BrokerHealth
{
    public volatile bool Connected;
    public string? LastError
    {
        get; set;
    }
}

