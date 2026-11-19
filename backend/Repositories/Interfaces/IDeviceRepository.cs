namespace Luggage;

public interface IDeviceRepository : IRepository<Device>
{
    Task<Device?> GetAsync(string airportId, string id);
}
