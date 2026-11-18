namespace Luggage;

public static class AirportValidation
{
    public static bool IsValid(Airport a) => System.Text.RegularExpressions.Regex.IsMatch(a.Id ?? "", "^[A-Z]{3}$") &&
        !string.IsNullOrWhiteSpace(a.Name) && a.Name.Length <= 120 && double.IsFinite(a.Latitude) && double.IsFinite(a.Longitude) &&
        a.Latitude is >= -90 and <= 90 && a.Longitude is >= -180 and <= 180;
}
