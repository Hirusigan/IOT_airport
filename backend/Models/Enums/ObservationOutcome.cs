namespace Luggage;

public enum ObservationOutcome
{
    Accepted,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("historical-sequence")]
    HistoricalSequence
}
