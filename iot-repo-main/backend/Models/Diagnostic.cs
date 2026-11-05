namespace Luggage;

public class Diagnostic
{
    public long Id { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public string Topic { get; set; } = "";
    public string Reason { get; set; } = "";
}
