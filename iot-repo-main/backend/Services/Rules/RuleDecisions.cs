namespace Luggage;
public static class RuleDecisions
{
    public static bool Loaded(Assignment a, Flight f) => a.LoadedAt is { } loaded && loaded <= (f.ActualDeparture ?? f.ScheduledDeparture);
    public static bool AtRisk(Assignment a, Flight f, DateTimeOffset now, RuleSettings settings) =>
        !Loaded(a, f) && f.ActualDeparture == null && now >= f.ScheduledDeparture.AddSeconds(-settings.LoadingCutoffSeconds);
    public static bool Missed(Assignment a, Flight f, DateTimeOffset now, RuleSettings settings) =>
        !Loaded(a, f) && f.ActualDeparture is { } departure && now >= departure.AddSeconds(settings.GraceSeconds);
    public static bool Unexpected(string airport, string stage, DateTimeOffset at, Flight flight) =>
        stage == "arrived" ? airport != flight.Destination || at < (flight.ActualDeparture ?? flight.ScheduledDeparture) : airport != flight.Origin;
}

