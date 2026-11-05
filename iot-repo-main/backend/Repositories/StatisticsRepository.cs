using Microsoft.EntityFrameworkCore;
namespace Luggage;
public class StatisticsRepository(Store db) : IStatisticsRepository
{
    public async Task<StatisticsDTO> GetAsync() => new(await db.Airports.CountAsync(), await db.Flights.CountAsync(),
        await db.Journeys.CountAsync(), await db.Events.CountAsync(), await db.Assignments.CountAsync(x => x.LoadedAt != null),
        await db.Alerts.CountAsync(x => x.Active), await db.Alerts.CountAsync(x => !x.Active), await db.Diagnostics.CountAsync());
}
