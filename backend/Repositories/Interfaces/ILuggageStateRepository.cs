namespace Luggage;
public interface ILuggageStateRepository : IRepository<LuggageState>
{
    Task<LuggageState?> GetAsync(string journeyId);
    Task<IReadOnlyList<LuggageState>> GetByJourneysAsync(string[] journeyIds);
}
