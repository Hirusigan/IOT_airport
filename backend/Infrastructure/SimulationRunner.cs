using System.Text.Json;
namespace Luggage;

public sealed class SimulationRunner(IHttpClientFactory clients, IConfiguration config, ILogger<SimulationRunner> log) : BackgroundService, ISimulationRunner
{
    readonly object sync = new();
    SimulationStatusDTO status = new("Idle");
    CancellationTokenSource? cancellation;
    Task? polling;
    string RunsDirectory => config["Simulation:RunsDirectory"] ?? "/runs";
    string Url => config["AirportSimulation:Url"] ?? "http://airport-simulation:8080";
    public SimulationStatusDTO Status { get { lock (sync) return status; } }
    public async Task<IReadOnlyList<ScenarioDTO>> GetScenarios()
    {
        using var client = clients.CreateClient();
        client.BaseAddress = new Uri(Url);
        using var response = await client.GetAsync("/simulation/scenarios");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ScenarioDTO[]>() ?? [];
    }
    void Update(SimulationStatusDTO value)
    {
        lock (sync) status = value;
        try { Directory.CreateDirectory(RunsDirectory); File.WriteAllText(Path.Combine(RunsDirectory, "status.json"), JsonSerializer.Serialize(value)); }
        catch (IOException ex) { log.LogWarning(ex, "Could not persist simulation status"); }
    }
    public async Task Start(string scenarioId)
    {
        lock (sync) if (polling is { IsCompleted: false }) throw new ConflictException("A simulation is already running");
        using var client = clients.CreateClient();
        client.BaseAddress = new Uri(Url);
        var response = await client.PostAsJsonAsync("/simulation/start", new { scenarioId });
        if (response.StatusCode == System.Net.HttpStatusCode.Conflict) throw new ConflictException("A simulation is already running");
        if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            throw new ArgumentException(await response.Content.ReadAsStringAsync());
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
