using System.Net.Http.Json;
using System.Text.Json;
namespace Luggage;

public sealed class SimulationRunner(IHttpClientFactory clients, IConfiguration config, ILogger<SimulationRunner> log) : BackgroundService, ISimulationRunner
{
    public IReadOnlyList<ScenarioDTO> Scenarios { get; } = [
        new("mixed", "All scenarios", "Normal arrival, missing loading, wrong flight and unexpected airport."),
        new("normal", "Normal journey", "Check-in, correct loading, departure and arrival."),
        new("transfer", "Connecting flights", "BAG-TRANSFER flies AAR → CPH → AAR with a luggage transfer at Copenhagen."),
        new("missed", "Missed flight", "A bag is never loaded: warning followed by a missed-flight alert."),
        new("wrong-flight", "Wrong flight", "A bag is loaded onto the wrong flight."),
        new("unexpected-airport", "Unexpected airport", "A bag is scanned at the wrong airport.")];
    readonly object sync = new();
    SimulationStatusDTO status = new("Idle");
    CancellationTokenSource? cancellation;
    Task? polling;
    string RunsDirectory => config["Simulation:RunsDirectory"] ?? "/runs";
    string Url => config["AirportSimulation:Url"] ?? "http://airport-simulation:8080";
    public SimulationStatusDTO Status { get { lock (sync) return status; } }
    void Update(SimulationStatusDTO value)
    {
        lock (sync) status = value;
        try { Directory.CreateDirectory(RunsDirectory); File.WriteAllText(Path.Combine(RunsDirectory, "status.json"), JsonSerializer.Serialize(value)); }
        catch (IOException ex) { log.LogWarning(ex, "Could not persist simulation status"); }
    }
    public async Task Start(string scenarioId)
    {
        if (!Scenarios.Any(x => x.Id == scenarioId)) throw new ArgumentException("Unknown scenario");
        lock (sync) if (polling is { IsCompleted: false }) throw new ConflictException("A simulation is already running");
        using var client = clients.CreateClient();
        client.BaseAddress = new Uri(Url);
        var response = await client.PostAsJsonAsync("/simulation/start", new { scenarioId });
        if (response.StatusCode == System.Net.HttpStatusCode.Conflict) throw new ConflictException("A simulation is already running");
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException(await response.Content.ReadAsStringAsync());
        var remote = await response.Content.ReadFromJsonAsync<SimulationStatusDTO>() ?? new("Starting", scenarioId);
        Update(remote);
        cancellation = new CancellationTokenSource();
        polling = Poll(cancellation.Token);
    }
    public async Task Stop()
    {
        using var client = clients.CreateClient(); client.BaseAddress = new Uri(Url);
        var response = await client.PostAsync("/simulation/stop", null);
        response.EnsureSuccessStatusCode();
        var remote = await response.Content.ReadFromJsonAsync<SimulationStatusDTO>();
        if (remote != null) Update(remote);
        cancellation?.Cancel();
        if (polling != null) await polling;
    }
    async Task Poll(CancellationToken token)
    {
        using var client = clients.CreateClient(); client.BaseAddress = new Uri(Url);
        try
        {
            while (!token.IsCancellationRequested)
            {
                var remote = await client.GetFromJsonAsync<SimulationStatusDTO>("/simulation/status", token);
                if (remote != null) Update(remote);
                if (remote?.State is "Completed" or "Stopped" or "Failed" or "Idle") return;
                await Task.Delay(1000, token);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) { log.LogWarning(ex, "Could not poll Airport Simulation status"); Update(Status with { State = "Failed", Error = ex.Message }); }
    }
    protected override async Task ExecuteAsync(CancellationToken token)
    {
        Directory.CreateDirectory(RunsDirectory);
        var path = Path.Combine(RunsDirectory, "status.json");
        if (File.Exists(path))
        {
            try
            {
                var saved = JsonSerializer.Deserialize<SimulationStatusDTO>(File.ReadAllText(path));
                if (saved != null && saved.State is "Starting" or "Running")
                {
                    try
                    {
                        using var client = clients.CreateClient(); client.BaseAddress = new Uri(Url);
                        client.Timeout = TimeSpan.FromSeconds(5);
                        var remote = await client.GetFromJsonAsync<SimulationStatusDTO>("/simulation/status", token);
                        if (remote?.State is "Starting" or "Running")
                        {
                            Update(remote);
                            cancellation = new CancellationTokenSource();
                            polling = Poll(cancellation.Token);
                            return;
                        }
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    { log.LogWarning(ex, "Could not recover Airport Simulation status after backend restart"); }
                    Update(saved with { State = "Interrupted", Error = "Backend restarted during simulation." });
                }
                else if (saved != null) Update(saved);
            }
            catch (JsonException ex) { log.LogWarning(ex, "Could not read previous simulation status"); }
        }
        await Task.CompletedTask;
    }
}
