namespace Luggage;

public class Alert
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public AlertType Rule { get; set; }
    public string JourneyId { get; set; } = "";
    public string FlightId { get; set; } = "";
    public string AirportId { get; set; } = "";
    public AlertSeverity Severity { get; set; } = AlertSeverity.Red;
    public bool Active { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
}
