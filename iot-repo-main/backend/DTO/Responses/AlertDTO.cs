namespace Luggage;

public record AlertDTO(string Id, string Rule, string JourneyId, string FlightId, string AirportId,
    string Severity, bool Active, DateTimeOffset CreatedAt, DateTimeOffset? ResolvedAt)
{
    public static AlertDTO From(Alert alert) => new(alert.Id, alert.Rule.ToValue(), alert.JourneyId, alert.FlightId,
        alert.AirportId, alert.Severity.ToValue(), alert.Active, alert.CreatedAt, alert.ResolvedAt);
}
