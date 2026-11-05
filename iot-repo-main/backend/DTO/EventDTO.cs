using System.Text.Json;
namespace Luggage;

public record Envelope(int SchemaVersion, string EventId, string SimulationRunId,
    DateTimeOffset OccurredAt, string AirportId, string DeviceId, string EventType, JsonElement Data);
public record BagData(string LuggageId, string JourneyId, int Sequence, string Stage, string? FlightId);
public record DepartureData(string FlightId, DateTimeOffset DepartedAt);
public record RunImport(string SimulationRunId, DateTimeOffset StartAt, Airport[] Airports,
    Device[] Devices, Flight[] Flights, Journey[] Journeys, Assignment[] Assignments);

