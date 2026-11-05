namespace Luggage;
public interface IDevtoolsService
{
    IReadOnlyList<ScenarioDTO> Scenarios { get; }
    SimulationStatusDTO Status { get; }
    Task Start(string id);
    Task Stop();
    Task Clear();
    Task Seed();
}
