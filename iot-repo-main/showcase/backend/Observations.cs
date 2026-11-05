using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Showcase;

public record TrackingEvent(Guid EventId, string BagId, string AirportId, string TrackingPointId,
    int Sequence, DateTimeOffset OccurredAt);
public record Envelope(int SchemaVersion, string EventId, string SimulationRunId,
    DateTimeOffset OccurredAt, string AirportId, string DeviceId, string EventType, JsonElement Data);
public record BagData(string LuggageId, string JourneyId, int Sequence, string Stage, string? FlightId);

public class Observation
{
    public Guid EventId { get; set; }
    public string BagId { get; set; } = "";
    public string AirportId { get; set; } = "";
    public string TrackingPointId { get; set; } = "";
    public int Sequence { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
}

public class ObservationStore(DbContextOptions<ObservationStore> options) : DbContext(options)
{
    public DbSet<Observation> Observations => Set<Observation>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Observation>().HasKey(x => x.EventId);
        model.Entity<Observation>().HasIndex(x => new { x.BagId, x.Sequence });
    }
}

public class ObservationGate
{
    public SemaphoreSlim Access { get; } = new(1, 1);
    public HashSet<string> ClearedBags { get; } = [];
}

public class ObservationService(ObservationStore store, ObservationGate gate)
{
    public static readonly string[] TrackingPoints = ["checkin", "security", "sorting"];
    public const string Topic = "airport/AAR/scanner/scanner-001/luggage";
    public const string Subscription = "airport/AAR/scanner/+/luggage";

    public async Task<bool> Ingest(string topic, string json, CancellationToken token = default)
    {
        var envelope = JsonSerializer.Deserialize<Envelope>(json, JsonSerializerOptions.Web)
            ?? throw new ArgumentException("Missing event");
        var parts = topic.Split('/');
        if (parts.Length != 5 || parts[0] != "airport" || parts[1] != envelope.AirportId
            || parts[2] != "scanner" || parts[3] != envelope.DeviceId || parts[4] != envelope.EventType
            || envelope.AirportId != "AAR" || envelope.EventType != "luggage"
            || envelope.DeviceId is not ("scanner-001" or "scanner-002" or "scanner-003")
            || envelope.SchemaVersion != 1 || !Guid.TryParse(envelope.EventId, out var eventId)
            || eventId == Guid.Empty || string.IsNullOrWhiteSpace(envelope.SimulationRunId)
            || envelope.OccurredAt == default || envelope.OccurredAt.Offset != TimeSpan.Zero
            || envelope.OccurredAt > DateTimeOffset.UtcNow.AddSeconds(5))
            throw new ArgumentException("Invalid envelope or topic/payload identity mismatch");
        var data = envelope.Data.Deserialize<BagData>(JsonSerializerOptions.Web)
            ?? throw new ArgumentException("Missing luggage data");
        if (string.IsNullOrWhiteSpace(data.JourneyId) || data.Stage is not ("checkin" or "security" or "sorted"))
            throw new ArgumentException("Invalid journey or unsupported showcase stage");
        var message = new TrackingEvent(eventId, data.LuggageId, envelope.AirportId,
            data.Stage == "sorted" ? "sorting" : data.Stage, data.Sequence, envelope.OccurredAt);
        if (string.IsNullOrWhiteSpace(message.BagId)
            || message.BagId.Length > 100 || message.AirportId != "AAR"
            || !TrackingPoints.Contains(message.TrackingPointId) || message.Sequence < 1
            || message.OccurredAt == default || message.OccurredAt.Offset != TimeSpan.Zero)
            throw new ArgumentException("Invalid tracking event");
        await gate.Access.WaitAsync(token);
        try
        {
            if (gate.ClearedBags.Contains(message.BagId)) return false;
            if (await store.Observations.AnyAsync(x => x.EventId == message.EventId, token)) return false;
            store.Observations.Add(new Observation
            {
                EventId = message.EventId, BagId = message.BagId, AirportId = message.AirportId,
                TrackingPointId = message.TrackingPointId, Sequence = message.Sequence,
                OccurredAt = message.OccurredAt, ReceivedAt = DateTimeOffset.UtcNow
            });
            await store.SaveChangesAsync(token);
            return true;
        }
        finally { gate.Access.Release(); }
    }

    public async Task Clear(CancellationToken token)
    {
        await gate.Access.WaitAsync(token);
        try
        {
            var bags = await store.Observations.Select(x => x.BagId).Distinct().ToListAsync(token);
            await store.Observations.ExecuteDeleteAsync(token);
            gate.ClearedBags.UnionWith(bags);
        }
        finally { gate.Access.Release(); }
    }

    public async Task<List<Observation>> Current(CancellationToken token = default) =>
        await store.Observations.AsNoTracking()
            .Where(x => x.EventId == store.Observations.Where(other => other.BagId == x.BagId)
                .OrderByDescending(other => other.Sequence).ThenByDescending(other => other.ReceivedAt)
                .ThenByDescending(other => other.EventId).Select(other => other.EventId).First())
            .OrderByDescending(x => x.OccurredAt).ThenBy(x => x.BagId).ToListAsync(token);

    public async Task<List<Observation>> History(string bagId, CancellationToken token = default) =>
        await store.Observations.AsNoTracking().Where(x => x.BagId == bagId)
            .OrderBy(x => x.Sequence).ThenBy(x => x.ReceivedAt).ThenBy(x => x.EventId).ToListAsync(token);
}
