using TrelloCli.Models;

namespace TrelloCli.Services;

public partial class TrelloApiService
{
    public Task<ApiResponse<List<Board>>> GetBoardsAsync() =>
        GetListAsync<Board>(BuildUrl("/members/me/boards", "filter=open"), notFoundMessage: null);

    public Task<ApiResponse<Board>> GetBoardAsync(string boardId) =>
        GetObjectAsync<Board>(BuildUrl($"/boards/{boardId}"), "Board not found", "NOT_FOUND", "Board not found");
}
