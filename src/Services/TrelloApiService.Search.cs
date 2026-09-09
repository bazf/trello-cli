using TrelloCli.Models;

namespace TrelloCli.Services;

public partial class TrelloApiService
{
    /// <summary>
    /// Trello-side search, so an agent can find a card by words in it rather than needing an ID
    /// it does not have yet.
    /// </summary>
    public Task<ApiResponse<SearchResults>> SearchAsync(string query, string? boardId, int? limit, bool cardsOnly)
    {
        var parameters = new List<string>
        {
            $"query={Uri.EscapeDataString(query)}",
            $"modelTypes={(cardsOnly ? "cards" : "cards,boards,members")}",
            // Trello matches whole words unless asked otherwise; partial matching is what a
            // caller searching for a fragment of a card title expects.
            "partial=true"
        };

        if (!string.IsNullOrEmpty(boardId))
        {
            parameters.Add($"idBoards={Uri.EscapeDataString(boardId)}");
        }

        if (limit is { } count)
        {
            var bounded = Math.Clamp(count, 1, 1000);
            parameters.Add($"cards_limit={bounded}");
            parameters.Add($"boards_limit={bounded}");
        }

        return GetObjectAsync<SearchResults>(
            BuildUrl("/search", string.Join('&', parameters)),
            "Search returned no result document",
            "NOT_FOUND",
            notFoundMessage: null);
    }

    public Task<ApiResponse<List<Member>>> SearchMembersAsync(string query, int? limit)
    {
        var parameters = new List<string> { $"query={Uri.EscapeDataString(query)}" };
        if (limit is { } count) parameters.Add($"limit={Math.Clamp(count, 1, 20)}");

        return GetListAsync<Member>(BuildUrl("/search/members", string.Join('&', parameters)), notFoundMessage: null);
    }
}
