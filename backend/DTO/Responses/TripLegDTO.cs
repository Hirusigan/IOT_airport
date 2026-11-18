namespace Luggage;

public record TripLegDTO(string JourneyId, int Order, Flight Flight, DateTimeOffset? LoadedAt);
