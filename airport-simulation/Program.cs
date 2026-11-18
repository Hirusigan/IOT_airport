using AirportSimulation;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient<ISimulationBackendClient, SimulationBackendClient>();
builder.Services.AddHttpClient<IDeviceCommandClient, DeviceCommandClient>();
builder.Services.AddSingleton<IScenarioRunner, ScenarioRunner>();

var app = builder.Build();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/simulation/scenarios", (IScenarioRunner runner) => runner.Scenarios);
app.MapGet("/simulation/status", (IScenarioRunner runner) => runner.Status);
app.MapPost("/simulation/start", async (StartRequest request, IScenarioRunner runner) =>
{
    try
    {
        await runner.Start(request.ScenarioId);
        return Results.Accepted("/simulation/status", runner.Status);
    }
    catch (ArgumentException e) { return Results.BadRequest(new { error = e.Message }); }
    catch (InvalidOperationException e) { return Results.Conflict(new { error = e.Message }); }
});
app.MapPost("/simulation/stop", async (IScenarioRunner runner) =>
{
    await runner.Stop();
    return Results.Ok(runner.Status);
});
app.Run();
