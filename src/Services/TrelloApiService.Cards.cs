using System.Text.Json;
using TrelloCli.Models;

namespace TrelloCli.Services;

public partial class TrelloApiService
{
    public Task<ApiResponse<List<Card>>> GetCardsInListAsync(string listId) =>
        GetListAsync<Card>(BuildUrl($"/lists/{listId}/cards"), "List not found");

    public Task<ApiResponse<List<Card>>> GetCardsInBoardAsync(string boardId, string? filter = null)
    {
        var selected = string.IsNullOrEmpty(filter) ? "open" : filter;
        return GetListAsync<Card>(BuildUrl($"/boards/{boardId}/cards", $"filter={selected}"), "Board not found");
    }

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

    // Adds one label, in contrast to --labels on --update-card which replaces the whole set.
    public Task<ApiResponse<List<string>>> AddCardLabelAsync(string cardId, string labelId) =>
        SendForListAsync<string>(
            HttpMethod.Post,
            BuildUrl($"/cards/{cardId}/idLabels"),
            new FormUrlEncodedContent(new Dictionary<string, string> { ["value"] = labelId }),
            "Card or label not found");

    public Task<ApiResponse<List<string>>> RemoveCardLabelAsync(string cardId, string labelId) =>
        SendForListAsync<string>(
            HttpMethod.Delete,
            BuildUrl($"/cards/{cardId}/idLabels/{labelId}"),
            content: null,
            "Card or label not found");

    /// <summary>Applies a partial change to a card, used by the single-field setters below.</summary>
    private Task<ApiResponse<Card>> PatchCardAsync(string cardId, Dictionary<string, string> formData) =>
        SendForObjectAsync<Card>(
            HttpMethod.Put,
            BuildUrl($"/cards/{cardId}"),
            new FormUrlEncodedContent(formData),
            "Failed to update card",
            "UPDATE_FAILED",
            "Card not found");

    public Task<ApiResponse<Card>> SetCardPositionAsync(string cardId, string position) =>
        PatchCardAsync(cardId, new Dictionary<string, string> { ["pos"] = position });

    public Task<ApiResponse<Card>> SetDueCompleteAsync(string cardId, bool complete) =>
        PatchCardAsync(cardId, new Dictionary<string, string> { ["dueComplete"] = complete ? "true" : "false" });

    // An empty date clears the start date, mirroring how --due "" clears the due date.
    public Task<ApiResponse<Card>> SetStartDateAsync(string cardId, string start) =>
        PatchCardAsync(cardId, new Dictionary<string, string> { ["start"] = start });

    /// <summary>
    /// Trello takes the cover as a JSON object inside the form field rather than as separate
    /// form fields, so it is built here rather than by the caller.
    /// </summary>
    public Task<ApiResponse<Card>> SetCardCoverAsync(
        string cardId,
        string? color,
        string? attachmentId,
        string? size,
        string? brightness)
    {
        var cover = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(color)) cover["color"] = color;
        if (!string.IsNullOrEmpty(attachmentId)) cover["idAttachment"] = attachmentId;
        if (!string.IsNullOrEmpty(size)) cover["size"] = size;
        if (!string.IsNullOrEmpty(brightness)) cover["brightness"] = brightness;

        if (cover.Count == 0)
            return Task.FromResult(ApiResponse<Card>.Fail("No cover parameters provided", "NO_PARAMS"));

        return PatchCardAsync(cardId, new Dictionary<string, string>
        {
            ["cover"] = JsonSerializer.Serialize(cover)
        });
    }

    public Task<ApiResponse<Card>> ClearCardCoverAsync(string cardId) =>
        PatchCardAsync(cardId, new Dictionary<string, string> { ["cover"] = string.Empty });

    public Task<ApiResponse<Card>> CopyCardAsync(
        string sourceCardId,
        string targetListId,
        string? name,
        string? position,
        string? keep)
    {
        var formData = new Dictionary<string, string>
        {
            ["idCardSource"] = sourceCardId,
            ["idList"] = targetListId,
            // Without this Trello copies the name only, quietly dropping the description,
            // checklists, labels and members that make a copy worth making.
            ["keepFromSource"] = string.IsNullOrEmpty(keep) ? "all" : keep
        };
        if (!string.IsNullOrEmpty(name)) formData["name"] = name;
        if (!string.IsNullOrEmpty(position)) formData["pos"] = position;

        return SendForObjectAsync<Card>(
            HttpMethod.Post,
            BuildUrl("/cards"),
            new FormUrlEncodedContent(formData),
            "Failed to copy card",
            "CREATE_FAILED",
            "Card or list not found");
    }

    public Task<ApiResponse<List<CardAction>>> GetCardActivityAsync(string cardId, int? limit, string? filter)
    {
        var parameters = new List<string> { $"filter={(string.IsNullOrEmpty(filter) ? "all" : filter)}" };
        if (limit is { } count) parameters.Add($"limit={Math.Clamp(count, 1, 1000)}");

        return GetListAsync<CardAction>(
            BuildUrl($"/cards/{cardId}/actions", string.Join('&', parameters)),
            "Card not found");
    }
}
