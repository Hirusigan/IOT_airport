using Microsoft.EntityFrameworkCore;
namespace Luggage;
public class DatabaseInitializer(Store db, DatabaseSeeder seeder)
{
    public async Task InitializeAsync()
    {
        await db.Database.MigrateAsync();
        await seeder.SeedAsync();
    }
}
