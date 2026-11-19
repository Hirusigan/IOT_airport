using System.Text.Json.Serialization;

namespace Luggage;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SimulationState
{
    Idle,
    Starting,
    Running,
    Completed,
    Stopped,
    Failed,
    Interrupted
}
