using Luggage;

if (await AirportBrokerProvisioner.TryRunCleanupCommandAsync(args))
    return;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddLuggageBackend(builder.Configuration);
var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync();
}
app.MapControllers();
app.MapHub<DashboardHub>("/hub");
app.Run();
