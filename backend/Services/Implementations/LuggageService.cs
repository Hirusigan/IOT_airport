namespace Luggage;

public class LuggageService(
    IJourneyRepository journeys,
    ILuggageStateRepository states,
    IAssignmentRepository assignments,
    IEventRepository events,
    IFlightRepository flights) : ILuggageService
{
    public async Task<LuggageDTO?> Get(string id)
    {
        var matches = await journeys.GetByLuggageAsync(id);
        if (matches.Count == 0)
            return null;

        var ids = matches.Select(x => x.Id).ToArray();
        var assigned = await assignments.GetByJourneysAsync(ids);
        var history = await events.GetHistoryAsync(ids);

        var observedFlightIds = history.Select(x => JsonContract.Data<BagData>(JsonContract.Read<Envelope>(x.Payload).Data).FlightId)
            .OfType<string>();
        var relevantFlights = await flights.GetByIdsAsync(assigned.Select(x => x.FlightId).Concat(observedFlightIds).Distinct().ToArray());
        var flightById = relevantFlights.ToDictionary(x => x.Id);

        var assignmentByJourney = assigned.ToDictionary(x => x.JourneyId);

        var trips = matches.GroupBy(x => x.TripId).Select(group => new TripDTO(group.Key, group.First().RunId, id,
            group.OrderBy(x => x.LegOrder).Select(journey =>
            {
                var assignment = assignmentByJourney[journey.Id];
                return new TripLegDTO(journey.Id, journey.LegOrder, flightById[assignment.FlightId], assignment.LoadedAt);
            }).ToArray())).OrderByDescending(x => x.Legs[0].Flight.ScheduledDeparture).ToArray();

        return new(matches, await states.GetByJourneysAsync(ids), assigned, history, relevantFlights, trips);
    }
}
