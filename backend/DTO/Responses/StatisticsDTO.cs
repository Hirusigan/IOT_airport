namespace Luggage;

public record StatisticsDTO(int Airports, int Flights, int Journeys, int Events, int LoadedAssignments,
    int ActiveAlerts, int ResolvedAlerts, int InvalidMessages);
