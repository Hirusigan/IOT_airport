namespace Luggage;
public class AirportService(IAirportRepository airports, IAlertRepository alerts, IFlightRepository flights, IUnitOfWork work,
    BrokerHealth health, RuleSettings settings, ProcessingGate gate, IDashboardNotifier notifications, TimeProvider clock) : IAirportService
{
    private async Task<AirportDTO> View(Airport airport)
    {
        var activeAlerts = await alerts.GetFilteredAsync(airport.Id, true);
        var status = activeAlerts.Any(x => x.Severity == AlertSeverity.Red) ? "red" : activeAlerts.Count > 0 ? "orange" : "green";
        var telemetry = !health.Connected ? "disconnected" : airport.LastSeenAt == null ? "unknown" :
            clock.GetUtcNow() - airport.LastSeenAt > TimeSpan.FromSeconds(settings.StaleSeconds) ? "stale" : "healthy";
        return new(airport.Id, airport.Name, airport.Latitude, airport.Longitude, airport.RegionId, airport.LastSeenAt, status, activeAlerts.Count, telemetry);
    }
    public async Task<IReadOnlyList<AirportDTO>> GetAll()
    {
        var result = new List<AirportDTO>();
        foreach (var airport in await airports.GetEnabledAsync()) result.Add(await View(airport));
        return result;
    }
    public async Task<AirportDetailsDTO?> GetDetails(string id)
    {
        var airport = await airports.GetAsync(id);
        return airport == null ? null : new(await View(airport), await flights.GetByAirportAsync(id),
            (await alerts.GetFilteredAsync(id)).Select(AlertDTO.From).ToList());
    }
    public async Task<AirportDTO> Register(Airport airport)
    {
        if (!AirportValidation.IsValid(airport) || airport.RegionId != "EU" || !airport.Enabled || airport.LastSeenAt != null)
            throw new ArgumentException("Valid metadata in enabled EU region required");
        await gate.Mutex.WaitAsync();
        try
        {
            if (await airports.GetAsync(airport.Id) != null) throw new ConflictException("Airport already exists");
            airports.Add(airport); await work.SaveChangesAsync();
        }
        finally { gate.Mutex.Release(); }
        await notifications.Send();
        return await View(airport);
    }
}
