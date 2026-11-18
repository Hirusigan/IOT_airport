namespace Luggage;
public class TripRepository(TrackingDbContext db) : BaseRepository<Trip>(db), ITripRepository { }
