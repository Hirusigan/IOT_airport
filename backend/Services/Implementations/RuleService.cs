namespace Luggage;

public class RuleService(IAlertRepository alerts, IFlightRepository flights, IAssignmentRepository assignments, RuleSettings settings) : IRuleService
{
    public async Task<bool> Set(AlertType type, Assignment assignment, string airport, AlertSeverity severity, bool active, DateTimeOffset now)
    {
        var alert = await alerts.GetAsync(type, assignment.JourneyId, assignment.FlightId);
        if (alert == null && active)
        {
            alerts.Add(new Alert
            {
                Rule = type,
                JourneyId = assignment.JourneyId,
                FlightId = assignment.FlightId,
                AirportId = airport,
                Severity = severity,
                CreatedAt = now
            });
            return true;
        }
        else if (alert != null && alert.Active != active)
        {
            alert.Active = active;
            alert.ResolvedAt = active ? null : now;
            return true;
        }
        return false;
    }
    public async Task<bool> Evaluate(DateTimeOffset now)
    {
        var changed = false;
        var flightById = (await flights.GetAllAsync()).ToDictionary(x => x.Id);

        foreach (var assignment in await assignments.GetAllAsync())
        {
            var flight = flightById[assignment.FlightId];
            changed |= await Set(AlertType.AtRisk, assignment, flight.Origin, AlertSeverity.Orange, RuleDecisions.AtRisk(assignment, flight, now, settings), now);
            changed |= await Set(AlertType.MissedFlight, assignment, flight.Origin, AlertSeverity.Red, RuleDecisions.Missed(assignment, flight, now, settings), now);
        }
        return changed;
    }
}
