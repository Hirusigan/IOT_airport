namespace Luggage;

public class JourneyRepository(TrackingDbContext db) : BaseRepository<Journey>(db), IJourneyRepository
{
    public Task<Journey?> GetAsync(string id) => FindAsync(id);
    public async Task<Journey?> GetPreviousLegAsync(string tripId, int legOrder) =>
        (await ListAsync(x => x.TripId == tripId && x.LegOrder == legOrder - 1)).SingleOrDefault();
    public Task<IReadOnlyList<Journey>> GetByLuggageAsync(string luggageId) => ListAsync(x => x.LuggageId == luggageId);
}
