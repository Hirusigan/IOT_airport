namespace Luggage;

public class DeviceRepository(TrackingDbContext db) : Repository<Device>(db), IDeviceRepository
{
    public Task<Device?> GetAsync(string airportId, string id) => GetByIdAsync(airportId, id);
}
