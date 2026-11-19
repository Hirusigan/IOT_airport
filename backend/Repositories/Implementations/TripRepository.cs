namespace Luggage;

public class TripRepository(TrackingDbContext db) : Repository<Trip>(db), ITripRepository
{
    public Task<Trip?> GetAsync(string id) => GetByIdAsync(id);
}
