namespace Luggage;

public interface IEventRepository : IRepository<Observation>
{
    Task<bool> ExistsAsync(string eventId);
    Task<bool> IsRetiredRunAsync(string runId);
    Task<IReadOnlyList<Observation>> GetHistoryAsync(string[] journeyIds);
}
