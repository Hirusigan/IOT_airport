namespace Luggage;
public class TripRepository(TrackingDbContext db) : BaseRepository<Trip>(db), ITripRepository
{
    public Task<Trip?> GetAsync(string id) => FindAsync(id);
}
