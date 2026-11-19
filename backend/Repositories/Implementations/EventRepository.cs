using Microsoft.EntityFrameworkCore;
namespace Luggage;

public class EventRepository : Repository<Observation>, IEventRepository
{
    private readonly TrackingDbContext db;
    public EventRepository(TrackingDbContext db) : base(db) { this.db = db; }
    public Task<bool> ExistsAsync(string eventId) => ExistsAsync(x => x.EventId == eventId);
    public Task<bool> IsRetiredRunAsync(string runId) => db.RetiredRuns.AnyAsync(x => x.Id == runId);
    public async Task<IReadOnlyList<Observation>> GetHistoryAsync(string[] journeyIds) => await Set.AsNoTracking()
        .Where(x => journeyIds.Contains(x.JourneyId!)).OrderBy(x => x.OccurredAt).ThenBy(x => x.ReceivedAt).ToListAsync();
}
