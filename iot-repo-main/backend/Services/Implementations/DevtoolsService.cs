namespace Luggage;
public class DevtoolsService(ISimulationRunner runner, IDevtoolsRepository repository,
    TrackingDbContext db,
    DevtoolsGate operations, ProcessingGate processing, IDashboardNotifier notifications) : IDevtoolsService
{
    public IReadOnlyList<ScenarioDTO> Scenarios => runner.Scenarios;
    public SimulationStatusDTO Status => runner.Status;
    public async Task Start(string id)
    {
        await operations.Mutex.WaitAsync();
        try { await runner.Start(id); } finally { operations.Mutex.Release(); }
    }
    public async Task Stop()
    {
        await operations.Mutex.WaitAsync();
        try { await runner.Stop(); } finally { operations.Mutex.Release(); }
    }
    public async Task Clear()
    {
        await operations.Mutex.WaitAsync();
        try
        {
            await runner.Stop();
            await processing.Mutex.WaitAsync();
            try { await repository.ClearAsync(); } finally { processing.Mutex.Release(); }
        }
        finally { operations.Mutex.Release(); }
        await notifications.Send();
    }
    public async Task Seed()
    {
        await operations.Mutex.WaitAsync();
        try
        {
            await processing.Mutex.WaitAsync();
            try { await DatabaseSeeder.SeedAsync(db); } finally { processing.Mutex.Release(); }
        }
        finally { operations.Mutex.Release(); }
        await notifications.Send();
    }
}
