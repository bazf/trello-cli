using System.Net;
using System.Text.Json;
using TrelloCli.Models;

namespace TrelloCli.Services;

public partial class TrelloApiService
{
    /// <summary>
    /// Trello-side search, so an agent can find a card by words in it rather than needing an ID
    /// it does not have yet.
    /// </summary>
    public async Task<ApiResponse<SearchResults>> SearchAsync(string query, string? boardId, int? limit, bool cardsOnly)
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
            // Search is the one endpoint that rejects the short link out of a board URL: idBoards
            // takes full ids only, and answers anything else with a bare 400. Every other command
            // accepts either, so resolve it here rather than making --board the odd one out.
            var resolved = await ResolveBoardIdAsync(boardId);
            if (!resolved.Ok || resolved.Data is null)
                return ApiResponse<SearchResults>.Fail(resolved.Error ?? "Board not found", resolved.Code ?? "NOT_FOUND");

            parameters.Add($"idBoards={Uri.EscapeDataString(resolved.Data)}");
        }

        if (limit is { } count)
        {
            var bounded = Math.Clamp(count, 1, 1000);
            parameters.Add($"cards_limit={bounded}");
            parameters.Add($"boards_limit={bounded}");
        }

        return await GetObjectAsync<SearchResults>(
            BuildUrl("/search", string.Join('&', parameters)),
            "Search returned no result document",
            "NOT_FOUND",
            notFoundMessage: null);
    }

    /// <summary>
    /// Turns a board short link into its full id, leaving a full id untouched. Costs one extra
    /// request only when a short link is actually passed.
    /// </summary>
    private Task<ApiResponse<string>> ResolveBoardIdAsync(string idOrShortLink)
    {
        if (IsFullTrelloId(idOrShortLink))
            return Task.FromResult(ApiResponse<string>.Success(idOrShortLink));

        return ExecuteAsync<string>(async () =>
        {
            try
            {
                var body = await GetStringAsync(BuildUrl($"/boards/{idOrShortLink}", "fields=id"));
                var board = JsonSerializer.Deserialize<Board>(body);
                return board is not null ? ApiResponse<string>.Success(board.Id) : NoSuchBoard(idOrShortLink);
            }
            catch (HttpRequestException exception)
                when (exception.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound)
            {
                // Trello answers a malformed reference with 400 and an unknown one with 404.
                // To someone who passed the wrong thing on the command line they mean the same.
                return NoSuchBoard(idOrShortLink);
            }
        });
    }

    private static ApiResponse<string> NoSuchBoard(string reference) => ApiResponse<string>.Fail(
        $"No board matches '{reference}'. Pass a board id, or the short link from the board URL.",
        "NOT_FOUND");

    private static bool IsFullTrelloId(string value) =>
        value.Length == 24 && value.All(Uri.IsHexDigit);

    public Task<ApiResponse<List<Member>>> SearchMembersAsync(string query, int? limit)
    {
        var parameters = new List<string> { $"query={Uri.EscapeDataString(query)}" };
        if (limit is { } count) parameters.Add($"limit={Math.Clamp(count, 1, 20)}");

        return GetListAsync<Member>(BuildUrl("/search/members", string.Join('&', parameters)), notFoundMessage: null);
    }
}
