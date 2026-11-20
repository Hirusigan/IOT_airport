namespace Luggage;

public interface IDevtoolsService
{
    Task<IReadOnlyList<ScenarioDTO>> GetScenarios();
    SimulationStatusDTO Status
    {
        get;
    }
    Task Start(string id);
    Task Stop();
    Task Clear();
    Task Seed();
}
