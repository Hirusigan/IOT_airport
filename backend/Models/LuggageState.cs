namespace Luggage;

public class LuggageState
{
    public string JourneyId { get; set; } = "";
    public int Sequence { get; set; }
    public string AirportId { get; set; } = "";
    public string Stage { get; set; } = "";
    public string? FlightId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}
