using Microsoft.EntityFrameworkCore;
namespace Luggage;

public class FlightRepository(TrackingDbContext db) : Repository<Flight>(db), IFlightRepository
{
    public Task<Flight?> GetAsync(string id) => GetByIdAsync(id);
    public Task<bool> ExistsInRunAsync(string id, string runId) => ExistsAsync(x => x.Id == id && x.RunId == runId);
    public Task<bool> HasRunAsync(string runId) => ExistsAsync(x => x.RunId == runId);
    public async Task<IReadOnlyList<Flight>> GetByAirportAsync(string airportId) => await Set.AsNoTracking()
        .Where(x => x.Origin == airportId || x.Destination == airportId).OrderByDescending(x => x.ScheduledDeparture).ToListAsync();
    public Task<IReadOnlyList<Flight>> GetByIdsAsync(string[] ids) => ListAsync(x => ids.Contains(x.Id));
}
