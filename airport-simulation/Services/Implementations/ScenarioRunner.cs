using System.Text.Json;
using Microsoft.Extensions.Hosting;

namespace AirportSimulation;

public sealed record StartRequest(string ScenarioId);
public sealed record ScenarioInfo(string Id, string Name, string Description);
public sealed record SimStatus(string State, string? ScenarioId = null, string? RunId = null,
    DateTimeOffset? StartedAt = null, int PublishedEvents = 0, int TotalEvents = 0, string? Error = null);

public sealed class ScenarioRunner(ISimulationBackendClient backend, IDeviceCommandClient devices,
    IConfiguration config, IHostApplicationLifetime lifetime) : IScenarioRunner
{
    public IReadOnlyList<ScenarioInfo> Scenarios
    {
        get;
    } = [
        new("mixed", "All scenarios", "Normal arrival, missing loading, wrong flight and unexpected airport."),
        new("normal", "Normal journey", "Check-in, correct loading, departure and arrival."),
        new("transfer", "Connecting flights", "BAG-TRANSFER flies AAR → CPH → AAR with a luggage transfer at Copenhagen."),
        new("missed", "Missed flight", "A bag is never loaded: warning followed by a missed-flight alert."),
        new("wrong-flight", "Wrong flight", "A bag is loaded onto the wrong flight."),
        new("unexpected-airport", "Unexpected airport", "A bag is scanned at the wrong airport.")];
    TaskCompletionSource<bool>? stopRequested;
    Task? running;
    SimStatus status = new("Idle");

    public SimStatus Status => status;

    void Set(SimStatus value)
    {
        status = value;
    }

    static async Task<bool> WaitUntil(DateTimeOffset target, Task stopSignal, CancellationToken token)
    {
        if (stopSignal.IsCompleted)
            return false;

        var delay = target - DateTimeOffset.UtcNow;
        if (delay <= TimeSpan.Zero)
            return !stopSignal.IsCompleted;

        using var delayCancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        var wait = Task.Delay(delay, delayCancellation.Token);
        var completed = await Task.WhenAny(wait, stopSignal);
        if (completed == stopSignal)
        {
            delayCancellation.Cancel();
            try
            {
                await wait;
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
            }

            return false;
        }

        await wait;
        return !stopSignal.IsCompleted;
    }

    public Task Start(string id)
    {
        if (!Scenarios.Any(scenario => scenario.Id == id))
            throw new ArgumentException("Unknown scenario");
        if (running is { IsCompleted: false })
        {
            throw new InvalidOperationException("A simulation is already running");
        }

        stopRequested = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Set(new("Starting", id, StartedAt: DateTimeOffset.UtcNow));
        running = Run(id, stopRequested.Task, lifetime.ApplicationStopping);
        return Task.CompletedTask;
    }

    public async Task Stop()
    {
        stopRequested?.TrySetResult(true);
        if (running != null)
            await running;
    }

    private async Task Run(string id, Task stopSignal, CancellationToken token)
    {
        try
        {
            using var scenario = await LoadScenario(id, token);
            if (stopSignal.IsCompleted)
            {
                Set(Status with { State = "Stopped" });
                return;
            }

            var root = scenario.RootElement;
            var runId = Guid.NewGuid().ToString();
            var start = DateTimeOffset.UtcNow.AddSeconds(15);
            var airports = root.GetProperty("airports").EnumerateArray().ToArray();

            var schedule = CreateSchedule(root, runId, start);
            await backend.RegisterSchedule(schedule, token);

            var events = root.GetProperty("events").EnumerateArray().ToArray();
            Set(new("Running", id, runId, Status.StartedAt, 0, events.Length));
            if (!await WaitUntil(start, stopSignal, token))
            {
                Set(Status with { State = "Stopped" });
                return;
            }

            var completed = await ExecuteActions(root, events, airports, runId, start, stopSignal, token);
            Set(Status with { State = completed ? "Completed" : "Stopped" });
        }
        catch (OperationCanceledException)
        {
            Set(Status with
            {
                State = "Stopped"
            });
        }
        catch (Exception e)
        {
            Set(Status with
            {
                State = "Failed",
                Error = e.Message
            });
        }
    }

    private async Task<JsonDocument> LoadScenario(string id, CancellationToken token)
    {
        var path = Path.Combine(config["ScenariosDirectory"] ?? "/app/scenarios", id + ".json");
        var contents = await File.ReadAllTextAsync(path, token);
        return JsonDocument.Parse(contents);
    }

    private static FlightScheduleRequest CreateSchedule(JsonElement root, string runId, DateTimeOffset start)
    {
        var devices = root.GetProperty("devices")
            .EnumerateArray()
            .Select(device => new ScheduledDevice(
                device.GetProperty("id").GetString()!,
                device.GetProperty("airportId").GetString()!,
                device.GetProperty("type").GetString()!))
            .ToArray();

        var flights = root.GetProperty("flights")
            .EnumerateArray()
            .Select(flight => CreateScheduledFlight(flight, runId, start))
            .ToArray();

        return new FlightScheduleRequest(runId, flights, devices);
    }

    private static ScheduledFlight CreateScheduledFlight(
        JsonElement flight,
        string runId,
        DateTimeOffset start)
    {
        var departure = start.AddSeconds(flight.GetProperty("departureTime").GetInt32());
        DateTimeOffset? arrival = flight.TryGetProperty("arrivalTime", out var arrivalOffset)
            ? start.AddSeconds(arrivalOffset.GetInt32())
            : null;

        return new ScheduledFlight(
            Scope(runId, flight.GetProperty("flightId").GetString()!),
            flight.GetProperty("number").GetString()!,
            flight.GetProperty("origin").GetString()!,
            flight.GetProperty("destination").GetString()!,
            departure,
            arrival);
    }

    private async Task<bool> ExecuteActions(
        JsonElement root,
        JsonElement[] events,
        JsonElement[] airports,
        string runId,
        DateTimeOffset start,
        Task stopSignal,
        CancellationToken token)
    {
        foreach (var action in CreateActions(root, events, runId))
        {
            if (stopSignal.IsCompleted || !await WaitUntil(start.AddSeconds(action.Time), stopSignal, token))
                return false;

            if (action.FlightId is { } flightId)
            {
                await backend.RecordArrival(
                    flightId,
                    new FlightArrivalRequest(runId, DateTimeOffset.UtcNow),
                    token);
            }
            else
            {
                await ExecuteScenarioEvent(root, airports, action.Event!.Value, runId, token);
            }

            if (stopSignal.IsCompleted)
                return false;
        }

        return true;
    }

    private static IOrderedEnumerable<ScheduledAction> CreateActions(
        JsonElement root,
        JsonElement[] events,
        string runId)
    {
        var eventActions = events.Select(scenarioEvent => new ScheduledAction(
            scenarioEvent.GetProperty("time").GetInt32(),
            scenarioEvent,
            null));
        var arrivalActions = root.GetProperty("flights")
            .EnumerateArray()
            .Where(flight => flight.TryGetProperty("arrivalTime", out _))
            .Select(flight => new ScheduledAction(
                flight.GetProperty("arrivalTime").GetInt32(),
                null,
                Scope(runId, flight.GetProperty("flightId").GetString()!)));

        return eventActions
            .Concat(arrivalActions)
            .OrderBy(action => action.Time)
            .ThenBy(action => action.FlightId == null ? 1 : 0);
    }

    private async Task ExecuteScenarioEvent(
        JsonElement root,
        JsonElement[] airports,
        JsonElement scenarioEvent,
        string runId,
        CancellationToken token)
    {
        var occurredAt = DateTimeOffset.UtcNow;
        var eventType = scenarioEvent.GetProperty("eventType").GetString()!;
        if (eventType == "departure")
        {
            var flightId = Scope(runId, scenarioEvent.GetProperty("flightId").GetString()!);
            await backend.RecordDeparture(
                flightId,
                new FlightDepartureRequest(runId, occurredAt),
                token);
        }
        else
        {
            var command = CreateLuggageCommand(root, airports, scenarioEvent, eventType, runId, occurredAt);
            await devices.Execute(command, token);
        }

        Set(Status with { PublishedEvents = Status.PublishedEvents + 1 });
    }

    private static DeviceActionCommand CreateLuggageCommand(
        JsonElement root,
        JsonElement[] airports,
        JsonElement scenarioEvent,
        string eventType,
        string runId,
        DateTimeOffset occurredAt)
    {
        var airportId = scenarioEvent.GetProperty("airport").GetString()!;
        var deviceId = scenarioEvent.GetProperty("deviceId").GetString()!;
        var airport = airports.First(item => item.GetProperty("code").GetString() == airportId);
        var rawJourneyId = scenarioEvent.GetProperty("journeyId").GetString()!;
        var luggage = root.GetProperty("luggage")
            .EnumerateArray()
            .First(item => item.GetProperty("journeyId").GetString() == rawJourneyId);
        var stage = scenarioEvent.GetProperty("stage").GetString();
        string? flightId = null;
        if (scenarioEvent.TryGetProperty("flightId", out var observedFlight))
        {
            flightId = Scope(runId, observedFlight.GetString()!);
        }
        else if (stage == "checkin")
        {
            flightId = Scope(runId, luggage.GetProperty("flightId").GetString()!);
        }

        var tripId = luggage.TryGetProperty("tripId", out var trip)
            ? trip.GetString()!
            : rawJourneyId;
        var legOrder = luggage.TryGetProperty("legOrder", out var leg)
            ? leg.GetInt32()
            : 1;

        return new DeviceActionCommand(
            airportId,
            airport.GetProperty("host").GetString()!,
            airport.GetProperty("port").GetInt32(),
            deviceId,
            "scanner",
            eventType,
            runId,
            occurredAt,
            scenarioEvent.GetProperty("luggageId").GetString(),
            Scope(runId, rawJourneyId),
            scenarioEvent.GetProperty("sequence").GetInt32(),
            stage,
            flightId,
            Scope(runId, tripId),
            legOrder);
    }

    private static string Scope(string runId, string value) => $"{runId}:{value}";

    private sealed record ScheduledAction(int Time, JsonElement? Event, string? FlightId);
}
