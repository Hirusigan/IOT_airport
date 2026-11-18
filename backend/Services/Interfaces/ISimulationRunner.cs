namespace Luggage;

public interface ISimulationRunner
{
    Task<IReadOnlyList<ScenarioDTO>> GetScenarios();
    SimulationStatusDTO Status { get; }
    Task Start(string scenarioId);
    Task Stop();
}
