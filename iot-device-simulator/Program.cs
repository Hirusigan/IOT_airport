using IotDeviceSimulator;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<IDeviceSimulator, DeviceSimulator>();
builder.Services.AddSingleton<IMqttEventPublisher, MqttEventPublisher>();

var app = builder.Build();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapPost("/devices/actions", async (DeviceActionCommand command, IDeviceSimulator simulator, CancellationToken token) =>
{
    try
    {
        await simulator.Execute(command, token);
        return Results.Accepted();
    }
    catch (ArgumentException e) { return Results.BadRequest(new { error = e.Message }); }
    catch (InvalidOperationException e) { return Results.BadRequest(new { error = e.Message }); }
});
app.Run();
