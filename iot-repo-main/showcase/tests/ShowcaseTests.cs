using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Showcase;
using Xunit;

public class TestStore(DbContextOptions<ObservationStore> options) : ObservationStore(options)
{
    protected override void OnModelCreating(ModelBuilder model)
    {
        base.OnModelCreating(model);
        model.Entity<Observation>().Property(x => x.OccurredAt).HasConversion(x => x.UtcTicks, x => new DateTimeOffset(x, TimeSpan.Zero));
        model.Entity<Observation>().Property(x => x.ReceivedAt).HasConversion(x => x.UtcTicks, x => new DateTimeOffset(x, TimeSpan.Zero));
    }
}

public class ShowcaseTests
{
    private static TrackingEvent Event(string point = "checkin", int sequence = 1) =>
        new(Guid.NewGuid(), "BAG-test", "AAR", point, sequence, DateTimeOffset.UtcNow);

    private static string Wire(TrackingEvent message) => JsonSerializer.Serialize(new
    {
        schemaVersion = 1, eventId = message.EventId, simulationRunId = "test-run",
        occurredAt = message.OccurredAt, airportId = message.AirportId,
        deviceId = "scanner-001", eventType = "luggage",
        data = new { luggageId = message.BagId, journeyId = "test-run-JOURNEY-1",
            sequence = message.Sequence, stage = message.TrackingPointId == "sorting" ? "sorted" : message.TrackingPointId }
    }, JsonSerializerOptions.Web);

    [Theory]
    [InlineData("airport/AAR/scanner/scanner-002/luggage", 1)]
    [InlineData("airport/CPH/scanner/scanner-001/luggage", 1)]
    [InlineData("airport/AAR/gate/scanner-001/luggage", 1)]
    [InlineData("airport/AAR/scanner/scanner-001/departure", 1)]
    [InlineData("airport/AAR/observations", 1)]
    [InlineData("airport/AAR/scanner/scanner-001/luggage", 2)]
    public async Task RejectsMismatchedTopicsAndUnsupportedSchemaVersions(string topic, int version)
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var store = new TestStore(new DbContextOptionsBuilder<ObservationStore>().UseSqlite(connection).Options);
        await store.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var service = new ObservationService(store, new ObservationGate());
        var json = Wire(Event()).Replace("\"schemaVersion\":1", $"\"schemaVersion\":{version}");
        await Assert.ThrowsAsync<ArgumentException>(() => service.Ingest(topic, json, TestContext.Current.CancellationToken));
        Assert.Equal(0, await store.Observations.CountAsync(TestContext.Current.CancellationToken));
    }

    private static async Task WaitForCompletion(DemoController demo)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        while (demo.Status.Status == "running") await Task.Delay(10, timeout.Token);
    }

    [Fact]
    public async Task StoresHistoryDeduplicatesAndKeepsHighestSequence()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var options = new DbContextOptionsBuilder<ObservationStore>().UseSqlite(connection).Options;
        await using (var store = new TestStore(options))
        {
            await store.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
            var service = new ObservationService(store, new ObservationGate());
            var last = Event("sorting", 3);
            var json = Wire(last);
            Assert.True(await service.Ingest(ObservationService.Topic, json, TestContext.Current.CancellationToken));
            Assert.False(await service.Ingest(ObservationService.Topic, json, TestContext.Current.CancellationToken));
            Assert.True(await service.Ingest(ObservationService.Topic, Wire(Event()), TestContext.Current.CancellationToken));
            Assert.Equal("sorting", Assert.Single(await service.Current(TestContext.Current.CancellationToken)).TrackingPointId);
            Assert.Equal(2, (await service.History("BAG-test", TestContext.Current.CancellationToken)).Count);
        }
        await using var reopened = new TestStore(options);
        Assert.Equal(2, await reopened.Observations.CountAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("unknown", 1)]
    [InlineData("checkin", 0)]
    public async Task RejectsInvalidEvents(string point, int sequence)
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var store = new TestStore(new DbContextOptionsBuilder<ObservationStore>().UseSqlite(connection).Options);
        await store.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var service = new ObservationService(store, new ObservationGate());
        await Assert.ThrowsAsync<ArgumentException>(() => service.Ingest(ObservationService.Topic,
            Wire(Event(point, sequence)), TestContext.Current.CancellationToken));
        Assert.Equal(0, await store.Observations.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task EqualSequencesAndReceiptTimesStillReturnOneCurrentObservation()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var store = new TestStore(new DbContextOptionsBuilder<ObservationStore>().UseSqlite(connection).Options);
        await store.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var service = new ObservationService(store, new ObservationGate());
        await service.Ingest(ObservationService.Topic, Wire(Event()), TestContext.Current.CancellationToken);
        await service.Ingest(ObservationService.Topic, Wire(Event()), TestContext.Current.CancellationToken);
        foreach (var observation in store.Observations.Local) observation.ReceivedAt = new DateTimeOffset(2026, 10, 10, 10, 0, 0, TimeSpan.Zero);
        await store.SaveChangesAsync(TestContext.Current.CancellationToken);
        var current = Assert.Single(await service.Current(TestContext.Current.CancellationToken));
        Assert.Equal((await service.History("BAG-test", TestContext.Current.CancellationToken))[^1].EventId, current.EventId);
    }

    [Fact]
    public async Task ConcurrentStartsLaunchExactlyOnePublisher()
    {
        var publisher = new ControlledPublisher();
        var demo = new DemoController(publisher, NullLogger<DemoController>.Instance);
        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(demo.Start)));
        Assert.Single(results, x => x != null);
        await publisher.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        publisher.Finish.SetResult();
        await WaitForCompletion(demo);
        await demo.StopAsync(TestContext.Current.CancellationToken);
        Assert.Equal("completed", demo.Status.Status);
    }

    [Fact]
    public async Task PublisherFailureIsReported()
    {
        var publisher = new ControlledPublisher();
        var demo = new DemoController(publisher, NullLogger<DemoController>.Instance);
        demo.Start();
        await publisher.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        publisher.Finish.SetException(new InvalidOperationException("Publisher exited with code 1"));
        await WaitForCompletion(demo);
        Assert.Equal("failed", demo.Status.Status);
        Assert.Contains("code 1", demo.Status.Error);
        await demo.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ApiReturnsNotFoundAndConflict()
    {
        using var factory = new TestApp();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/luggage/missing", TestContext.Current.CancellationToken)).StatusCode);
        var broker = factory.Services.GetRequiredService<BrokerStatus>();
        broker.Connected = false;
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/health", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.PostAsync("/api/demo", null, TestContext.Current.CancellationToken)).StatusCode);
        broker.Connected = true;
        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsync("/api/demo", null, TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync("/api/demo", null, TestContext.Current.CancellationToken)).StatusCode);
        factory.Publisher.Finish.TrySetResult();
    }

    [Fact]
    public async Task CurrentStateIsSortedByScanTimeNewestFirst()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var store = new TestStore(new DbContextOptionsBuilder<ObservationStore>().UseSqlite(connection).Options);
        await store.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var service = new ObservationService(store, new ObservationGate());
        var older = Event() with { BagId = "BAG-A", OccurredAt = DateTimeOffset.UtcNow.AddMinutes(-2) };
        var newer = Event("sorting", 3) with { BagId = "BAG-Z" };
        foreach (var message in new[] { newer, older })
            await service.Ingest(ObservationService.Topic, Wire(message), TestContext.Current.CancellationToken);
        Assert.Equal(new[] { "BAG-Z", "BAG-A" }, (await service.Current(TestContext.Current.CancellationToken)).Select(x => x.BagId));
    }

    [Fact]
    public async Task WipeClearsHistoryResetsStatusAndAllowsFreshData()
    {
        using var factory = new TestApp();
        using var client = factory.CreateClient();
        var message = Event();
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ObservationService>().Ingest(ObservationService.Topic,
                Wire(message), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/database/wipe", null, TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal("[]", await client.GetStringAsync("/api/luggage", TestContext.Current.CancellationToken));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/luggage/BAG-test", TestContext.Current.CancellationToken)).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<ObservationService>();
            Assert.False(await service.Ingest(ObservationService.Topic, Wire(message), TestContext.Current.CancellationToken));
            Assert.True(await service.Ingest(ObservationService.Topic, Wire(Event() with { BagId = "BAG-fresh" }), TestContext.Current.CancellationToken));
        }
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/database/wipe", null, TestContext.Current.CancellationToken)).StatusCode);
        using var status = JsonDocument.Parse(await client.GetStringAsync("/api/demo", TestContext.Current.CancellationToken));
        Assert.Equal("idle", status.RootElement.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsync("/api/demo", null, TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync("/api/database/wipe", null, TestContext.Current.CancellationToken)).StatusCode);
        factory.Publisher.Finish.TrySetResult();
    }

    [Fact]
    public async Task DemoCannotStartDuringWipe()
    {
        var demo = new DemoController(new ControlledPublisher(), NullLogger<DemoController>.Instance);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var wipe = demo.Clear(() => finish.Task);
        Assert.Null(demo.Start());
        Assert.False(await demo.Clear(() => Task.CompletedTask));
        finish.SetResult();
        Assert.True(await wipe);
        await demo.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ShutdownCancelsPublisherAndFreshControllerDoesNotResume()
    {
        var publisher = new ControlledPublisher();
        var demo = new DemoController(publisher, NullLogger<DemoController>.Instance);
        demo.Start();
        await publisher.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await demo.StopAsync(TestContext.Current.CancellationToken);
        Assert.Equal("failed", demo.Status.Status);
        Assert.Equal("Demo cancelled by backend shutdown", demo.Status.Error);
        var restarted = new DemoController(publisher, NullLogger<DemoController>.Instance);
        Assert.Equal("idle", restarted.Status.Status);
        await restarted.StopAsync(TestContext.Current.CancellationToken);
    }
}

public class ControlledPublisher : IDemoPublisher
{
    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Finish { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public async Task Publish(string demoId, CancellationToken token)
    {
        Started.TrySetResult();
        await Finish.Task.WaitAsync(token);
    }
}

public class TestApp : WebApplicationFactory<Program>
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    public ControlledPublisher Publisher { get; } = new();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        connection.Open();
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ObservationStore>();
            services.RemoveAll<DbContextOptions<ObservationStore>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ObservationStore>>();
            services.AddScoped<ObservationStore>(_ => new TestStore(new DbContextOptionsBuilder<ObservationStore>().UseSqlite(connection).Options));
            services.Remove(services.Single(x => x.ServiceType == typeof(IHostedService) && x.ImplementationType == typeof(MqttIngestion)));
            services.RemoveAll<IDemoPublisher>();
            services.AddSingleton<IDemoPublisher>(Publisher);
            services.RemoveAll<BrokerStatus>();
            services.AddSingleton(new BrokerStatus { Connected = true });
        });
    }
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) connection.Dispose();
    }
}


