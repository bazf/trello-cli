using TrelloCli.Models;

namespace TrelloCli.Services;

public partial class TrelloApiService
{
    public Task<ApiResponse<List<Checklist>>> GetChecklistsAsync(string cardId) =>
        GetListAsync<Checklist>(BuildUrl($"/cards/{cardId}/checklists"), "Card not found");

    public Task<ApiResponse<Checklist>> CreateChecklistAsync(string cardId, string name) =>
        SendForObjectAsync<Checklist>(
            HttpMethod.Post,
            BuildUrl("/checklists", $"idCard={cardId}&name={Uri.EscapeDataString(name)}"),
            content: null,
            "Failed to create checklist",
            "CREATE_FAILED",
            "Card not found");

    public Task<ApiResponse<bool>> DeleteChecklistAsync(string checklistId) =>
        SendForSuccessAsync(HttpMethod.Delete, BuildUrl($"/checklists/{checklistId}"), "Checklist not found");

    public Task<ApiResponse<ChecklistItem>> AddChecklistItemAsync(string checklistId, string name) =>
        SendForObjectAsync<ChecklistItem>(
            HttpMethod.Post,
            BuildUrl($"/checklists/{checklistId}/checkItems", $"name={Uri.EscapeDataString(name)}"),
            content: null,
            "Failed to add checklist item",
            "CREATE_FAILED",
            "Checklist not found");

    // Takes a card ID, unlike the add and delete item calls which take a checklist ID.
    public Task<ApiResponse<ChecklistItem>> UpdateChecklistItemAsync(string cardId, string checkItemId, string state) =>
        PatchChecklistItemAsync(cardId, checkItemId, new Dictionary<string, string> { ["state"] = state });

    public Task<ApiResponse<ChecklistItem>> RenameChecklistItemAsync(string cardId, string checkItemId, string name) =>
        PatchChecklistItemAsync(cardId, checkItemId, new Dictionary<string, string> { ["name"] = name });

    public Task<ApiResponse<ChecklistItem>> MoveChecklistItemAsync(string cardId, string checkItemId, string pos) =>
        PatchChecklistItemAsync(cardId, checkItemId, new Dictionary<string, string> { ["pos"] = pos });

    private Task<ApiResponse<ChecklistItem>> PatchChecklistItemAsync(
        string cardId,
        string checkItemId,
        Dictionary<string, string> formData) =>
        SendForObjectAsync<ChecklistItem>(
            HttpMethod.Put,
            BuildUrl($"/cards/{cardId}/checkItem/{checkItemId}"),
            new FormUrlEncodedContent(formData),
            "Failed to update checklist item",
            "UPDATE_FAILED",
            "Card or checklist item not found");

    public Task<ApiResponse<Checklist>> UpdateChecklistAsync(string checklistId, string? name, string? pos)
    {
        var formData = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(name)) formData["name"] = name;
        if (!string.IsNullOrEmpty(pos)) formData["pos"] = pos;

        if (formData.Count == 0)
            return Task.FromResult(ApiResponse<Checklist>.Fail("No update parameters provided", "NO_PARAMS"));

        return SendForObjectAsync<Checklist>(
            HttpMethod.Put,
            BuildUrl($"/checklists/{checklistId}"),
            new FormUrlEncodedContent(formData),
            "Failed to update checklist",
            "UPDATE_FAILED",
            "Checklist not found");
    }

    public Task<ApiResponse<bool>> DeleteChecklistItemAsync(string checklistId, string checkItemId) =>
        SendForSuccessAsync(
            HttpMethod.Delete,
            BuildUrl($"/checklists/{checklistId}/checkItems/{checkItemId}"),
            "Checklist or item not found");
}
