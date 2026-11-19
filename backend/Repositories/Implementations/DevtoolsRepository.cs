namespace Luggage;

public class DevtoolsRepository(
    IRepository<Alert> alerts,
    IRepository<LuggageState> states,
    IRepository<Observation> events,
    IRepository<Assignment> assignments,
    IRepository<Journey> journeys,
    IRepository<Trip> trips,
    IRepository<Flight> flights,
    IRepository<Device> devices,
    IRepository<Airport> airports,
    IRepository<Diagnostic> diagnostics,
    IRepository<RetiredRun> retiredRuns) : IDevtoolsRepository
{
    public async Task ClearAsync()
    {
        await using var transaction = await airports.BeginTransactionAsync();

        var runIds = (await flights.GetAllAsync()).Select(x => x.RunId).Distinct();
        var retiredIds = (await retiredRuns.GetAllAsync()).Select(x => x.Id).ToHashSet();
        foreach (var runId in runIds)
        {
            if (retiredIds.Add(runId))
                retiredRuns.Add(new RetiredRun { Id = runId });
        }

        await DeleteAll(alerts);
        await DeleteAll(states);
        await DeleteAll(events);
        await DeleteAll(assignments);
        await DeleteAll(journeys);
        await DeleteAll(trips);
        await DeleteAll(flights);
        await DeleteAll(devices);
        await DeleteAll(airports);
        await DeleteAll(diagnostics);
        await airports.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    private static async Task DeleteAll<T>(IRepository<T> repository) where T : class
    {
        foreach (var entity in await repository.GetAllAsync())
            repository.Delete(entity);
    }
}
