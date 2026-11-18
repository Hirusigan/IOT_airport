namespace Luggage;

public class DeadlineTimer(IServiceScopeFactory scopes, ILogger<DeadlineTimer> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            while (await timer.WaitForNextTickAsync(token))
            {
                using var scope = scopes.CreateScope();
                try { await scope.ServiceProvider.GetRequiredService<IDeadlineService>().Evaluate(); }
                catch (Exception ex) { log.LogError(ex, "Deadline evaluation failed; retry next tick"); }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }
}
