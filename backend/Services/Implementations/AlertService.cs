namespace Luggage;

public class AlertService(IAlertRepository alerts) : IAlertService
{
    public async Task<IReadOnlyList<AlertDTO>> Get(string? airportId, bool? active, string? severity)
    {
        // Existing API returns no matches for an unrecognized severity filter.
        if (severity != null && severity is not ("red" or "orange")) return [];
        AlertSeverity? parsed = severity == null ? null : AlertValues.ParseSeverity(severity);
        return (await alerts.GetFilteredAsync(airportId, active, parsed)).Select(AlertDTO.From).ToList();
    }
}
