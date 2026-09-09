using TrelloCli.Models;

namespace TrelloCli.Services;

public partial class TrelloApiService
{
    public Task<ApiResponse<List<TrelloList>>> GetListsAsync(string boardId) =>
        GetListAsync<TrelloList>(BuildUrl($"/boards/{boardId}/lists", "filter=open"), "Board not found");

    public Task<ApiResponse<TrelloList>> MoveListAsync(string listId, string pos) =>
        SendForObjectAsync<TrelloList>(
            HttpMethod.Put,
            BuildUrl($"/lists/{listId}"),
            new FormUrlEncodedContent(new Dictionary<string, string> { ["pos"] = pos }),
            "Failed to move list",
            "UPDATE_FAILED",
            "List not found");

    public Task<ApiResponse<TrelloList>> CreateListAsync(string boardId, string name) =>
        SendForObjectAsync<TrelloList>(
            HttpMethod.Post,
            BuildUrl("/lists", $"name={Uri.EscapeDataString(name)}&idBoard={boardId}"),
            content: null,
            "Failed to create list",
            "CREATE_FAILED",
            notFoundMessage: null);
}
