namespace Luggage;

public interface IDiagnosticService
{
    Task Record(string topic, string reason);
}
