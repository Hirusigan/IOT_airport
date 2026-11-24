namespace Luggage;

public class DevtoolsService(
    ISimulationRunner runner,
    IDevtoolsRepository repository,
    TrackingDbContext db,
    IRepository<Airport> airports,
    IAirportBrokerProvisioner brokers,
    IDashboardNotifier notifications) : IDevtoolsService
{
    public Task<IReadOnlyList<ScenarioDTO>> GetScenarios() => runner.GetScenarios();

    public SimulationStatusDTO Status => runner.Status;

    public Task Start(string id) => runner.Start(id);

    public Task Stop() => runner.Stop();

    public async Task Clear()
    {
        await runner.Stop();
        await repository.ClearAsync();
        try
        {
            await brokers.RemoveProvisionedBrokers();
        }
        catch (Exception ex)
        {
            throw new BrokerCleanupException(ex);
        }
        finally
        {
            await notifications.Send();
        }
    }

    public async Task Seed()
    {
        await using var transaction = await airports.BeginTransactionAsync();
        await DatabaseSeeder.SeedAsync(db);
        await transaction.CommitAsync();
        await notifications.Send();
    }
}
