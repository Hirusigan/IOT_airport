namespace Luggage;
public record TripDTO(string Id, string RunId, string LuggageId, IReadOnlyList<TripLegDTO> Legs);
