namespace Luggage;

public class Device
{
    public string Id { get; set; } = string.Empty;
    public string AirportId { get; set; } = string.Empty;
    public DeviceType Type
    {
        get; set;
    }
    public bool Enabled { get; set; } = true;
}
