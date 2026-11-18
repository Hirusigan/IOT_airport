namespace Luggage;

public interface IAlertRepository : IRepository<Alert>
{
    Task<Alert?> GetAsync(AlertType type, string journeyId, string flightId);
    Task<IReadOnlyList<Alert>> GetFilteredAsync(string? airportId = null, bool? active = null, AlertSeverity? severity = null);
}
