using Microsoft.EntityFrameworkCore;
namespace Luggage;

public static class DependencyInjection
{
    public static IServiceCollection AddLuggageBackend(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<TrackingDbContext>(options => options.UseNpgsql(config.GetConnectionString("Database")
            ?? throw new InvalidOperationException("ConnectionStrings__Database is required")));
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddSingleton<IAirportBrokerProvisioner, AirportBrokerProvisioner>();
        services.AddSingleton<EnvironmentSetupLock>();
        services.AddHttpClient();
        services.AddSingleton<SimulationRunner>();
        services.AddSingleton<ISimulationRunner>(sp => sp.GetRequiredService<SimulationRunner>());
        services.AddHostedService(sp => sp.GetRequiredService<SimulationRunner>());
        services.AddScoped<IDevtoolsRepository, DevtoolsRepository>();
        services.AddScoped<IDevtoolsService, DevtoolsService>();
        services.AddSingleton<BrokerHealth>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(new RuleSettings(config.GetValue("Rules:LoadingCutoffSeconds", 20),
            config.GetValue("Rules:GraceSeconds", 5), config.GetValue("Rules:StaleSeconds", 30)));
        services.AddScoped<IAirportRepository, AirportRepository>();
        services.AddScoped<IDeviceRepository, DeviceRepository>();
        services.AddScoped<IFlightRepository, FlightRepository>();
        services.AddScoped<IJourneyRepository, JourneyRepository>();
        services.AddScoped<ITripRepository, TripRepository>();
        services.AddScoped<IAssignmentRepository, AssignmentRepository>();
        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<ILuggageStateRepository, LuggageStateRepository>();
        services.AddScoped<IAlertRepository, AlertRepository>();
        services.AddScoped<IStatisticsRepository, StatisticsRepository>();
        services.AddScoped<IAirportService, AirportService>();
        services.AddScoped<ILuggageService, LuggageService>();
        services.AddScoped<IAlertService, AlertService>();
        services.AddScoped<IStatisticsService, StatisticsService>();
        services.AddScoped<IEventValidationService, EventValidationService>();
        services.AddScoped<IEventProcessingService, EventProcessingService>();
        services.AddScoped<IRuleService, RuleService>();
        services.AddScoped<IDeadlineService, DeadlineService>();
        services.AddScoped<IDiagnosticService, DiagnosticService>();
        services.AddTransient<IDashboardNotifier, SignalRDashboardNotifier>();
        services.AddScoped<DatabaseInitializer>();
        services.AddControllers().AddJsonOptions(options =>
            options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase)));
        services.AddSignalR();
        services.AddHostedService<MqttIngestion>();
        services.AddHostedService<DeadlineTimer>();
        return services;
    }
}
