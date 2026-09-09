using System.Text.Json.Serialization;

namespace TrelloCli.Models;

/// <summary>A Trello workspace. The API calls them organizations.</summary>
public class Organization
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("desc")]
    public string? Desc { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("website")]
    public string? Website { get; set; }
}
