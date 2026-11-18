namespace Luggage;

public class Airport
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string RegionId { get; set; } = "EU";
    public bool Enabled { get; set; } = true;
    public DateTimeOffset? LastSeenAt { get; set; }
}
