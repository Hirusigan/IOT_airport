namespace Luggage;

public class Airport
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double Latitude
    {
        get; set;
    }
    public double Longitude
    {
        get; set;
    }
    public string RegionId { get; set; } = "EU";
    public bool Enabled { get; set; } = true;
    public DateTimeOffset? LastSeenAt
    {
        get; set;
    }
}
