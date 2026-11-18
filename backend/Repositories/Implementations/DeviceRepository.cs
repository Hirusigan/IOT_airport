namespace Luggage;

public class DeviceRepository(TrackingDbContext db) : BaseRepository<Device>(db), IDeviceRepository
{
    public Task<Device?> GetAsync(string airportId, string id) => FindAsync(airportId, id);
}
