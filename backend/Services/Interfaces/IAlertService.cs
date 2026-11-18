namespace Luggage;

public interface IAlertService { Task<IReadOnlyList<AlertDTO>> Get(string? airportId, bool? active, string? severity); }
