using TrelloCli.Models;
using TrelloCli.Services;

namespace TrelloCli.Commands;

public class SearchCommands(TrelloApiService api, TextWriter output)
    : CommandsBase(api, output)
{
    public async Task SearchAsync(string query, string? boardId, string? limit, bool cardsOnly)
    {
        if (string.IsNullOrEmpty(query))
        {
            Write(ApiResponse<object>.Fail("Search query required", "MISSING_PARAM"));
            return;
        }

        if (!TryParseLimit(limit, out var parsed)) return;

        Write(await Api.SearchAsync(query, boardId, parsed, cardsOnly));
    }

    public async Task SearchMembersAsync(string query, string? limit)
    {
        if (string.IsNullOrEmpty(query))
        {
            Write(ApiResponse<object>.Fail("Search query required", "MISSING_PARAM"));
            return;
        }

        if (!TryParseLimit(limit, out var parsed)) return;

        Write(await Api.SearchMembersAsync(query, parsed));
    }

    private bool TryParseLimit(string? limit, out int? parsed)
    {
        parsed = null;
        if (string.IsNullOrEmpty(limit)) return true;

        if (!int.TryParse(limit, out var value) || value <= 0)
        {
            Write(ApiResponse<object>.Fail("--limit must be a positive whole number", "INVALID_PARAM"));
            return false;
        }

        parsed = value;
        return true;
    }
}
