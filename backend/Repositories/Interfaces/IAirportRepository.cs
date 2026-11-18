namespace Luggage;

public interface IAirportRepository : IRepository<Airport>
{
    Task<Airport?> GetAsync(string id);
    Task<IReadOnlyList<Airport>> GetEnabledAsync();
    Task<bool> IsEnabledAsync(string id);
    Task<bool> AnyAsync();
}
