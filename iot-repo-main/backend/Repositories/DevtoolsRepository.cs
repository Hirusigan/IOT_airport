using Microsoft.EntityFrameworkCore;
namespace Luggage;
public class DevtoolsRepository(TrackingDbContext db) : IDevtoolsRepository
{
    public async Task ClearAsync()
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        // Tombstones survive resets so queued MQTT messages cannot resurrect cleared runs.
        await db.Database.ExecuteSqlRawAsync("INSERT INTO \"RetiredRuns\" (\"Id\") SELECT DISTINCT \"RunId\" FROM \"Flights\" ON CONFLICT DO NOTHING");
        await db.Database.ExecuteSqlRawAsync("TRUNCATE \"Alerts\", \"States\", \"Events\", \"Assignments\", \"Journeys\", \"Trips\", \"Flights\", \"Devices\", \"Airports\", \"Diagnostics\" RESTART IDENTITY");
        await transaction.CommitAsync();
    }
}
