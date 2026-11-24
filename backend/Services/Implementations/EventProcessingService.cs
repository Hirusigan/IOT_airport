namespace Luggage;

public class EventProcessingService(
    IEventRepository events,
    IAssignmentRepository assignments,
    IFlightRepository flights,
    ILuggageStateRepository states,
    IAirportRepository airports,
    IJourneyRepository journeys,
    ITripRepository trips,
    IEventValidationService validator,
    IRuleService rules,
    IDashboardNotifier notifications,
    TimeProvider clock) : IEventProcessingService
{
    public async Task<bool> Process(string topic, string raw, string region)
    {
        var envelope = JsonContract.Read<Envelope>(raw);
        if (await events.IsRetiredRunAsync(envelope.SimulationRunId))
        {
            return false;
        }

        await validator.Validate(topic, envelope, region);
        if (await events.ExistsAsync(envelope.EventId))
        {
            return false;
        }

        string journeyId;
        await using (var transaction = await events.BeginTransactionAsync())
        {
            var now = clock.GetUtcNow();
            var observation = CreateObservation(envelope, raw, now);
            journeyId = await ProcessLuggage(envelope, observation, now);

            events.Add(observation);
            await UpdateAirportLastSeen(envelope.AirportId, now);
            await rules.Evaluate(now);
            await events.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        await notifications.Send(journeyId);
        return true;
    }

    private static Observation CreateObservation(Envelope envelope, string raw, DateTimeOffset now) => new()
    {
        EventId = envelope.EventId,
        RunId = envelope.SimulationRunId,
        AirportId = envelope.AirportId,
        DeviceId = envelope.DeviceId,
        EventType = EventType.Luggage,
        SchemaVersion = envelope.SchemaVersion,
        OccurredAt = envelope.OccurredAt,
        ReceivedAt = now,
        Payload = raw
    };

    private async Task<string> ProcessLuggage(Envelope envelope, Observation observation, DateTimeOffset now)
    {
        var bag = JsonContract.Data<BagData>(envelope.Data);
        var stage = ModelValues.ParseStage(bag.Stage);
        var journey = await journeys.GetAsync(bag.JourneyId);
        if (journey == null)
        {
            journey = await CreateFirstJourney(envelope, bag);
        }

        observation.JourneyId = journey.Id;
        var assignment = await assignments.GetByJourneyAsync(journey.Id);
        var flight = await flights.GetAsync(assignment.FlightId)
            ?? throw new ArgumentException("Missing flight");

        RecordLoadingEvidence(assignment, bag, stage, envelope, flight);
        await UpdateLuggageState(bag, stage, envelope, observation, assignment, flight, now);
        return journey.Id;
    }

    private async Task<Journey> CreateFirstJourney(Envelope envelope, BagData bag)
    {
        var flightId = bag.FlightId
            ?? throw new ArgumentException("A first check-in must identify its planned flight");
        var plannedFlight = await flights.GetAsync(flightId)
            ?? throw new ArgumentException("Missing flight");
        var tripId = bag.TripId ?? bag.JourneyId;
        var trip = await trips.GetAsync(tripId);
        if (trip != null && (trip.RunId != envelope.SimulationRunId || trip.LuggageId != bag.LuggageId))
        {
            throw new ArgumentException("Trip identity conflicts with luggage/run");
        }

        if (trip == null)
        {
            trips.Add(new Trip { Id = tripId, RunId = envelope.SimulationRunId, LuggageId = bag.LuggageId });
        }

        var journey = new Journey
        {
            Id = bag.JourneyId,
            RunId = envelope.SimulationRunId,
            LuggageId = bag.LuggageId,
            TripId = tripId,
            LegOrder = bag.LegOrder
        };
        journeys.Add(journey);
        assignments.Add(new Assignment { JourneyId = journey.Id, FlightId = plannedFlight.Id });

        // Save the new journey and assignment before loading them below, within the same transaction.
        await events.SaveChangesAsync();
        return journey;
    }

    private static void RecordLoadingEvidence(
        Assignment assignment,
        BagData bag,
        LuggageStage stage,
        Envelope envelope,
        Flight flight)
    {
        if (stage == LuggageStage.Loaded && bag.FlightId == flight.Id && envelope.AirportId == flight.Origin &&
            (assignment.LoadedAt == null || envelope.OccurredAt < assignment.LoadedAt))
        {
            assignment.LoadedAt = envelope.OccurredAt;
        }
    }

    private async Task UpdateLuggageState(
        BagData bag,
        LuggageStage stage,
        Envelope envelope,
        Observation observation,
        Assignment assignment,
        Flight flight,
        DateTimeOffset now)
    {
        var state = await states.GetAsync(bag.JourneyId);
        if (state != null && bag.Sequence <= state.Sequence)
        {
            observation.Outcome = ObservationOutcome.HistoricalSequence;
            return;
        }

        var isNew = state == null;
        state ??= new LuggageState { JourneyId = bag.JourneyId };
        if (isNew)
        {
            states.Add(state);
        }

        state.Sequence = bag.Sequence;
        state.AirportId = envelope.AirportId;
        state.Stage = stage;
        state.FlightId = bag.FlightId;
        state.OccurredAt = envelope.OccurredAt;

        await rules.Set(
            AlertType.WrongFlight,
            assignment,
            envelope.AirportId,
            AlertSeverity.Red,
            stage == LuggageStage.Loaded && bag.FlightId != flight.Id,
            now);
        await rules.Set(
            AlertType.UnexpectedAirport,
            assignment,
            envelope.AirportId,
            AlertSeverity.Red,
            RuleDecisions.Unexpected(envelope.AirportId, stage, envelope.OccurredAt, flight),
            now);
    }

    private async Task UpdateAirportLastSeen(string airportId, DateTimeOffset now)
    {
        var airport = await airports.GetAsync(airportId)
            ?? throw new ArgumentException("Missing airport");
        airport.LastSeenAt = now;
    }
}
