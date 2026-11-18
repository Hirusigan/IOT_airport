namespace Luggage;
public interface IRuleService
{
    Task Set(AlertType type, Assignment assignment, string airport, AlertSeverity severity, bool active, DateTimeOffset now);
    Task Evaluate(DateTimeOffset now);
}
