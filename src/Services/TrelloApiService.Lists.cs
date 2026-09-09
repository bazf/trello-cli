using TrelloCli.Models;

namespace TrelloCli.Services;

public partial class TrelloApiService
{
    public Task<ApiResponse<List<TrelloList>>> GetListsAsync(string boardId, string? filter = null)
    {
        var selected = string.IsNullOrEmpty(filter) ? "open" : filter;
        return GetListAsync<TrelloList>(BuildUrl($"/boards/{boardId}/lists", $"filter={selected}"), "Board not found");
    }

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

    public Task<ApiResponse<TrelloList>> UpdateListAsync(string listId, string? name, string? pos)
    {
        var formData = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(name)) formData["name"] = name;
        if (!string.IsNullOrEmpty(pos)) formData["pos"] = pos;

        if (formData.Count == 0)
            return Task.FromResult(ApiResponse<TrelloList>.Fail("No update parameters provided", "NO_PARAMS"));

        return SendForObjectAsync<TrelloList>(
            HttpMethod.Put,
            BuildUrl($"/lists/{listId}"),
            new FormUrlEncodedContent(formData),
            "Failed to update list",
            "UPDATE_FAILED",
            "List not found");
    }

    // Archiving a list leaves its cards intact; they come back with the list.
    public Task<ApiResponse<TrelloList>> SetListClosedAsync(string listId, bool closed) =>
        SendForObjectAsync<TrelloList>(
            HttpMethod.Put,
            BuildUrl($"/lists/{listId}/closed"),
            new FormUrlEncodedContent(new Dictionary<string, string> { ["value"] = closed ? "true" : "false" }),
            closed ? "Failed to archive list" : "Failed to restore list",
            "UPDATE_FAILED",
            "List not found");

    public Task<ApiResponse<List<Card>>> ArchiveAllCardsAsync(string listId) =>
        SendForListAsync<Card>(
            HttpMethod.Post,
            BuildUrl($"/lists/{listId}/archiveAllCards"),
            content: null,
            "List not found");

    public async Task<ApiResponse<List<Card>>> MoveAllCardsAsync(string sourceListId, string targetListId)
    {
        // Trello wants the destination board alongside the destination list, and the caller
        // only has list IDs, so resolve it here rather than making them pass it.
        var target = await GetObjectAsync<TrelloList>(
            BuildUrl($"/lists/{targetListId}"),
            "Target list not found",
            "NOT_FOUND",
            "Target list not found");

        if (!target.Ok || target.Data is null)
            return ApiResponse<List<Card>>.Fail(target.Error ?? "Target list not found", target.Code ?? "NOT_FOUND");

        return await SendForListAsync<Card>(
            HttpMethod.Post,
            BuildUrl($"/lists/{sourceListId}/moveAllCards",
                $"idBoard={target.Data.BoardId}&idList={targetListId}"),
            content: null,
            "List not found");
    }
}
