using System.Diagnostics;
using System.Text.Json;
using System.Threading.Channels;
namespace Luggage;

public sealed class SimulationRunner(IServiceScopeFactory scopes, IConfiguration config, ILogger<SimulationRunner> log) : BackgroundService, ISimulationRunner
{
    public IReadOnlyList<ScenarioDTO> Scenarios { get; } = [
        new("mixed", "All scenarios", "Normal arrival, missing loading, wrong flight and unexpected airport."),
        new("normal", "Normal journey", "Check-in, correct loading, departure and arrival."),
        new("transfer", "Connecting flights", "BAG-TRANSFER flies AAR → CPH → AAR with a luggage transfer at Copenhagen."),
        new("missed", "Missed flight", "A bag is never loaded: warning followed by a missed-flight alert."),
        new("wrong-flight", "Wrong flight", "A bag is loaded onto the wrong flight."),
        new("unexpected-airport", "Unexpected airport", "A bag is scanned at the wrong airport.")];
    private readonly object sync = new();
    private readonly Channel<string> requests = Channel.CreateUnbounded<string>();
    private SimulationStatusDTO status = new("Idle");
    private CancellationTokenSource? cancellation;
    private TaskCompletionSource? finished;
    private string DirectoryPath => config["Simulation:RunsDirectory"] ?? "/runs";
    public SimulationStatusDTO Status { get { lock (sync) return status; } }
    private void Update(SimulationStatusDTO value)
    {
        lock (sync) status = value;
        try
        {
            var path = Path.Combine(DirectoryPath, "status.json");
            File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(value));
            File.Move(path + ".tmp", path, true);
        }
        catch (IOException ex) { log.LogWarning(ex, "Could not persist simulation status"); }
    }
    public Task Start(string scenarioId)
    {
        if (!Scenarios.Any(x => x.Id == scenarioId)) throw new ArgumentException("Unknown scenario");
        lock (sync)
        {
            if (finished != null && !finished.Task.IsCompleted) throw new ConflictException("A simulation is already running");
            cancellation = new CancellationTokenSource();
            finished = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Update(new("Starting", scenarioId, StartedAt: DateTimeOffset.UtcNow));
            requests.Writer.TryWrite(scenarioId);
        }
        return Task.CompletedTask;
    }
    public async Task Stop()
    {
        Task? pending;
        lock (sync) { cancellation?.Cancel(); pending = finished?.Task; }
        if (pending != null) await pending;
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Directory.CreateDirectory(DirectoryPath);
        var savedPath = Path.Combine(DirectoryPath, "status.json");
        if (File.Exists(savedPath))
        {
            try
            {
                var saved = JsonSerializer.Deserialize<SimulationStatusDTO>(File.ReadAllText(savedPath));
                if (saved != null) Update(saved.State is "Starting" or "Running" ? saved with { State = "Interrupted", Error = "Backend restarted during simulation." } : saved);
            }
            catch (JsonException ex) { log.LogWarning(ex, "Could not read previous simulation status"); }
        }
        try
        {
            await foreach (var scenario in requests.Reader.ReadAllAsync(stoppingToken))
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, cancellation!.Token);
                try { await Run(scenario, linked.Token); }
                catch (OperationCanceledException) { Update(Status with { State = "Stopped" }); }
                catch (Exception ex) { log.LogError(ex, "Simulation failed"); Update(Status with { State = "Failed", Error = ex.Message }); }
                finally { lock (sync) { cancellation?.Dispose(); cancellation = null; finished!.TrySetResult(); } }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
    private async Task Run(string scenario, CancellationToken token)
    {
        var path = Path.Combine(DirectoryPath, $"{Guid.NewGuid()}.json");
        var scenarioPath = Path.Combine(config["Simulation:ScenariosDirectory"] ?? "/app/scenarios", $"{scenario}.json");
        await Execute(["--prepare", scenarioPath, path], token);
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(path, token));
        var manifest = JsonContract.Read<RunImport>(document.RootElement.GetProperty("manifest").GetRawText());
        token.ThrowIfCancellationRequested();
        using (var scope = scopes.CreateScope()) await scope.ServiceProvider.GetRequiredService<IRunImportService>().Import(manifest);
        token.ThrowIfCancellationRequested();
        Update(Status with { State = "Running", RunId = manifest.SimulationRunId, TotalEvents = document.RootElement.GetProperty("events").GetArrayLength() });
        await Execute(["--publish", path], token, true);
        Update(Status with { State = "Completed" });
    }
    private async Task Execute(string[] arguments, CancellationToken token, bool publishing = false)
    {
        var start = new ProcessStartInfo(config["Simulation:Executable"] ?? "/app/simulation") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        start.Environment["MQTT_PASSWORD"] = config["Simulation:Password"] ?? throw new InvalidOperationException("Simulation credentials are missing");
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start simulation");
        var errors = process.StandardError.ReadToEndAsync(token);
        try
        {
            while (await process.StandardOutput.ReadLineAsync(token) is { } line)
                if (publishing && line.StartsWith("Published ")) Update(Status with { PublishedEvents = Status.PublishedEvents + 1 });
            await process.WaitForExitAsync(token);
            var error = await errors;
            if (process.ExitCode != 0) throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? "Simulator exited unsuccessfully" : error.Trim());
        }
        finally
        {
            if (!process.HasExited) { process.Kill(true); await process.WaitForExitAsync(); }
        }
    }
}
