using Microsoft.EntityFrameworkCore;
using Showcase;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<ObservationStore>(options => options.UseNpgsql(
    builder.Configuration.GetConnectionString("Database")
    ?? "Host=localhost;Database=showcase;Username=showcase;Password=showcase-local"));
builder.Services.AddScoped<ObservationService>();
builder.Services.AddSingleton<ObservationGate>();
builder.Services.AddSingleton<BrokerStatus>();
builder.Services.AddHostedService<MqttIngestion>();
builder.Services.AddSingleton<IDemoPublisher, DemoPublisher>();
builder.Services.AddSingleton<DemoController>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<DemoController>());
var app = builder.Build();
using (var scope = app.Services.CreateScope())
    await scope.ServiceProvider.GetRequiredService<ObservationStore>().Database.EnsureCreatedAsync();

app.MapGet("/api/luggage", async (ObservationService service, CancellationToken token) =>
    Results.Ok(await service.Current(token)));
app.MapGet("/api/luggage/{id}", async (string id, ObservationService service, CancellationToken token) =>
{
    var history = await service.History(id, token);
    return history.Count == 0 ? Results.NotFound() : Results.Ok(new { current = history[^1], observations = history });
});
app.MapGet("/api/demo", (DemoController demo) => Results.Ok(demo.Status));
app.MapPost("/api/database/wipe", async (DemoController demo, ObservationService service, CancellationToken token) =>
    await demo.Clear(() => service.Clear(token))
        ? Results.Ok(new { message = "Showcase database wiped" })
        : Results.Conflict(new { error = "Wait for the demo or database wipe to finish" }));
app.MapPost("/api/demo", (DemoController demo, BrokerStatus broker) =>
{
    if (demo.Status.Status == "running") return Results.Conflict(new { error = "A demo is already running" });
    if (!broker.Connected) return Results.Json(new { error = "MQTT is not connected yet" }, statusCode: 503);
    var started = demo.Start();
    return started == null ? Results.Conflict(new { error = "A demo is already running" })
        : Results.Accepted("/api/demo", started);
});
app.MapGet("/health", async (ObservationStore store, BrokerStatus broker, CancellationToken token) =>
{
    var database = await store.Database.CanConnectAsync(token);
    return Results.Json(new { database, mqtt = broker.Connected }, statusCode: database && broker.Connected ? 200 : 503);
});
app.Run();

public partial class Program;
