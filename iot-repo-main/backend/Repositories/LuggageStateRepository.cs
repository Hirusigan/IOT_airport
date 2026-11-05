namespace Luggage;
public class LuggageStateRepository(Store db) : BaseRepository<LuggageState>(db), ILuggageStateRepository
{
    public Task<LuggageState?> GetAsync(string journeyId) => FindAsync(journeyId);
    public Task<IReadOnlyList<LuggageState>> GetByJourneysAsync(string[] journeyIds) => ListAsync(x => journeyIds.Contains(x.JourneyId));
}
