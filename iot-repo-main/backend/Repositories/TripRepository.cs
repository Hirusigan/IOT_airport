namespace Luggage;
public class TripRepository(Store db) : BaseRepository<Trip>(db), ITripRepository { }
