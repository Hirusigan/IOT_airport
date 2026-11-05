using Luggage;
using Moq;
using Xunit;
using System.Text.Json;

public class ServiceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-07T12:00:00Z");
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }

    [Theory]
    [InlineData(-21, false, false)]
    [InlineData(-20, true, false)]
    [InlineData(0, false, false)]
    [InlineData(5, false, true)]
    public async Task Deadline_rules_follow_cutoff_and_grace(int seconds, bool warning, bool missed)
    {
        var alerts = new Mock<IAlertRepository>();
        var flights = new Mock<IFlightRepository>();
        var assignments = new Mock<IAssignmentRepository>();
        var assignment = new Assignment { JourneyId = "J", FlightId = "F" };
        flights.Setup(x => x.GetAllAsync()).ReturnsAsync(new[] { new Flight { Id = "F", Origin = "AAR", ScheduledDeparture = Now, ActualDeparture = seconds >= 0 ? Now : null } });
        assignments.Setup(x => x.GetAllAsync()).ReturnsAsync(new[] { assignment });
        var service = new RuleService(alerts.Object, flights.Object, assignments.Object, new RuleSettings(20, 5, 30));
        await service.Evaluate(Now.AddSeconds(seconds));
        alerts.Verify(x => x.Add(It.Is<Alert>(a => a.Rule == AlertType.AtRisk)), warning ? Times.Once() : Times.Never());
        alerts.Verify(x => x.Add(It.Is<Alert>(a => a.Rule == AlertType.MissedFlight)), missed ? Times.Once() : Times.Never());
    }

    [Fact]
    public async Task Timely_loading_resolves_missed_flight_without_replacing_alert()
    {
        var alerts = new Mock<IAlertRepository>();
        var flights = new Mock<IFlightRepository>();
        var assignments = new Mock<IAssignmentRepository>();
        var existing = new Alert { Rule = AlertType.MissedFlight, Active = true };
        alerts.Setup(x => x.GetAsync(AlertType.MissedFlight, "J", "F")).ReturnsAsync(existing);
        flights.Setup(x => x.GetAllAsync()).ReturnsAsync(new[] { new Flight { Id = "F", Origin = "AAR", ScheduledDeparture = Now } });
        assignments.Setup(x => x.GetAllAsync()).ReturnsAsync(new[] { new Assignment { JourneyId = "J", FlightId = "F", LoadedAt = Now.AddSeconds(-1) } });
        await new RuleService(alerts.Object, flights.Object, assignments.Object, new RuleSettings(20, 5, 30)).Evaluate(Now.AddMinutes(1));
        Assert.False(existing.Active);
        Assert.Equal(Now.AddMinutes(1), existing.ResolvedAt);
        alerts.Verify(x => x.Add(It.IsAny<Alert>()), Times.Never());
    }

    [Theory]
    [InlineData(false, "disconnected")]
    [InlineData(true, "stale")]
    public async Task Airport_business_status_is_independent_of_telemetry(bool connected, string telemetry)
    {
        var airports = new Mock<IAirportRepository>();
        var alerts = new Mock<IAlertRepository>();
        airports.Setup(x => x.GetEnabledAsync()).ReturnsAsync(new[] { new Airport { Id = "AAR", LastSeenAt = Now.AddSeconds(-31) } });
        alerts.Setup(x => x.GetFilteredAsync("AAR", true, null)).ReturnsAsync(new[] { new Alert { Severity = AlertSeverity.Orange }, new Alert { Severity = AlertSeverity.Red } });
        var service = new AirportService(airports.Object, alerts.Object, Mock.Of<IFlightRepository>(), Mock.Of<IUnitOfWork>(),
            new BrokerHealth { Connected = connected }, new RuleSettings(20, 5, 30), new ProcessingGate(), Mock.Of<IDashboardNotifier>(), new Clock());
        var result = Assert.Single(await service.GetAll());
        Assert.Equal("red", result.BusinessStatus);
        Assert.Equal(2, result.AlertCount);
        Assert.Equal(telemetry, result.TelemetryHealth);
    }

    [Fact]
    public async Task Invalid_airport_registration_does_not_write()
    {
        var airports = new Mock<IAirportRepository>();
        var service = new AirportService(airports.Object, Mock.Of<IAlertRepository>(), Mock.Of<IFlightRepository>(), Mock.Of<IUnitOfWork>(),
            new BrokerHealth(), new RuleSettings(20, 5, 30), new ProcessingGate(), Mock.Of<IDashboardNotifier>(), new Clock());
        await Assert.ThrowsAsync<ArgumentException>(() => service.Register(new Airport { Id = "invalid" }));
        airports.Verify(x => x.Add(It.IsAny<Airport>()), Times.Never());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Deadline_notifications_follow_successful_commit(bool fail)
    {
        var work = new Mock<IUnitOfWork>();
        var tx = new Mock<ITransaction>();
        var notifications = new Mock<IDashboardNotifier>();
        work.Setup(x => x.BeginTransactionAsync()).ReturnsAsync(tx.Object);
        work.SetupGet(x => x.HasChanges).Returns(true);
        if (fail) tx.Setup(x => x.CommitAsync()).ThrowsAsync(new InvalidOperationException("Commit failed"));
        var service = new DeadlineService(Mock.Of<IRuleService>(), work.Object, new ProcessingGate(), notifications.Object, new Clock());
        if (fail) await Assert.ThrowsAsync<InvalidOperationException>(() => service.Evaluate());
        else await service.Evaluate();
        notifications.Verify(x => x.Send(null), fail ? Times.Never() : Times.Once());
    }

    [Fact]
    public async Task Old_loading_evidence_does_not_overwrite_newer_state()
    {
        var events = new Mock<IEventRepository>();
        var assignments = new Mock<IAssignmentRepository>();
        var flights = new Mock<IFlightRepository>();
        var states = new Mock<ILuggageStateRepository>();
        var airports = new Mock<IAirportRepository>();
        var work = new Mock<IUnitOfWork>();
        var assignment = new Assignment { JourneyId = "J", FlightId = "F" };
        var state = new LuggageState { JourneyId = "J", Sequence = 10, Stage = "arrived", AirportId = "CPH" };
        assignments.Setup(x => x.GetByJourneyAsync("J")).ReturnsAsync(assignment);
        flights.Setup(x => x.GetAsync("F")).ReturnsAsync(new Flight { Id = "F", Origin = "AAR", ScheduledDeparture = Now });
        states.Setup(x => x.GetAsync("J")).ReturnsAsync(state);
        airports.Setup(x => x.GetAsync("AAR")).ReturnsAsync(new Airport { Id = "AAR" });
        work.Setup(x => x.BeginTransactionAsync()).ReturnsAsync(Mock.Of<ITransaction>());
        var service = new EventProcessingService(events.Object, assignments.Object, flights.Object, states.Object, airports.Object,
            Mock.Of<IEventValidationService>(), Mock.Of<IRuleService>(), work.Object, new ProcessingGate(), Mock.Of<IDashboardNotifier>(), new Clock());
        var evidence = Now.AddSeconds(-5);
        var raw = JsonSerializer.Serialize(new { eventId = "E", eventType = "luggage", airportId = "AAR", occurredAt = evidence,
            data = new { journeyId = "J", luggageId = "B", sequence = 2, stage = "loaded", flightId = "F" } });
        Assert.True(await service.Process("topic", raw, "EU"));
        Assert.Equal(evidence, assignment.LoadedAt);
        Assert.Equal(10, state.Sequence);
        Assert.Equal("arrived", state.Stage);
        events.Verify(x => x.Add(It.Is<Observation>(e => e.Outcome == "historical-sequence")), Times.Once());
    }

    [Fact]
    public async Task Duplicate_event_has_no_business_effect()
    {
        var events = new Mock<IEventRepository>();
        var work = new Mock<IUnitOfWork>();
        var notifications = new Mock<IDashboardNotifier>();
        events.Setup(x => x.ExistsAsync("E")).ReturnsAsync(true);
        var service = new EventProcessingService(events.Object, Mock.Of<IAssignmentRepository>(), Mock.Of<IFlightRepository>(),
            Mock.Of<ILuggageStateRepository>(), Mock.Of<IAirportRepository>(), Mock.Of<IEventValidationService>(), Mock.Of<IRuleService>(),
            work.Object, new ProcessingGate(), notifications.Object, new Clock());
        Assert.False(await service.Process("topic", "{\"eventId\":\"E\"}", "EU"));
        work.Verify(x => x.BeginTransactionAsync(), Times.Never());
        events.Verify(x => x.Add(It.IsAny<Observation>()), Times.Never());
        notifications.Verify(x => x.Send(It.IsAny<string?>()), Times.Never());
    }
}
