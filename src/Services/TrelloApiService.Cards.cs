using TrelloCli.Models;

namespace TrelloCli.Services;

public partial class TrelloApiService
{
    public Task<ApiResponse<List<Card>>> GetCardsInListAsync(string listId) =>
        GetListAsync<Card>(BuildUrl($"/lists/{listId}/cards"), "List not found");

    public Task<ApiResponse<List<Card>>> GetCardsInBoardAsync(string boardId) =>
        GetListAsync<Card>(BuildUrl($"/boards/{boardId}/cards", "filter=open"), "Board not found");

    public Task<ApiResponse<Card>> GetCardAsync(string cardId) =>
        GetObjectAsync<Card>(BuildUrl($"/cards/{cardId}"), "Card not found", "NOT_FOUND", "Card not found");

    public Task<ApiResponse<Card>> CreateCardAsync(string listId, string name, string? desc = null,
        string? due = null, string? labels = null, string? members = null)
    {
        var formData = new Dictionary<string, string>
        {
            ["idList"] = listId,
            ["name"] = name
        };
        if (!string.IsNullOrEmpty(desc)) formData["desc"] = desc;
        if (!string.IsNullOrEmpty(due)) formData["due"] = due;
        if (!string.IsNullOrEmpty(labels)) formData["idLabels"] = labels;
        if (!string.IsNullOrEmpty(members)) formData["idMembers"] = members;

        return SendForObjectAsync<Card>(
            HttpMethod.Post,
            BuildUrl("/cards"),
            new FormUrlEncodedContent(formData),
            "Failed to create card",
            "CREATE_FAILED",
            notFoundMessage: null);
    }

    public Task<ApiResponse<Card>> UpdateCardAsync(string cardId, string? name = null, string? desc = null,
        string? due = null, string? listId = null, string? labels = null, string? members = null, bool? closed = null)
    {
        var formData = new Dictionary<string, string>();
        // An empty name is ignored, while an empty desc or due clears the field. That asymmetry
        // is documented behaviour: a card cannot be nameless.
        if (!string.IsNullOrEmpty(name)) formData["name"] = name;
        if (desc != null) formData["desc"] = desc;
        if (due != null) formData["due"] = due;
        if (!string.IsNullOrEmpty(listId)) formData["idList"] = listId;
        if (labels != null) formData["idLabels"] = labels;
        if (members != null) formData["idMembers"] = members;
        if (closed.HasValue) formData["closed"] = closed.Value.ToString().ToLower();

        if (formData.Count == 0)
            return Task.FromResult(ApiResponse<Card>.Fail("No update parameters provided", "NO_PARAMS"));

        return SendForObjectAsync<Card>(
            HttpMethod.Put,
            BuildUrl($"/cards/{cardId}"),
            new FormUrlEncodedContent(formData),
            "Failed to update card",
            "UPDATE_FAILED",
            "Card not found");
    }

    public Task<ApiResponse<Card>> MoveCardAsync(string cardId, string listId) =>
        UpdateCardAsync(cardId, listId: listId);

    public Task<ApiResponse<bool>> DeleteCardAsync(string cardId) =>
        SendForSuccessAsync(HttpMethod.Delete, BuildUrl($"/cards/{cardId}"), "Card not found");
}
