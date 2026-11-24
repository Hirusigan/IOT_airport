namespace Luggage;

public class EventValidationService(
    IAirportRepository airports,
    IDeviceRepository devices,
    IJourneyRepository journeys,
    IFlightRepository flights,
    TimeProvider clock,
    ILuggageStateRepository states,
    IAssignmentRepository assignments) : IEventValidationService
{
    public async Task Validate(string topic, Envelope e, string region)
    {
        if (e.EventType != "luggage")
        {
            throw new ArgumentException("MQTT events must be luggage scans");
        }

        var deviceType = ValidateTopic(topic, e);
        ValidateEnvelope(e);
        await ValidateSource(e, region, deviceType);
        await ValidateLuggage(e, deviceType);
    }

    private static string ValidateTopic(string topic, Envelope e)
    {
        var parts = topic.Split('/');
        if (parts.Length != 5 || parts[0] != "airport" || parts[1] != e.AirportId || parts[3] != e.DeviceId || parts[4] != e.EventType)
        {
            throw new ArgumentException("Topic/payload identity mismatch");
        }

        return parts[2];
    }

    private void ValidateEnvelope(Envelope e)
    {
        if (e.SchemaVersion != 1 || !Guid.TryParse(e.EventId, out _) || string.IsNullOrWhiteSpace(e.SimulationRunId) ||
            e.OccurredAt == default || e.OccurredAt.Offset != TimeSpan.Zero || e.OccurredAt > clock.GetUtcNow().AddSeconds(5))
        {
            throw new ArgumentException("Invalid envelope/version/UTC occurrence time");
        }
    }

    private async Task ValidateSource(Envelope e, string region, string deviceType)
    {
        var airport = await airports.GetAsync(e.AirportId);
        var device = await devices.GetAsync(e.AirportId, e.DeviceId);
        if (airport is not { Enabled: true } || airport.RegionId != region || device is not { Enabled: true } || device.Type.ToValue() != deviceType)
        {
            throw new ArgumentException("Unknown/disabled source or incorrect region/device type");
        }
    }

    private async Task ValidateLuggage(Envelope e, string deviceType)
    {
        if (ModelValues.ParseDeviceType(deviceType) != DeviceType.Scanner)
        {
            throw new ArgumentException("Luggage requires a scanner");
        }

        var bag = JsonContract.Data<BagData>(e.Data);
        var stage = ValidateBagData(bag);
        var journey = await journeys.GetAsync(bag.JourneyId);
        await ValidateJourney(bag, stage, journey, e.SimulationRunId);

        var tripId = journey?.TripId ?? bag.TripId ?? bag.JourneyId;
        var legOrder = journey?.LegOrder ?? bag.LegOrder;
        if (legOrder > 1)
        {
            await ValidatePreviousLeg(tripId, legOrder, e.OccurredAt);
        }

        if (stage == LuggageStage.Loaded && string.IsNullOrWhiteSpace(bag.FlightId))
        {
            throw new ArgumentException("Loading needs flightId");
        }

        if (bag.FlightId != null && !await flights.ExistsInRunAsync(bag.FlightId, e.SimulationRunId))
        {
            throw new ArgumentException("Unknown flight/run");
        }
    }

    private static LuggageStage ValidateBagData(BagData bag)
    {
        if (bag.Sequence <= 0 || bag.LegOrder < 1)
        {
            throw new ArgumentException("Invalid sequence, leg order or stage");
        }

        try
        {
            return ModelValues.ParseStage(bag.Stage);
        }
        catch (ArgumentException)
        {
            throw new ArgumentException("Invalid sequence, leg order or stage");
        }
    }

    private async Task ValidateJourney(BagData bag, LuggageStage stage, Journey? journey, string runId)
    {
        if (journey == null)
        {
            if (stage != LuggageStage.Checkin || string.IsNullOrWhiteSpace(bag.FlightId))
            {
                throw new ArgumentException("A first check-in must identify its planned flight");
            }

            var plannedFlight = await flights.GetAsync(bag.FlightId);
            if (plannedFlight == null || plannedFlight.RunId != runId)
            {
                throw new ArgumentException("Unknown flight/run");
            }
        }
        else if (journey.LuggageId != bag.LuggageId || journey.RunId != runId)
        {
            throw new ArgumentException("Unknown journey/luggage/run");
        }
    }

    private async Task ValidatePreviousLeg(string tripId, int legOrder, DateTimeOffset occurredAt)
    {
        var previous = await journeys.GetPreviousLegAsync(tripId, legOrder)
            ?? throw new ArgumentException("Missing preceding trip leg");
        var previousState = await states.GetAsync(previous.Id);
        var previousAssignment = await assignments.GetByJourneyAsync(previous.Id);
        var previousFlight = await flights.GetAsync(previousAssignment.FlightId);

        if (previousState?.Stage != LuggageStage.Arrived || previousFlight?.ActualDeparture == null ||
            previousAssignment.LoadedAt == null || previousAssignment.LoadedAt > previousFlight.ActualDeparture ||
            previousState.AirportId != previousFlight.Destination || previousState.OccurredAt < previousFlight.ActualDeparture ||
            occurredAt < previousState.OccurredAt)
        {
            throw new ArgumentException("Transfer requires confirmed arrival of the preceding leg");
        }
    }
}
