namespace AirportSimulation;

public interface IScenarioRunner
{
    IReadOnlyList<ScenarioInfo> Scenarios
    {
        get;
    }
    SimStatus Status
    {
        get;
    }
    Task Start(string scenarioId);
    Task Stop();
}
