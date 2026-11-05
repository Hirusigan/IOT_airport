namespace Luggage;
public interface ISimulationRunner
{
    IReadOnlyList<ScenarioDTO> Scenarios { get; }
    SimulationStatusDTO Status { get; }
    Task Start(string scenarioId);
    Task Stop();
}
