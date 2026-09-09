using System.Text.Json;
using System.Text.Json.Serialization;

namespace TrelloCli.Utils;

public static class OutputFormatter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string ToJson<T>(T obj)
    {
        return JsonSerializer.Serialize(obj, Options);
    }
}
