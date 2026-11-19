namespace Luggage;

public static class AlertValues
{
    public static string ToValue(this AlertType type) => type switch
    {
        AlertType.AtRisk => "at-risk",
        AlertType.MissedFlight => "missed-flight",
        AlertType.WrongFlight => "wrong-flight",
        AlertType.UnexpectedAirport => "unexpected-airport",
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
    public static AlertType ParseType(string value) => value switch
    {
        "at-risk" => AlertType.AtRisk,
        "missed-flight" => AlertType.MissedFlight,
        "wrong-flight" => AlertType.WrongFlight,
        "unexpected-airport" => AlertType.UnexpectedAirport,
        _ => throw new ArgumentException("Unknown alert type")
    };
    public static string ToValue(this AlertSeverity severity) => severity switch
    {
        AlertSeverity.Orange => "orange",
        AlertSeverity.Red => "red",
        _ => throw new ArgumentOutOfRangeException(nameof(severity))
    };
    public static AlertSeverity ParseSeverity(string value) => value switch
    {
        "orange" => AlertSeverity.Orange,
        "red" => AlertSeverity.Red,
        _ => throw new ArgumentException("Unknown alert severity")
    };
}
