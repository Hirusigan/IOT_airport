namespace AirportSimulation;

public sealed record FlightScheduleRequest(string SimulationRunId, ScheduledFlight[] Flights, ScheduledDevice[] Devices);
public sealed record ScheduledFlight(string Id, string Number, string Origin, string Destination,
    DateTimeOffset ScheduledDeparture, DateTimeOffset? ScheduledArrival);
public sealed record ScheduledDevice(string Id, string AirportId, string Type);
public sealed record FlightDepartureRequest(string SimulationRunId, DateTimeOffset DepartedAt);
public sealed record FlightArrivalRequest(string SimulationRunId, DateTimeOffset ArrivedAt);

public sealed class SimulationBackendClient(HttpClient client, IConfiguration config) : ISimulationBackendClient
{
    public async Task RegisterSchedule(FlightScheduleRequest schedule, CancellationToken token)
    {
        client.BaseAddress ??= new Uri(config["BackendUrl"] ?? "http://backend:8080");
        using var response = await client.PostAsJsonAsync("/api/airports/schedule", schedule, token);
        response.EnsureSuccessStatusCode();
    }

    public async Task RecordDeparture(string flightId, FlightDepartureRequest departure, CancellationToken token)
    {
        client.BaseAddress ??= new Uri(config["BackendUrl"] ?? "http://backend:8080");
        using var response = await client.PostAsJsonAsync(
            $"/api/airports/flights/{Uri.EscapeDataString(flightId)}/departure", departure, token);
        response.EnsureSuccessStatusCode();
    }

    public async Task RecordArrival(string flightId, FlightArrivalRequest arrival, CancellationToken token)
    {
        client.BaseAddress ??= new Uri(config["BackendUrl"] ?? "http://backend:8080");
        using var response = await client.PostAsJsonAsync(
            $"/api/airports/flights/{Uri.EscapeDataString(flightId)}/arrival", arrival, token);
        response.EnsureSuccessStatusCode();
    }
}
