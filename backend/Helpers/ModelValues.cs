namespace Luggage;

public static class ModelValues
{
    public static string ToValue(this LuggageStage value) => value switch
    {
        LuggageStage.Checkin => "checkin",
        LuggageStage.Security => "security",
        LuggageStage.Sorted => "sorted",
        LuggageStage.Loaded => "loaded",
        LuggageStage.Arrived => "arrived",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    public static LuggageStage ParseStage(string value) => value switch
    {
        "checkin" => LuggageStage.Checkin,
        "security" => LuggageStage.Security,
        "sorted" => LuggageStage.Sorted,
        "loaded" => LuggageStage.Loaded,
        "arrived" => LuggageStage.Arrived,
        _ => throw new ArgumentException("Unknown luggage stage")
    };

    public static string ToValue(this EventType value) => value switch
    {
        EventType.Luggage => "luggage",
        EventType.Departure => "departure",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    public static EventType ParseEventType(string value) => value switch
    {
        "luggage" => EventType.Luggage,
        "departure" => EventType.Departure,
        _ => throw new ArgumentException("Unsupported eventType")
    };

    public static string ToValue(this DeviceType value) => value switch
    {
        DeviceType.Scanner => "scanner",
        DeviceType.Gate => "gate",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    public static DeviceType ParseDeviceType(string value) => value switch
    {
        "scanner" => DeviceType.Scanner,
        "gate" => DeviceType.Gate,
        _ => throw new ArgumentException("Unknown device type")
    };

    public static string ToValue(this ObservationOutcome value) => value switch
    {
        ObservationOutcome.Accepted => "accepted",
        ObservationOutcome.HistoricalSequence => "historical-sequence",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    public static ObservationOutcome ParseOutcome(string value) => value switch
    {
        "accepted" => ObservationOutcome.Accepted,
        "historical-sequence" => ObservationOutcome.HistoricalSequence,
        _ => throw new ArgumentException("Unknown observation outcome")
    };
}
