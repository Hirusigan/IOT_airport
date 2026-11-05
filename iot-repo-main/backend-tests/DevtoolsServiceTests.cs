using Luggage;
using Moq;
using Xunit;

public class DevtoolsServiceTests
{
    [Fact]
    public async Task Conflicting_start_is_propagated_and_releases_lock()
    {
        var runner = new Mock<ISimulationRunner>();
        var gate = new DevtoolsGate();
        runner.Setup(x => x.Start("normal")).ThrowsAsync(new ConflictException("Already running"));
        var service = new DevtoolsService(runner.Object, Mock.Of<IDevtoolsRepository>(), Mock.Of<IDatabaseSeeder>(), gate, new ProcessingGate(), Mock.Of<IDashboardNotifier>());
        await Assert.ThrowsAsync<ConflictException>(() => service.Start("normal"));
        Assert.Equal(1, gate.Mutex.CurrentCount);
    }

    [Fact]
    public async Task Seed_adds_only_missing_default_registrations()
    {
        var airports = new Mock<IAirportRepository>();
        var devices = new Mock<IDeviceRepository>();
        airports.Setup(x => x.GetAsync("AAR")).ReturnsAsync(new Airport { Id = "AAR" });
        devices.Setup(x => x.GetAsync("AAR", "scanner-001")).ReturnsAsync(new Device { Id = "scanner-001" });
        await new DatabaseSeeder(airports.Object, devices.Object, Mock.Of<IUnitOfWork>()).SeedAsync();
        airports.Verify(x => x.Add(It.Is<Airport>(a => a.Id == "AAR")), Times.Never());
        airports.Verify(x => x.Add(It.Is<Airport>(a => a.Id == "CPH")), Times.Once());
        devices.Verify(x => x.Add(It.Is<Device>(d => d.AirportId == "AAR" && d.Id == "scanner-001")), Times.Never());
        devices.Verify(x => x.Add(It.IsAny<Device>()), Times.Exactly(3));
    }

    [Fact]
    public async Task Clear_stops_publishing_before_clearing_and_notifying()
    {
        var sequence = new MockSequence();
        var runner = new Mock<ISimulationRunner>(MockBehavior.Strict);
        var repository = new Mock<IDevtoolsRepository>(MockBehavior.Strict);
        var notifier = new Mock<IDashboardNotifier>(MockBehavior.Strict);
        runner.InSequence(sequence).Setup(x => x.Stop()).Returns(Task.CompletedTask);
        repository.InSequence(sequence).Setup(x => x.ClearAsync()).Returns(Task.CompletedTask);
        notifier.InSequence(sequence).Setup(x => x.Send(null)).Returns(Task.CompletedTask);
        var service = new DevtoolsService(runner.Object, repository.Object, Mock.Of<IDatabaseSeeder>(), new DevtoolsGate(), new ProcessingGate(), notifier.Object);
        await service.Clear();
        runner.VerifyAll(); repository.VerifyAll(); notifier.VerifyAll();
    }

    [Fact]
    public async Task Failed_stop_does_not_clear_data_and_releases_operation_lock()
    {
        var runner = new Mock<ISimulationRunner>();
        var repository = new Mock<IDevtoolsRepository>();
        var notifier = new Mock<IDashboardNotifier>();
        var gate = new DevtoolsGate();
        runner.Setup(x => x.Stop()).ThrowsAsync(new InvalidOperationException("Could not stop"));
        var service = new DevtoolsService(runner.Object, repository.Object, Mock.Of<IDatabaseSeeder>(), gate, new ProcessingGate(), notifier.Object);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.Clear());
        repository.Verify(x => x.ClearAsync(), Times.Never());
        notifier.Verify(x => x.Send(null), Times.Never());
        Assert.Equal(1, gate.Mutex.CurrentCount);
    }

    [Fact]
    public async Task Seed_notifies_only_after_successful_seeding()
    {
        var seeder = new Mock<IDatabaseSeeder>();
        var notifier = new Mock<IDashboardNotifier>();
        seeder.Setup(x => x.SeedAsync()).ThrowsAsync(new InvalidOperationException("Seed failed"));
        var service = new DevtoolsService(Mock.Of<ISimulationRunner>(), Mock.Of<IDevtoolsRepository>(), seeder.Object, new DevtoolsGate(), new ProcessingGate(), notifier.Object);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.Seed());
        notifier.Verify(x => x.Send(null), Times.Never());
    }

    [Fact]
    public async Task Cleared_run_messages_are_ignored_before_validation()
    {
        var events = new Mock<IEventRepository>();
        var validator = new Mock<IEventValidationService>();
        var work = new Mock<IUnitOfWork>();
        events.Setup(x => x.IsRetiredRunAsync("old-run")).ReturnsAsync(true);
        var service = new EventProcessingService(events.Object, Mock.Of<IAssignmentRepository>(), Mock.Of<IFlightRepository>(), Mock.Of<ILuggageStateRepository>(),
            Mock.Of<IAirportRepository>(), validator.Object, Mock.Of<IRuleService>(), work.Object, new ProcessingGate(), Mock.Of<IDashboardNotifier>(), TimeProvider.System);
        Assert.False(await service.Process("topic", "{\"simulationRunId\":\"old-run\"}", "EU"));
        validator.Verify(x => x.Validate(It.IsAny<string>(), It.IsAny<Envelope>(), It.IsAny<string>()), Times.Never());
        work.Verify(x => x.BeginTransactionAsync(), Times.Never());
    }
}
