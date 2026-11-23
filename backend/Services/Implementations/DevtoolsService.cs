namespace Luggage;

public class DevtoolsService(
    ISimulationRunner runner,
    IDevtoolsRepository repository,
    TrackingDbContext db,
    IRepository<Airport> airports,
    IAirportBrokerProvisioner brokers,
    EnvironmentSetupLock setupLock,
    IDashboardNotifier notifications) : IDevtoolsService
{
    public Task<IReadOnlyList<ScenarioDTO>> GetScenarios() => runner.GetScenarios();

    public SimulationStatusDTO Status => runner.Status;

    public async Task Start(string id)
    {
        using var setup = await setupLock.Acquire();
        await runner.Start(id);
    }

    public async Task Stop()
    {
        using var setup = await setupLock.Acquire();
        await runner.Stop();
    }

    public async Task Clear()
    {
        using var setup = await setupLock.Acquire();
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
        using var setup = await setupLock.Acquire();
        await using var transaction = await airports.BeginTransactionAsync();
        await DatabaseSeeder.SeedAsync(db);
        await transaction.CommitAsync();
        await notifications.Send();
    }
}
