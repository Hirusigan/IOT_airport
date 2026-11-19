namespace Luggage;

public interface IRuleService
{
    Task<bool> Set(AlertType type, Assignment assignment, string airport, AlertSeverity severity, bool active, DateTimeOffset now);
    Task<bool> Evaluate(DateTimeOffset now);
}
