namespace Luggage;
public record LuggageDTO(IReadOnlyList<Journey> Journeys, IReadOnlyList<LuggageState> States,
    IReadOnlyList<Assignment> Assignments, IReadOnlyList<Observation> Events, IReadOnlyList<Flight> Flights,
    IReadOnlyList<TripDTO> Trips);
