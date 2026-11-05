using Luggage;
using Moq;
using System.Text.Json;
using Xunit;

public class TripServiceTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-10-07T10:00:00Z");

    [Fact]
    public async Task Luggage_groups_ordered_legs_without_merging_other_runs()
    {
        var journeys = new Mock<IJourneyRepository>();
        var assignments = new Mock<IAssignmentRepository>();
        var flights = new Mock<IFlightRepository>();
        var states = new Mock<ILuggageStateRepository>();
        var events = new Mock<IEventRepository>();
        journeys.Setup(x => x.GetByLuggageAsync("B")).ReturnsAsync(new[] {
            new Journey { Id = "J2", RunId = "R1", TripId = "T1", LegOrder = 2 },
            new Journey { Id = "J3", RunId = "R2", TripId = "T2", LegOrder = 1 },
            new Journey { Id = "J1", RunId = "R1", TripId = "T1", LegOrder = 1 }
        });
        assignments.Setup(x => x.GetByJourneysAsync(It.IsAny<string[]>())).ReturnsAsync(new[] {
            new Assignment { JourneyId = "J1", FlightId = "F1" }, new Assignment { JourneyId = "J2", FlightId = "F2" },
            new Assignment { JourneyId = "J3", FlightId = "F3" }
        });
        flights.Setup(x => x.GetByIdsAsync(It.IsAny<string[]>())).ReturnsAsync(new[] {
            new Flight { Id = "F1", ScheduledDeparture = Start }, new Flight { Id = "F2", ScheduledDeparture = Start.AddHours(1) },
            new Flight { Id = "F3", ScheduledDeparture = Start.AddDays(1) }
        });
        states.Setup(x => x.GetByJourneysAsync(It.IsAny<string[]>())).ReturnsAsync(Array.Empty<LuggageState>());
        events.Setup(x => x.GetHistoryAsync(It.IsAny<string[]>())).ReturnsAsync(Array.Empty<Observation>());
        var result = await new LuggageService(journeys.Object, states.Object, assignments.Object, events.Object, flights.Object).Get("B");
        Assert.NotNull(result);
        Assert.Equal(2, result.Trips.Count);
        Assert.Equal("R2", result.Trips[0].RunId);
        Assert.Equal(new[] { "J1", "J2" }, result.Trips[1].Legs.Select(x => x.JourneyId));
    }

    [Theory]
    [InlineData("gap")]
    [InlineData("different-bag")]
    [InlineData("disconnected")]
    [InlineData("departure-order")]
    public async Task Invalid_itineraries_are_rejected_before_writing(string problem)
    {
        var run = Manifest();
        if (problem == "gap") run.Journeys[1].LegOrder = 3;
        if (problem == "different-bag") run.Journeys[1].LuggageId = "OTHER";
        if (problem == "disconnected") run.Flights[1].Origin = "OSL";
        if (problem == "departure-order") run.Flights[1].ScheduledDeparture = Start;
        var work = new Mock<IUnitOfWork>();
        var service = ImportService(work, new Mock<ITripRepository>());
        await Assert.ThrowsAsync<ArgumentException>(() => service.Import(run));
        work.Verify(x => x.BeginTransactionAsync(), Times.Never());
    }

    [Fact]
    public async Task Legacy_manifest_creates_independent_single_leg_trips()
    {
        var run = Manifest();
        foreach (var journey in run.Journeys) { journey.TripId = ""; journey.LegOrder = 1; }
        var trips = new Mock<ITripRepository>();
        var work = new Mock<IUnitOfWork>();
        work.Setup(x => x.BeginTransactionAsync()).ReturnsAsync(Mock.Of<ITransaction>());
        await ImportService(work, trips).Import(run);
        trips.Verify(x => x.AddRange(It.Is<IEnumerable<Trip>>(items => items.Count() == 2 && items.All(t => t.RunId == "R" && t.LuggageId == "B"))), Times.Once());
        Assert.Equal("J1", run.Journeys[0].TripId);
        Assert.Equal("J2", run.Journeys[1].TripId);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    public async Task Transfer_requires_confirmed_arrival_and_later_occurrence(bool arrived, bool tooEarly, bool accepted)
    {
        var airports = new Mock<IAirportRepository>();
        var devices = new Mock<IDeviceRepository>();
        var journeys = new Mock<IJourneyRepository>();
        var flights = new Mock<IFlightRepository>();
        var assignments = new Mock<IAssignmentRepository>();
        var states = new Mock<ILuggageStateRepository>();
        airports.Setup(x => x.GetAsync("CPH")).ReturnsAsync(new Airport { Enabled = true, RegionId = "EU" });
        devices.Setup(x => x.GetAsync("CPH", "scanner-001")).ReturnsAsync(new Device { Enabled = true, Type = "scanner" });
        journeys.Setup(x => x.GetAsync("J2")).ReturnsAsync(new Journey { Id = "J2", TripId = "T", LegOrder = 2, RunId = "R", LuggageId = "B" });
        journeys.Setup(x => x.GetPreviousLegAsync("T", 2)).ReturnsAsync(new Journey { Id = "J1" });
        assignments.Setup(x => x.GetByJourneyAsync("J1")).ReturnsAsync(new Assignment { FlightId = "F1", LoadedAt = Start });
        flights.Setup(x => x.GetAsync("F1")).ReturnsAsync(new Flight { ActualDeparture = Start.AddMinutes(1), Destination = "CPH" });
        states.Setup(x => x.GetAsync("J1")).ReturnsAsync(new LuggageState { Stage = arrived ? "arrived" : "loaded", AirportId = "CPH", OccurredAt = Start.AddMinutes(2) });
        var envelope = new Envelope(1, Guid.NewGuid().ToString(), "R", Start.AddMinutes(tooEarly ? 1 : 3), "CPH", "scanner-001", "luggage",
            JsonSerializer.SerializeToElement(new BagData("B", "J2", 1, "sorted", null)));
        var service = new EventValidationService(airports.Object, devices.Object, journeys.Object, flights.Object, TimeProvider.System, states.Object, assignments.Object);
        var validate = () => service.Validate("airport/CPH/scanner/scanner-001/luggage", envelope, "EU");
        if (accepted) await validate();
        else await Assert.ThrowsAsync<ArgumentException>(validate);
    }

    private static RunImport Manifest() => new("R", Start, [], [], [
        new Flight { Id = "F1", RunId = "R", Origin = "AAR", Destination = "CPH", ScheduledDeparture = Start },
        new Flight { Id = "F2", RunId = "R", Origin = "CPH", Destination = "AAR", ScheduledDeparture = Start.AddHours(1) }
    ], [
        new Journey { Id = "J1", RunId = "R", LuggageId = "B", TripId = "T", LegOrder = 1 },
        new Journey { Id = "J2", RunId = "R", LuggageId = "B", TripId = "T", LegOrder = 2 }
    ], [new Assignment { JourneyId = "J1", FlightId = "F1" }, new Assignment { JourneyId = "J2", FlightId = "F2" }]);

    private static RunImportService ImportService(Mock<IUnitOfWork> work, Mock<ITripRepository> trips)
    {
        var airports = new Mock<IAirportRepository>();
        airports.Setup(x => x.IsEnabledAsync(It.IsAny<string>())).ReturnsAsync(true);
        return new(airports.Object, Mock.Of<IDeviceRepository>(), Mock.Of<IFlightRepository>(), Mock.Of<IJourneyRepository>(),
            Mock.Of<IAssignmentRepository>(), work.Object, new ProcessingGate(), Mock.Of<IDashboardNotifier>(), Mock.Of<IEventRepository>(), trips.Object);
    }
}
