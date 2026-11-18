namespace AirportSimulation;

public sealed record DeviceActionCommand(string AirportId, string Host, int Port, string DeviceId, string DeviceType,
    string EventType, string SimulationRunId, DateTimeOffset OccurredAt, string? LuggageId = null,
    string? JourneyId = null, int? Sequence = null, string? Stage = null, string? FlightId = null,
    string? TripId = null, int LegOrder = 1);

public sealed class DeviceCommandClient(HttpClient client, IConfiguration config) : IDeviceCommandClient
{
    public async Task Execute(DeviceActionCommand command, CancellationToken token)
    {
        var url = config["DeviceSimulatorUrl"] ?? "http://iot-device-simulator:8080/devices/actions";
        using var response = await client.PostAsJsonAsync(url, command, token);
        response.EnsureSuccessStatusCode();
    }
}
