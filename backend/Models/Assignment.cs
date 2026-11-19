namespace Luggage;

public class Assignment
{
    public string JourneyId { get; set; } = string.Empty;
    public string FlightId { get; set; } = string.Empty;
    public DateTimeOffset? LoadedAt
    {
        get; set;
    }
}
