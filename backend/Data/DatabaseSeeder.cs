using Microsoft.EntityFrameworkCore;
namespace Luggage;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(TrackingDbContext db)
    {
        var defaults = new[] {
            new Airport { Id = "AAR", Name = "Aarhus Airport", Latitude = 56.300, Longitude = 10.619 },
            new Airport { Id = "CPH", Name = "Copenhagen Airport", Latitude = 55.618, Longitude = 12.656 }
        };
        foreach (var airport in defaults)
            if (!await db.Airports.AnyAsync(x => x.Id == airport.Id)) db.Airports.Add(airport);
        foreach (var id in new[] { "AAR", "CPH" })
        {
            if (!await db.Devices.AnyAsync(x => x.AirportId == id && x.Id == "scanner-001"))
                db.Devices.Add(new Device { Id = "scanner-001", AirportId = id, Type = "scanner" });
            if (!await db.Devices.AnyAsync(x => x.AirportId == id && x.Id == "gate-01"))
                db.Devices.Add(new Device { Id = "gate-01", AirportId = id, Type = "gate" });
        }
        await db.SaveChangesAsync();
    }
}
