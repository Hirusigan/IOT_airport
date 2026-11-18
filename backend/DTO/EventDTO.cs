using System.Text.Json;
namespace Luggage;

public record Envelope(int SchemaVersion, string EventId, string SimulationRunId,
    DateTimeOffset OccurredAt, string AirportId, string DeviceId, string EventType, JsonElement Data);
public record BagData(string LuggageId, string JourneyId, int Sequence, string Stage, string? FlightId, string? TripId = null, int LegOrder = 1);
public record DepartureData(string FlightId, DateTimeOffset DepartedAt);
public record FlightSchedule(string SimulationRunId, FlightUpdate[] Flights, DeviceUpdate[]? Devices = null);
public record FlightUpdate(string Id, string Number, string Origin, string Destination, DateTimeOffset ScheduledDeparture,
    DateTimeOffset? ScheduledArrival = null);
public record DeviceUpdate(string Id, string AirportId, string Type);
public record FlightDepartureUpdate(string SimulationRunId, DateTimeOffset DepartedAt);
public record FlightArrivalUpdate(string SimulationRunId, DateTimeOffset ArrivedAt);

