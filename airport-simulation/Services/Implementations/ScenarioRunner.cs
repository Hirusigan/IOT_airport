using System.Text.Json;

namespace AirportSimulation;

public sealed record StartRequest(string ScenarioId);
public sealed record ScenarioInfo(string Id, string Name, string Description);
public sealed record SimStatus(string State, string? ScenarioId = null, string? RunId = null,
    DateTimeOffset? StartedAt = null, int PublishedEvents = 0, int TotalEvents = 0, string? Error = null);

public sealed class ScenarioRunner(ISimulationBackendClient backend, IDeviceCommandClient devices,
    IConfiguration config, ILogger<ScenarioRunner> logger) : IScenarioRunner
{
    public IReadOnlyList<ScenarioInfo> Scenarios { get; } = [
        new("mixed", "All scenarios", "Normal arrival, missing loading, wrong flight and unexpected airport."),
        new("normal", "Normal journey", "Check-in, correct loading, departure and arrival."),
        new("transfer", "Connecting flights", "BAG-TRANSFER flies AAR → CPH → AAR with a luggage transfer at Copenhagen."),
        new("missed", "Missed flight", "A bag is never loaded: warning followed by a missed-flight alert."),
        new("wrong-flight", "Wrong flight", "A bag is loaded onto the wrong flight."),
        new("unexpected-airport", "Unexpected airport", "A bag is scanned at the wrong airport.")];
    readonly object sync = new();
    CancellationTokenSource? cancellation;
    Task? running;
    SimStatus status = new("Idle");

    public SimStatus Status { get { lock (sync) return status; } }
    void Set(SimStatus value) { lock (sync) status = value; }

    static async Task WaitUntil(DateTimeOffset target, CancellationToken token)
    {
        var delay = target - DateTimeOffset.UtcNow;
        if (delay > TimeSpan.Zero) await Task.Delay(delay, token);
    }

    public Task Start(string id)
    {
        if (!Scenarios.Any(scenario => scenario.Id == id)) throw new ArgumentException("Unknown scenario");
        lock (sync)
        {
            if (running is { IsCompleted: false }) throw new InvalidOperationException("A simulation is already running");
            cancellation = new CancellationTokenSource();
            Set(new("Starting", id, StartedAt: DateTimeOffset.UtcNow));
            running = Run(id, cancellation.Token);
        }
        return Task.CompletedTask;
    }

    public async Task Stop()
    {
        Task? task;
        lock (sync) { cancellation?.Cancel(); task = running; }
        if (task != null) await task;
    }

    async Task Run(string id, CancellationToken token)
    {
        try
        {
            var path = Path.Combine(config["ScenariosDirectory"] ?? "/app/scenarios", id + ".json");
            using var scenario = JsonDocument.Parse(await File.ReadAllTextAsync(path, token));
            var root = scenario.RootElement;
            var runId = Guid.NewGuid().ToString();
            var start = DateTimeOffset.UtcNow.AddSeconds(15);
            string Scope(string value) => runId + ":" + value;
            var airports = root.GetProperty("airports").EnumerateArray().ToArray();
            var devicesInScenario = root.GetProperty("devices").EnumerateArray().Select(device => new ScheduledDevice(
                device.GetProperty("id").GetString()!, device.GetProperty("airportId").GetString()!,
                device.GetProperty("type").GetString()!)).ToArray();
            var flights = root.GetProperty("flights").EnumerateArray().Select(flight =>
            {
                var departure = start.AddSeconds(flight.GetProperty("departureTime").GetInt32());
                DateTimeOffset? arrival = flight.TryGetProperty("arrivalTime", out var arrivalOffset)
                    ? start.AddSeconds(arrivalOffset.GetInt32()) : null;
                return new ScheduledFlight(Scope(flight.GetProperty("flightId").GetString()!),
                    flight.GetProperty("number").GetString()!, flight.GetProperty("origin").GetString()!,
                    flight.GetProperty("destination").GetString()!, departure, arrival);
            }).ToArray();

            await backend.RegisterSchedule(new FlightScheduleRequest(runId, flights, devicesInScenario), token);
            var events = root.GetProperty("events").EnumerateArray().ToArray();
            Set(new("Running", id, runId, Status.StartedAt, 0, events.Length));
            await WaitUntil(start, token);

            var actions = events.Select(scenarioEvent => new ScheduledAction(
                    scenarioEvent.GetProperty("time").GetInt32(), scenarioEvent, null))
                .Concat(root.GetProperty("flights").EnumerateArray()
                    .Where(flight => flight.TryGetProperty("arrivalTime", out _))
                    .Select(flight => new ScheduledAction(flight.GetProperty("arrivalTime").GetInt32(), null,
                        Scope(flight.GetProperty("flightId").GetString()!))))
                .OrderBy(action => action.Time)
                .ThenBy(action => action.FlightId == null ? 1 : 0);

            foreach (var action in actions)
            {
                await WaitUntil(start.AddSeconds(action.Time), token);
                if (action.FlightId is { } arrivingFlightId)
                {
                    await backend.RecordArrival(arrivingFlightId,
                        new FlightArrivalRequest(runId, DateTimeOffset.UtcNow), token);
                    continue;
                }
                var scenarioEvent = action.Event!.Value;
                var airport = scenarioEvent.GetProperty("airport").GetString()!;
                var eventType = scenarioEvent.GetProperty("eventType").GetString()!;
                var deviceId = scenarioEvent.GetProperty("deviceId").GetString()!;
                var occurredAt = DateTimeOffset.UtcNow;
                string? luggageId = null;
                string? journeyId = null;
                int? sequence = null;
                string? stage = null;
                string? flightId = null;
                string? tripId = null;
                var legOrder = 1;

                if (eventType == "departure")
                {
                    flightId = Scope(scenarioEvent.GetProperty("flightId").GetString()!);
                    await backend.RecordDeparture(flightId, new FlightDepartureRequest(runId, occurredAt), token);
                }
                else
                {
                    var rawJourney = scenarioEvent.GetProperty("journeyId").GetString()!;
                    var luggage = root.GetProperty("luggage").EnumerateArray()
                        .First(item => item.GetProperty("journeyId").GetString() == rawJourney);
                    luggageId = scenarioEvent.GetProperty("luggageId").GetString();
                    journeyId = Scope(rawJourney);
                    sequence = scenarioEvent.GetProperty("sequence").GetInt32();
                    stage = scenarioEvent.GetProperty("stage").GetString();
                    if (scenarioEvent.TryGetProperty("flightId", out var observedFlight)) flightId = Scope(observedFlight.GetString()!);
                    else if (stage == "checkin") flightId = Scope(luggage.GetProperty("flightId").GetString()!);
                    tripId = Scope(luggage.TryGetProperty("tripId", out var trip) ? trip.GetString()! : rawJourney);
                    legOrder = luggage.TryGetProperty("legOrder", out var leg) ? leg.GetInt32() : 1;
                }

                var airportDefinition = airports.First(item => item.GetProperty("code").GetString() == airport);
                var command = new DeviceActionCommand(airport,
                    airportDefinition.GetProperty("host").GetString()!, airportDefinition.GetProperty("port").GetInt32(),
                    deviceId, eventType == "departure" ? "gate" : "scanner", eventType, runId, occurredAt,
                    luggageId, journeyId, sequence, stage, flightId, tripId, legOrder);
                await devices.Execute(command, token);
                Set(Status with { PublishedEvents = Status.PublishedEvents + 1 });
            }
            Set(Status with { State = "Completed" });
        }
        catch (OperationCanceledException) { Set(Status with { State = "Stopped" }); }
        catch (Exception e) { logger.LogError(e, "Airport scenario failed"); Set(Status with { State = "Failed", Error = e.Message }); }
    }

    sealed record ScheduledAction(int Time, JsonElement? Event, string? FlightId);
}
