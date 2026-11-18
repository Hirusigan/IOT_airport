using Luggage;

var builder = WebApplication.CreateBuilder(args);
var generatedConfig = Environment.GetEnvironmentVariable("GENERATED_CONFIG_PATH");
if (generatedConfig != null) builder.Configuration.AddJsonFile(generatedConfig, optional: false);
builder.Services.AddLuggageBackend(builder.Configuration);
var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync();
}
app.MapControllers();
app.MapHub<DashboardHub>("/hub");
app.Run();
