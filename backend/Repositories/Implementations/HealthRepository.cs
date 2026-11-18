namespace Luggage;

public class HealthRepository(TrackingDbContext db) : IHealthRepository
{
    public Task<bool> CanConnectAsync() => db.Database.CanConnectAsync();
}
