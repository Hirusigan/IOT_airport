using Microsoft.EntityFrameworkCore;

namespace Luggage;

public class TrackingDbContext(DbContextOptions<TrackingDbContext> options) : DbContext(options)
{
    public DbSet<Airport> Airports => Set<Airport>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<Flight> Flights => Set<Flight>();
    public DbSet<Journey> Journeys => Set<Journey>();
    public DbSet<Trip> Trips => Set<Trip>();
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<Observation> Events => Set<Observation>();
    public DbSet<LuggageState> States => Set<LuggageState>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<Diagnostic> Diagnostics => Set<Diagnostic>();
    public DbSet<RetiredRun> RetiredRuns => Set<RetiredRun>();

    protected override void OnModelCreating(ModelBuilder m)
    {
        m.Entity<Airport>().HasKey(x => x.Id);
        m.Entity<Device>().HasKey(x => new { x.AirportId, x.Id });
        m.Entity<Device>().Property(x => x.Type).HasConversion(x => x.ToValue(), x => ModelValues.ParseDeviceType(x));
        m.Entity<Device>().HasOne<Airport>().WithMany().HasForeignKey(x => x.AirportId);
        m.Entity<Flight>().HasKey(x => x.Id);
        m.Entity<Flight>().Property(x => x.ActualDeparture).IsConcurrencyToken();
        m.Entity<Flight>().Property(x => x.ActualArrival).IsConcurrencyToken();
        m.Entity<Flight>().HasOne<Airport>().WithMany().HasForeignKey(x => x.Origin).OnDelete(DeleteBehavior.Restrict);
        m.Entity<Flight>().HasOne<Airport>().WithMany().HasForeignKey(x => x.Destination).OnDelete(DeleteBehavior.Restrict);
        m.Entity<Journey>().HasKey(x => x.Id);
        m.Entity<Trip>().HasKey(x => x.Id);
        m.Entity<Trip>().HasIndex(x => new { x.RunId, x.LuggageId });
        m.Entity<Journey>().HasOne<Trip>().WithMany().HasForeignKey(x => x.TripId);
        m.Entity<Journey>().HasIndex(x => new { x.TripId, x.LegOrder }).IsUnique();
        m.Entity<Journey>().HasIndex(x => x.LuggageId);
        m.Entity<Assignment>().HasKey(x => new { x.JourneyId, x.FlightId });
        m.Entity<Assignment>().HasIndex(x => x.JourneyId).IsUnique();
        m.Entity<Assignment>().HasOne<Journey>().WithMany().HasForeignKey(x => x.JourneyId);
        m.Entity<Assignment>().HasOne<Flight>().WithMany().HasForeignKey(x => x.FlightId);
        m.Entity<Observation>().HasKey(x => x.EventId);
        m.Entity<Observation>().Property(x => x.EventType).HasConversion(x => x.ToValue(), x => ModelValues.ParseEventType(x));
        m.Entity<Observation>().Property(x => x.Outcome).HasConversion(x => x.ToValue(), x => ModelValues.ParseOutcome(x));
        m.Entity<Observation>().HasIndex(x => x.JourneyId);
        m.Entity<Observation>().HasOne<Journey>().WithMany().HasForeignKey(x => x.JourneyId);
        m.Entity<LuggageState>().HasKey(x => x.JourneyId);
        m.Entity<LuggageState>().Property(x => x.Stage).HasConversion(x => x.ToValue(), x => ModelValues.ParseStage(x));
        m.Entity<LuggageState>().Property(x => x.Sequence).IsConcurrencyToken();
        m.Entity<LuggageState>().HasOne<Journey>().WithOne().HasForeignKey<LuggageState>(x => x.JourneyId);
        m.Entity<Alert>().HasKey(x => x.Id);
        m.Entity<Alert>().Property(x => x.Rule).HasConversion(x => x.ToValue(), x => AlertValues.ParseType(x));
        m.Entity<Alert>().Property(x => x.Severity).HasConversion(x => x.ToValue(), x => AlertValues.ParseSeverity(x));
        m.Entity<Alert>().HasIndex(x => new { x.Rule, x.JourneyId, x.FlightId }).IsUnique();
        m.Entity<Alert>().HasOne<Assignment>().WithMany().HasForeignKey(x => new { x.JourneyId, x.FlightId });
        m.Entity<Diagnostic>().HasKey(x => x.Id);
        m.Entity<RetiredRun>().HasKey(x => x.Id);
    }
}
