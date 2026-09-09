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

    public Task<ApiResponse<Board>> CreateBoardAsync(
        string name,
        string? desc,
        string? organizationId,
        bool? defaultLists,
        string? permissionLevel)
    {
        var formData = new Dictionary<string, string> { ["name"] = name };
        if (!string.IsNullOrEmpty(desc)) formData["desc"] = desc;
        if (!string.IsNullOrEmpty(organizationId)) formData["idOrganization"] = organizationId;
        if (defaultLists is { } lists) formData["defaultLists"] = lists ? "true" : "false";
        if (!string.IsNullOrEmpty(permissionLevel)) formData["prefs_permissionLevel"] = permissionLevel;

        return SendForObjectAsync<Board>(
            HttpMethod.Post,
            BuildUrl("/boards"),
            new FormUrlEncodedContent(formData),
            "Failed to create board",
            "CREATE_FAILED",
            notFoundMessage: null);
    }

    public Task<ApiResponse<Board>> UpdateBoardAsync(string boardId, string? name, string? desc, string? permissionLevel)
    {
        var formData = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(name)) formData["name"] = name;
        if (desc != null) formData["desc"] = desc;
        // Nested preference, unlike the flat prefs_ form that board creation takes.
        if (!string.IsNullOrEmpty(permissionLevel)) formData["prefs/permissionLevel"] = permissionLevel;

        if (formData.Count == 0)
            return Task.FromResult(ApiResponse<Board>.Fail("No update parameters provided", "NO_PARAMS"));

        return SendForObjectAsync<Board>(
            HttpMethod.Put,
            BuildUrl($"/boards/{boardId}"),
            new FormUrlEncodedContent(formData),
            "Failed to update board",
            "UPDATE_FAILED",
            "Board not found");
    }

    // Closing is Trello's reversible alternative to deleting a board, which this CLI does not do.
    public Task<ApiResponse<Board>> SetBoardClosedAsync(string boardId, bool closed) =>
        SendForObjectAsync<Board>(
            HttpMethod.Put,
            BuildUrl($"/boards/{boardId}/closed"),
            new FormUrlEncodedContent(new Dictionary<string, string> { ["value"] = closed ? "true" : "false" }),
            closed ? "Failed to close board" : "Failed to reopen board",
            "UPDATE_FAILED",
            "Board not found");
}
