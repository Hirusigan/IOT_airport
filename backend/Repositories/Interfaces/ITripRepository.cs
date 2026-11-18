namespace Luggage;

public interface ITripRepository : IRepository<Trip> { Task<Trip?> GetAsync(string id); }
