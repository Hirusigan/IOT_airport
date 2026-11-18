
namespace Luggage;

public class EventValidationService(IAirportRepository airports, IDeviceRepository devices, IJourneyRepository journeys, IFlightRepository flights, TimeProvider clock, ILuggageStateRepository states, IAssignmentRepository assignments) : IEventValidationService
{
    public async Task Validate(string topic, Envelope e, string region)
    {
        var parts = topic.Split('/');
        if (parts.Length != 5 || parts[0] != "airport" || parts[1] != e.AirportId || parts[3] != e.DeviceId || parts[4] != e.EventType)
            throw new ArgumentException("Topic/payload identity mismatch");
        if (e.SchemaVersion != 1 || !Guid.TryParse(e.EventId, out _) || string.IsNullOrWhiteSpace(e.SimulationRunId) ||
            e.OccurredAt == default || e.OccurredAt.Offset != TimeSpan.Zero || e.OccurredAt > clock.GetUtcNow().AddSeconds(5))
            throw new ArgumentException("Invalid envelope/version/UTC occurrence time");
        var airport = await airports.GetAsync(e.AirportId);
        var device = await devices.GetAsync(e.AirportId, e.DeviceId);
        if (airport is not { Enabled: true } || airport.RegionId != region || device is not { Enabled: true } || device.Type != parts[2])
            throw new ArgumentException("Unknown/disabled source or incorrect region/device type");
        if (e.EventType == "luggage")
        {
            if (parts[2] != "scanner") throw new ArgumentException("Luggage requires a scanner");
            var b = JsonContract.Data<BagData>(e.Data);
            if (b.Sequence <= 0 || b.LegOrder < 1 || b.Stage is not ("checkin" or "security" or "sorted" or "loaded" or "arrived"))
                throw new ArgumentException("Invalid sequence, leg order or stage");
            var journey = await journeys.GetAsync(b.JourneyId);
            if (journey == null)
            {
                if (b.Stage != "checkin" || string.IsNullOrWhiteSpace(b.FlightId))
                    throw new ArgumentException("A first check-in must identify its planned flight");
                var plannedFlight = await flights.GetAsync(b.FlightId);
                if (plannedFlight == null || plannedFlight.RunId != e.SimulationRunId)
                    throw new ArgumentException("Unknown flight/run");
            }
            else if (journey.LuggageId != b.LuggageId || journey.RunId != e.SimulationRunId)
                throw new ArgumentException("Unknown journey/luggage/run");
            var tripId = journey?.TripId ?? b.TripId ?? b.JourneyId;
            var legOrder = journey?.LegOrder ?? b.LegOrder;
            if (legOrder > 1)
            {
                var previous = await journeys.GetPreviousLegAsync(tripId, legOrder)
                    ?? throw new ArgumentException("Missing preceding trip leg");
                var previousState = await states.GetAsync(previous.Id);
                var previousAssignment = await assignments.GetByJourneyAsync(previous.Id);
                var previousFlight = await flights.GetAsync(previousAssignment.FlightId);
                if (previousState?.Stage != "arrived" || previousFlight?.ActualDeparture == null ||
                    previousAssignment.LoadedAt == null || previousAssignment.LoadedAt > previousFlight.ActualDeparture ||
                    previousState.AirportId != previousFlight.Destination || previousState.OccurredAt < previousFlight.ActualDeparture ||
                    e.OccurredAt < previousState.OccurredAt)
                    throw new ArgumentException("Transfer requires confirmed arrival of the preceding leg");
            }
            if (b.Stage == "loaded" && string.IsNullOrWhiteSpace(b.FlightId)) throw new ArgumentException("Loading needs flightId");
            if (b.FlightId != null && !await flights.ExistsInRunAsync(b.FlightId, e.SimulationRunId))
                throw new ArgumentException("Unknown flight/run");
        }
        else if (e.EventType == "departure")
        {
            if (parts[2] != "gate") throw new ArgumentException("Departure requires a gate");
            var d = JsonContract.Data<DepartureData>(e.Data);
            var flight = await flights.GetAsync(d.FlightId);
            if (flight == null || flight.Origin != e.AirportId || flight.RunId != e.SimulationRunId ||
                d.DepartedAt == default || d.DepartedAt.Offset != TimeSpan.Zero || d.DepartedAt != e.OccurredAt)
                throw new ArgumentException("Invalid departure flight/time/run");
        }
        else throw new ArgumentException("Unsupported eventType");
    }
}

