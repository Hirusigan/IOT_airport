using System.Text.Json;

namespace Luggage;

public sealed class SimulationRunner(
    IHttpClientFactory clients,
    IConfiguration config) : BackgroundService, ISimulationRunner
{
    private const string StatusPath = "/simulation/status";
    private const string InterruptedError = "Backend restarted during simulation.";
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan RecoveryTimeout = TimeSpan.FromSeconds(5);

    private SimulationStatusDTO status = new(SimulationState.Idle);
    private CancellationTokenSource? pollingCancellation;
    private Task? polling;
    private CancellationToken stoppingToken;

    private string RunsDirectory => config["Simulation:RunsDirectory"] ?? "/runs";
    private string Url => config["AirportSimulation:Url"] ?? "http://airport-simulation:8080";

    public SimulationStatusDTO Status => status;

    public async Task<IReadOnlyList<ScenarioDTO>> GetScenarios()
    {
        using var client = CreateClient();
        using var response = await client.GetAsync("/simulation/scenarios");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ScenarioDTO[]>() ?? [];
    }

    public async Task Start(string scenarioId)
    {
        if (polling is { IsCompleted: false })
        {
            throw new ConflictException("A simulation is already running");
        }

        using var client = CreateClient();
        var response = await client.PostAsJsonAsync("/simulation/start", new { scenarioId });
        if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            throw new ConflictException("A simulation is already running");
        }

        if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            throw new ArgumentException(await response.Content.ReadAsStringAsync());
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(await response.Content.ReadAsStringAsync());
        }

        var remote = await response.Content.ReadFromJsonAsync<SimulationStatusDTO>()
            ?? new SimulationStatusDTO(SimulationState.Starting, scenarioId);
        Update(remote);
        StartPolling(stoppingToken);
    }

    public async Task Stop()
    {
        using var client = CreateClient();
        using var response = await client.PostAsync("/simulation/stop", null);
        response.EnsureSuccessStatusCode();

        var remote = await response.Content.ReadFromJsonAsync<SimulationStatusDTO>();
        if (remote != null)
        {
            Update(remote);
        }

        pollingCancellation?.Cancel();
        if (polling != null)
        {
            await polling;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        this.stoppingToken = stoppingToken;
        await RestoreStatus(stoppingToken);
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        finally
        {
            pollingCancellation?.Cancel();
            if (polling != null)
            {
                await polling;
            }

            pollingCancellation?.Dispose();
        }
    }

    private HttpClient CreateClient()
    {
        var client = clients.CreateClient();
        client.BaseAddress = new Uri(Url);
        return client;
    }

    private void Update(SimulationStatusDTO value)
    {
        status = value;
        try
        {
            Directory.CreateDirectory(RunsDirectory);
            var path = Path.Combine(RunsDirectory, "status.json");
            File.WriteAllText(path, JsonSerializer.Serialize(value));
        }
        catch (IOException)
        {
            // Persisted status is only used to recover the display state after restart.
        }
    }

    private void StartPolling(CancellationToken stoppingToken)
    {
        pollingCancellation?.Dispose();
        pollingCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        polling = Poll(pollingCancellation.Token);
    }

    private async Task Poll(CancellationToken token)
    {
        using var client = CreateClient();
        try
        {
            while (!token.IsCancellationRequested)
            {
                var remote = await client.GetFromJsonAsync<SimulationStatusDTO>(StatusPath, token);
                if (remote != null)
                {
                    Update(remote);
                    if (IsFinished(remote.State))
                    {
                        return;
                    }
                }

                await Task.Delay(PollInterval, token);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Update(Status with
            {
                State = SimulationState.Failed,
                Error = exception.Message
            });
        }
    }

    private async Task RestoreStatus(CancellationToken stoppingToken)
    {
        var saved = LoadStatus();
        if (saved == null)
        {
            return;
        }

        if (!IsActive(saved.State))
        {
            Update(saved);
            return;
        }

        try
        {
            using var client = CreateClient();
            client.Timeout = RecoveryTimeout;
            var remote = await client.GetFromJsonAsync<SimulationStatusDTO>(StatusPath, stoppingToken);
            if (remote != null && IsActive(remote.State))
            {
                Update(remote);
                StartPolling(stoppingToken);
                return;
            }
        }
        catch (Exception) when (!stoppingToken.IsCancellationRequested)
        {
            // If the remote simulation cannot be queried, mark the saved run interrupted below.
        }

        Update(saved with
        {
            State = SimulationState.Interrupted,
            Error = InterruptedError
        });
    }

    private SimulationStatusDTO? LoadStatus()
    {
        var path = Path.Combine(RunsDirectory, "status.json");
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<SimulationStatusDTO>(File.ReadAllText(path));
        }
        catch (IOException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool IsActive(SimulationState state) =>
        state is SimulationState.Starting or SimulationState.Running;

    private static bool IsFinished(SimulationState state) =>
        state is SimulationState.Completed or SimulationState.Stopped or SimulationState.Failed or SimulationState.Idle;
}
