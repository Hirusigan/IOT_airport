using Microsoft.EntityFrameworkCore;
namespace Luggage;

public class AlertRepository(TrackingDbContext db) : Repository<Alert>(db), IAlertRepository
{
    public async Task<Alert?> GetAsync(AlertType type, string journeyId, string flightId) =>
        Set.Local.FirstOrDefault(x => x.Rule == type && x.JourneyId == journeyId && x.FlightId == flightId)
        ?? await Set.SingleOrDefaultAsync(x => x.Rule == type && x.JourneyId == journeyId && x.FlightId == flightId);
    public async Task<IReadOnlyList<Alert>> GetFilteredAsync(string? airportId = null, bool? active = null, AlertSeverity? severity = null)
    {
        var query = Set.AsNoTracking().AsQueryable();
        if (airportId != null)
            query = query.Where(x => x.AirportId == airportId);
        if (active != null)
            query = query.Where(x => x.Active == active);
        if (severity != null)
            query = query.Where(x => x.Severity == severity);
        return await query.OrderByDescending(x => x.CreatedAt).ToListAsync();
    }
}
