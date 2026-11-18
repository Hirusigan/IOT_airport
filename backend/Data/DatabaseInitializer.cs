using Microsoft.EntityFrameworkCore;
namespace Luggage;

public class DatabaseInitializer(TrackingDbContext db)
{
    public async Task InitializeAsync()
    {
        await db.Database.MigrateAsync();
        await DatabaseSeeder.SeedAsync(db);
    }
}
