namespace Luggage;

public record AirportDTO(string Id, string Name, double Latitude, double Longitude, string RegionId,
    DateTimeOffset? LastSeenAt, string BusinessStatus, int AlertCount, string TelemetryHealth);
public record AirportDetailsDTO(AirportDTO Airport, IReadOnlyList<Flight> Flights, IReadOnlyList<AlertDTO> Alerts);
