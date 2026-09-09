using System.Text.Json;
using System.Text.Json.Serialization;

namespace TrelloCli.Models;

/// <summary>
/// An entry from a card's activity feed. Trello's <c>data</c> payload differs per action type,
/// so it is passed through as raw JSON rather than being forced into one shape.
/// </summary>
public class CardAction
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("date")]
    public string Date { get; set; } = string.Empty;

    [JsonPropertyName("memberCreator")]
    public CommentMember? MemberCreator { get; set; }

    [JsonPropertyName("data")]
    public JsonElement Data { get; set; }
}
