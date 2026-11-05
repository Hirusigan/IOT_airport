namespace Luggage;

public class Flight
{
    public string Id { get; set; } = "";
    public string RunId { get; set; } = "";
    public string Number { get; set; } = "";
    public string Origin { get; set; } = "";
    public string Destination { get; set; } = "";
    public DateTimeOffset ScheduledDeparture { get; set; }
    public DateTimeOffset? ActualDeparture { get; set; }
}
