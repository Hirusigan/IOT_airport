namespace Luggage;

public class Diagnostic
{
    public long Id
    {
        get; set;
    }
    public DateTimeOffset ReceivedAt
    {
        get; set;
    }
    public string Topic { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}
