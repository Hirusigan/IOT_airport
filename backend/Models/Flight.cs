namespace Luggage;

public class Flight
{
    public string Id { get; set; } = string.Empty;
    public string RunId { get; set; } = string.Empty;
    public string Number { get; set; } = string.Empty;
    public string Origin { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public DateTimeOffset ScheduledDeparture
    {
        get; set;
    }
    public DateTimeOffset? ActualDeparture
    {
        get; set;
    }
    public DateTimeOffset? ScheduledArrival
    {
        get; set;
    }
    public DateTimeOffset? ActualArrival
    {
        get; set;
    }
}
