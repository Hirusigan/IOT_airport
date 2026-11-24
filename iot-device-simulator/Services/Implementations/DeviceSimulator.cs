using System.Text.Json;

namespace IotDeviceSimulator;

public sealed record DeviceActionCommand(
    string AirportId,
    string Host,
    int Port,
    string DeviceId,
    string DeviceType,
    string EventType,
    string SimulationRunId,
    DateTimeOffset OccurredAt,
    string? LuggageId = null,
    string? JourneyId = null,
    int? Sequence = null,
    string? Stage = null,
    string? FlightId = null,
    string? TripId = null,
    int LegOrder = 1);

public sealed record DeviceEventEnvelope(
    int SchemaVersion,
    string EventId,
    string SimulationRunId,
    DateTimeOffset OccurredAt,
    string AirportId,
    string DeviceId,
    string EventType,
    JsonElement Data);

public sealed class DeviceSimulator(IMqttEventPublisher publisher) : IDeviceSimulator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task Execute(DeviceActionCommand command, CancellationToken token)
    {
        ValidateCommand(command);
        var data = CreateLuggageData(command);
        var envelope = new DeviceEventEnvelope(
            1,
            Guid.NewGuid().ToString(),
            command.SimulationRunId,
            command.OccurredAt,
            command.AirportId,
            command.DeviceId,
            command.EventType,
            data);
        var topic = $"airport/{command.AirportId}/{command.DeviceType}/{command.DeviceId}/{command.EventType}";

        await publisher.Publish(
            command.Host,
            command.Port,
            topic,
            JsonSerializer.Serialize(envelope, JsonOptions),
            token);
    }

    private static void ValidateCommand(DeviceActionCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.AirportId) || command.AirportId.Length != 3 ||
            command.AirportId.Any(character => character is < 'A' or > 'Z') ||
            string.IsNullOrWhiteSpace(command.Host) ||
            command.Port is < 1 or > 65535 ||
            string.IsNullOrWhiteSpace(command.DeviceId) ||
            command.DeviceId.Contains('/') ||
            command.DeviceId.Contains('+') ||
            command.DeviceId.Contains('#') ||
            string.IsNullOrWhiteSpace(command.SimulationRunId) ||
            command.OccurredAt == default ||
            command.OccurredAt.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Invalid device action");
        }

        if (command.DeviceType != "scanner" || command.EventType != "luggage")
        {
            throw new ArgumentException("Device actions must be luggage scans from a scanner");
        }

        ValidateLuggage(command);
    }

    private static void ValidateLuggage(DeviceActionCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.LuggageId) ||
            string.IsNullOrWhiteSpace(command.JourneyId) ||
            command.Sequence is null or <= 0 ||
            command.Stage is not ("checkin" or "security" or "sorted" or "loaded" or "arrived") ||
            command.LegOrder < 1 ||
            ((command.Stage is "checkin" or "loaded") && string.IsNullOrWhiteSpace(command.FlightId)))
        {
            throw new ArgumentException("Invalid luggage action");
        }
    }

    private static JsonElement CreateLuggageData(DeviceActionCommand command)
    {
        var data = new Dictionary<string, object?>
        {
            ["luggageId"] = command.LuggageId,
            ["journeyId"] = command.JourneyId,
            ["sequence"] = command.Sequence,
            ["stage"] = command.Stage,
            ["legOrder"] = command.LegOrder
        };

        if (command.FlightId != null)
        {
            data["flightId"] = command.FlightId;
        }

        if (command.TripId != null)
        {
            data["tripId"] = command.TripId;
        }

        return JsonSerializer.SerializeToElement(data);
    }

}
