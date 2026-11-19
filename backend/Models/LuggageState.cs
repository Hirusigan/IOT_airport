namespace Luggage;

public class LuggageState
{
    public string JourneyId { get; set; } = string.Empty;
    public int Sequence
    {
        get; set;
    }
    public string AirportId { get; set; } = string.Empty;
    public LuggageStage Stage
    {
        get; set;
    }
    public string? FlightId
    {
        get; set;
    }
    public DateTimeOffset OccurredAt
    {
        get; set;
    }
}
