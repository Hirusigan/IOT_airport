namespace Luggage;

public interface IFlightRepository : IRepository<Flight>
{
    Task<Flight?> GetAsync(string id);
    Task<bool> ExistsInRunAsync(string id, string runId);
    Task<bool> HasRunAsync(string runId);
    Task<IReadOnlyList<Flight>> GetByAirportAsync(string airportId);
    Task<IReadOnlyList<Flight>> GetByIdsAsync(string[] ids);
}
