namespace Luggage;

public class Assignment
{
    public string JourneyId { get; set; } = "";
    public string FlightId { get; set; } = "";
    public DateTimeOffset? LoadedAt { get; set; }
}
