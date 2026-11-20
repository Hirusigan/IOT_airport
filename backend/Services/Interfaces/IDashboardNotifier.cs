namespace Luggage;

public interface IDashboardNotifier
{
    Task Send(string? journeyId = null);
    Task SendSimulationStatus(SimulationStatusDTO status);
}
