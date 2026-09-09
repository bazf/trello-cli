using TrelloCli.Models;

namespace TrelloCli.Services;

public partial class TrelloApiService
{
    public Task<ApiResponse<List<Board>>> GetBoardsAsync(string? filter = null)
    {
        var selected = string.IsNullOrEmpty(filter) ? "open" : filter;
        return GetListAsync<Board>(BuildUrl("/members/me/boards", $"filter={selected}"), notFoundMessage: null);
    }

    public Task<ApiResponse<Board>> GetBoardAsync(string boardId) =>
        GetObjectAsync<Board>(BuildUrl($"/boards/{boardId}"), "Board not found", "NOT_FOUND", "Board not found");
}
