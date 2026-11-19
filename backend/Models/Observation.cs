namespace Luggage;

public class Observation
{
    public string EventId { get; set; } = string.Empty;
    public string RunId { get; set; } = string.Empty;
    public string AirportId { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string? JourneyId
    {
        get; set;
    }
    public EventType EventType
    {
        get; set;
    }
    public int SchemaVersion
    {
        get; set;
    }
    public DateTimeOffset OccurredAt
    {
        get; set;
    }
    public DateTimeOffset ReceivedAt
    {
        get; set;
    }
    public string Payload { get; set; } = string.Empty;
    public ObservationOutcome Outcome { get; set; } = ObservationOutcome.Accepted;
}
