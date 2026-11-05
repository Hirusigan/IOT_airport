namespace Luggage;
public class HealthRepository(Store db) : IHealthRepository
{
    public Task<bool> CanConnectAsync() => db.Database.CanConnectAsync();
}
