namespace Luggage;
public class DatabaseSeeder(IAirportRepository airports, IDeviceRepository devices, IUnitOfWork work) : IDatabaseSeeder
{
    public async Task SeedAsync()
    {
        var defaults = new[] {
            new Airport { Id = "AAR", Name = "Aarhus Airport", Latitude = 56.300, Longitude = 10.619 },
            new Airport { Id = "CPH", Name = "Copenhagen Airport", Latitude = 55.618, Longitude = 12.656 }
        };
        foreach (var airport in defaults)
            if (await airports.GetAsync(airport.Id) == null) airports.Add(airport);
        foreach (var id in new[] { "AAR", "CPH" })
        {
            if (await devices.GetAsync(id, "scanner-001") == null)
                devices.Add(new Device { Id = "scanner-001", AirportId = id, Type = "scanner" });
            if (await devices.GetAsync(id, "gate-01") == null)
                devices.Add(new Device { Id = "gate-01", AirportId = id, Type = "gate" });
        }
        await work.SaveChangesAsync();
    }
}
