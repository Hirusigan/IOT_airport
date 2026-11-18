using System.Text.Json;
using System.Net.Http.Json;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient();
builder.Services.AddSingleton<ScenarioRunner>();
var app = builder.Build();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/simulation/status", (ScenarioRunner runner) => runner.Status);
app.MapPost("/simulation/start", async (StartRequest request, ScenarioRunner runner) =>
{
    try { await runner.Start(request.ScenarioId); return Results.Accepted("/simulation/status", runner.Status); }
    catch (ArgumentException e) { return Results.BadRequest(new { error = e.Message }); }
    catch (InvalidOperationException e) { return Results.Conflict(new { error = e.Message }); }
});
app.MapPost("/simulation/stop", async (ScenarioRunner runner) => { await runner.Stop(); return Results.Ok(runner.Status); });
app.Run();

record StartRequest(string ScenarioId);
record SimStatus(string State, string? ScenarioId = null, string? RunId = null, DateTimeOffset? StartedAt = null,
    int PublishedEvents = 0, int TotalEvents = 0, string? Error = null);
sealed class ScenarioRunner(IHttpClientFactory clients, IConfiguration config, ILogger<ScenarioRunner> logger)
{
    static readonly string[] Names = ["mixed", "normal", "transfer", "missed", "wrong-flight", "unexpected-airport"];
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
        if (!Names.Contains(id)) throw new ArgumentException("Unknown scenario");
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
            var devices = root.GetProperty("devices").EnumerateArray().Select(d => new { id = d.GetProperty("id").GetString(), airportId = d.GetProperty("airportId").GetString(), type = d.GetProperty("type").GetString() }).ToArray();
            var flights = root.GetProperty("flights").EnumerateArray().Select(f => new { id = Scope(f.GetProperty("flightId").GetString()!), number = f.GetProperty("number").GetString(), origin = f.GetProperty("origin").GetString(), destination = f.GetProperty("destination").GetString(), scheduledDeparture = start.AddSeconds(f.GetProperty("departureTime").GetInt32()) }).ToArray();
            var backend = clients.CreateClient(); backend.BaseAddress = new Uri(config["BackendUrl"] ?? "http://backend:8080");
            var scheduleResponse = await backend.PostAsJsonAsync("/api/airports/schedule", new { simulationRunId = runId, flights, devices }, token);
            scheduleResponse.EnsureSuccessStatusCode();
            var events = root.GetProperty("events").EnumerateArray().ToArray();
            Set(new("Running", id, runId, status.StartedAt, 0, events.Length));
            await WaitUntil(start, token);
            foreach (var e in events)
            {
                await WaitUntil(start.AddSeconds(e.GetProperty("time").GetInt32()), token);
                var airport = e.GetProperty("airport").GetString()!;
                var eventType = e.GetProperty("eventType").GetString()!;
                var deviceId = e.GetProperty("deviceId").GetString()!;
                var occurredAt = DateTimeOffset.UtcNow;
                var data = new Dictionary<string, object?>();
                if (eventType == "departure")
                {
                    var flightId = Scope(e.GetProperty("flightId").GetString()!);
                    data["flightId"] = flightId; data["departedAt"] = occurredAt;
                    var departureResponse = await backend.PostAsJsonAsync($"/api/airports/flights/{Uri.EscapeDataString(flightId)}/departure",
                        new { simulationRunId = runId, departedAt = occurredAt }, token);
                    departureResponse.EnsureSuccessStatusCode();
                }
                else
                {
                    var rawJourney = e.GetProperty("journeyId").GetString()!;
                    var luggage = root.GetProperty("luggage").EnumerateArray().First(x => x.GetProperty("journeyId").GetString() == rawJourney);
                    data["luggageId"] = e.GetProperty("luggageId").GetString(); data["journeyId"] = Scope(rawJourney);
                    data["sequence"] = e.GetProperty("sequence").GetInt32(); data["stage"] = e.GetProperty("stage").GetString();
                    if (e.TryGetProperty("flightId", out var fid)) data["flightId"] = Scope(fid.GetString()!);
                    else if (e.GetProperty("stage").GetString() == "checkin") data["flightId"] = Scope(luggage.GetProperty("flightId").GetString()!);
                    data["tripId"] = Scope(luggage.TryGetProperty("tripId", out var trip) ? trip.GetString()! : rawJourney);
                    data["legOrder"] = luggage.TryGetProperty("legOrder", out var leg) ? leg.GetInt32() : 1;
                }
                var deviceType = eventType == "departure" ? "gate" : "scanner";
                var topic = $"airport/{airport}/{deviceType}/{deviceId}/{eventType}";
                var envelope = new { schemaVersion = 1, eventId = Guid.NewGuid().ToString(), simulationRunId = runId,
                    occurredAt, airportId = airport, deviceId, eventType, data };
                var response = await clients.CreateClient().PostAsJsonAsync(config["DeviceSimulatorUrl"] ?? "http://iot-device-simulator:8080/publish",
                    new { airportId = airport, host = airports.First(a => a.GetProperty("code").GetString() == airport).GetProperty("host").GetString(),
                        port = airports.First(a => a.GetProperty("code").GetString() == airport).GetProperty("port").GetInt32(), topic, payload = JsonSerializer.Serialize(envelope) }, token);
                response.EnsureSuccessStatusCode();
                Set(Status with { PublishedEvents = Status.PublishedEvents + 1 });
            }
            Set(Status with { State = "Completed" });
        }
        catch (OperationCanceledException) { Set(Status with { State = "Stopped" }); }
        catch (Exception e) { logger.LogError(e, "Airport scenario failed"); Set(Status with { State = "Failed", Error = e.Message }); }
    }
}
