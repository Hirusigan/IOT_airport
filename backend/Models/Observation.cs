namespace Luggage;

public class Observation
{
    public string EventId { get; set; } = "";
    public string RunId { get; set; } = "";
    public string AirportId { get; set; } = "";
    public string DeviceId { get; set; } = "";
    public string? JourneyId { get; set; }
    public string EventType { get; set; } = "";
    public int SchemaVersion { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public string Payload { get; set; } = "";
    public string Outcome { get; set; } = "accepted";
}
