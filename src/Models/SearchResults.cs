using System.Text.Json.Serialization;

namespace TrelloCli.Models;

/// <summary>
/// What Trello's search endpoint returns. Each collection is present only when the
/// corresponding model type was requested, so all of them default to empty.
/// </summary>
public class SearchResults
{
    [JsonPropertyName("cards")]
    public List<Card> Cards { get; set; } = new();

    [JsonPropertyName("boards")]
    public List<Board> Boards { get; set; } = new();

    [JsonPropertyName("members")]
    public List<Member> Members { get; set; } = new();
}
