using Microsoft.EntityFrameworkCore;
namespace Luggage;

public class AirportRepository(TrackingDbContext db) : BaseRepository<Airport>(db), IAirportRepository
{
    public Task<Airport?> GetAsync(string id) => FindAsync(id);
    public async Task<IReadOnlyList<Airport>> GetEnabledAsync() => await Set.AsNoTracking().Where(x => x.Enabled).OrderBy(x => x.Id).ToListAsync();
    public Task<bool> IsEnabledAsync(string id) => ExistsAsync(x => x.Id == id && x.Enabled);
    public Task<bool> AnyAsync() => Set.AnyAsync();
}
