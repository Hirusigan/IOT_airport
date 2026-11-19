namespace Luggage;

public class LuggageStateRepository(TrackingDbContext db) : Repository<LuggageState>(db), ILuggageStateRepository
{
    public Task<LuggageState?> GetAsync(string journeyId) => GetByIdAsync(journeyId);
    public Task<IReadOnlyList<LuggageState>> GetByJourneysAsync(string[] journeyIds) => ListAsync(x => journeyIds.Contains(x.JourneyId));
}
