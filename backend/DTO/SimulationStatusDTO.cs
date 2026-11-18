namespace Luggage;
public record SimulationStatusDTO(string State, string? ScenarioId = null, string? RunId = null,
    DateTimeOffset? StartedAt = null, int PublishedEvents = 0, int TotalEvents = 0, string? Error = null);
