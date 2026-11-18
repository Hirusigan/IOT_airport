namespace Luggage;

public class EventProcessingService(IEventRepository events, IAssignmentRepository assignments, IFlightRepository flights, ILuggageStateRepository states, IAirportRepository airports, IJourneyRepository journeys, ITripRepository trips, IEventValidationService validator, IRuleService rules, IUnitOfWork work, ProcessingGate gate, IDashboardNotifier notifications, TimeProvider clock) : IEventProcessingService
{
    public async Task<bool> Process(string topic, string raw, string region)
    {
        await gate.Mutex.WaitAsync();
        string? journeyId = null;
        try
        {
            var e = JsonContract.Read<Envelope>(raw);
            if (await events.IsRetiredRunAsync(e.SimulationRunId)) return false;
            await validator.Validate(topic, e, region);
            if (await events.ExistsAsync(e.EventId)) return false;
            await using var tx = await work.BeginTransactionAsync();
            var now = clock.GetUtcNow();
            var row = new Observation { EventId = e.EventId, RunId = e.SimulationRunId, AirportId = e.AirportId,
                DeviceId = e.DeviceId, EventType = e.EventType, SchemaVersion = e.SchemaVersion,
                OccurredAt = e.OccurredAt, ReceivedAt = now, Payload = raw };
            if (e.EventType == "luggage")
            {
                var b = JsonContract.Data<BagData>(e.Data);
                var journey = await journeys.GetAsync(b.JourneyId);
                if (journey == null)
                {
                    var flightId = b.FlightId ?? throw new ArgumentException("A first check-in must identify its planned flight");
                    var flight = await flights.GetAsync(flightId) ?? throw new ArgumentException("Missing flight");
                    var tripId = b.TripId ?? b.JourneyId;
                    var trip = await trips.GetAsync(tripId);
                    if (trip != null && (trip.RunId != e.SimulationRunId || trip.LuggageId != b.LuggageId))
                        throw new ArgumentException("Trip identity conflicts with luggage/run");
                    if (trip == null) trips.Add(new Trip { Id = tripId, RunId = e.SimulationRunId, LuggageId = b.LuggageId });
                    journey = new Journey { Id = b.JourneyId, RunId = e.SimulationRunId, LuggageId = b.LuggageId,
                        TripId = tripId, LegOrder = b.LegOrder };
                    journeys.Add(journey);
                    assignments.Add(new Assignment { JourneyId = journey.Id, FlightId = flight.Id });
                    await work.SaveChangesAsync();
                }
                journeyId = row.JourneyId = b.JourneyId;
                var assignment = await assignments.GetByJourneyAsync(b.JourneyId);
                var flight = await flights.GetAsync(assignment.FlightId) ?? throw new ArgumentException("Missing flight");
                var state = await states.GetAsync(b.JourneyId);
                // Loading evidence belongs to the assignment, independently of the latest observation.
                if (b.Stage == "loaded" && b.FlightId == flight.Id && e.AirportId == flight.Origin &&
                    (assignment.LoadedAt == null || e.OccurredAt < assignment.LoadedAt)) assignment.LoadedAt = e.OccurredAt;
                if (state == null || b.Sequence > state.Sequence)
                {
                    var isNew = state == null;
                    state ??= new LuggageState { JourneyId = b.JourneyId };
                    if (isNew) states.Add(state);
                    state.Sequence = b.Sequence; state.AirportId = e.AirportId; state.Stage = b.Stage;
                    state.FlightId = b.FlightId; state.OccurredAt = e.OccurredAt;
                    await rules.Set(AlertType.WrongFlight, assignment, e.AirportId, AlertSeverity.Red, b.Stage == "loaded" && b.FlightId != flight.Id, now);
                    await rules.Set(AlertType.UnexpectedAirport, assignment, e.AirportId, AlertSeverity.Red, RuleDecisions.Unexpected(e.AirportId, b.Stage, e.OccurredAt, flight), now);
                }
                else row.Outcome = "historical-sequence";
            }
            else
            {
                var departure = JsonContract.Data<DepartureData>(e.Data);
                var flight = await flights.GetAsync(departure.FlightId) ?? throw new ArgumentException("Missing flight");
                if (flight.ActualDeparture != null && flight.ActualDeparture != departure.DepartedAt)
                    throw new ArgumentException("Conflicting departure evidence");
                flight.ActualDeparture = departure.DepartedAt;
            }
            events.Add(row);
            (await airports.GetAsync(e.AirportId))!.LastSeenAt = now;
            await rules.Evaluate(now);
            await work.SaveChangesAsync();
            await tx.CommitAsync();
        }
        finally { gate.Mutex.Release(); }
        await notifications.Send(journeyId);
        return true;
    }
}

