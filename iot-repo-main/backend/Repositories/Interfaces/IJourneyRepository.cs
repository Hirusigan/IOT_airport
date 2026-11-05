namespace Luggage;
public interface IJourneyRepository : IRepository<Journey>
{
    Task<Journey?> GetAsync(string id);
    Task<Journey?> GetPreviousLegAsync(string tripId, int legOrder);
    Task<IReadOnlyList<Journey>> GetByLuggageAsync(string luggageId);
}
