using System.Text.Json;
namespace Luggage;

public static class JsonContract
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    public static T Read<T>(string raw) => JsonSerializer.Deserialize<T>(raw, Options) ?? throw new ArgumentException("Empty JSON");
    public static T Data<T>(JsonElement data) => data.Deserialize<T>(Options) ?? throw new ArgumentException("Missing data");
}

