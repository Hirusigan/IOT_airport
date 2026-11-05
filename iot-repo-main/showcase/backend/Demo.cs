using System.Diagnostics;

namespace Showcase;

public record DemoStatus(string Status, string? DemoId = null, string? Error = null);

public interface IDemoPublisher
{
    Task Publish(string demoId, CancellationToken token);
}

public class DemoPublisher(IConfiguration config, ILogger<DemoPublisher> log) : IDemoPublisher
{
    public async Task Publish(string demoId, CancellationToken token)
    {
        var start = new ProcessStartInfo(config["Publisher:Path"] ?? "/app/publisher")
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true
        };
        start.ArgumentList.Add(config["Mqtt:Host"] ?? "localhost");
        start.ArgumentList.Add(config["Mqtt:Port"] ?? "1883");
        start.ArgumentList.Add(demoId);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Publisher did not start");
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        try { await process.WaitForExitAsync(token); }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            throw;
        }
        log.LogInformation("Publisher: {Output}", await output);
        var error = await errors;
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Publisher exited with code {process.ExitCode}: {error.Trim()}");
    }
}

public class DemoController(IDemoPublisher publisher, ILogger<DemoController> log) : IHostedService
{
    private readonly object gate = new();
    private readonly CancellationTokenSource shutdown = new();
    private DemoStatus status = new("idle");
    private Task? running;
    private bool clearing;

    public DemoStatus Status { get { lock (gate) return status; } }

    public DemoStatus? Start()
    {
        lock (gate)
        {
            if (status.Status == "running" || clearing || shutdown.IsCancellationRequested) return null;
            var id = Guid.NewGuid().ToString("N");
            status = new("running", id);
            running = Task.Run(() => Run(id));
            return status;
        }
    }

    public async Task<bool> Clear(Func<Task> clear)
    {
        lock (gate)
        {
            if (status.Status == "running" || clearing) return false;
            clearing = true;
        }
        try
        {
            await clear();
            lock (gate) status = new("idle");
            return true;
        }
        finally { lock (gate) clearing = false; }
    }

    private async Task Run(string id)
    {
        try
        {
            await publisher.Publish(id, shutdown.Token);
            lock (gate) status = new("completed", id);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Demo failed or was cancelled");
            lock (gate) status = new("failed", id,
                shutdown.IsCancellationRequested ? "Demo cancelled by backend shutdown" : ex.Message);
        }
    }

    public Task StartAsync(CancellationToken token) => Task.CompletedTask;
    public async Task StopAsync(CancellationToken token)
    {
        await shutdown.CancelAsync();
        Task? task;
        lock (gate) task = running;
        if (task != null) await task.WaitAsync(token);
    }
}
