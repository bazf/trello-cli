using TrelloCli.Models;

namespace TrelloCli.Services;

public partial class TrelloApiService
{
    public Task<ApiResponse<List<CustomField>>> GetCustomFieldsAsync(string boardId) =>
        GetListAsync<CustomField>(BuildUrl($"/boards/{boardId}/customFields"), "Board not found");

    public Task<ApiResponse<List<CustomFieldItem>>> GetCardCustomFieldsAsync(string cardId) =>
        GetListAsync<CustomFieldItem>(BuildUrl($"/cards/{cardId}/customFieldItems"), "Card not found");

    /// <summary>
    /// Sets a custom field on a card. Trello wants the value wrapped in a key naming the field's
    /// type, so the field is read first to learn it: asking the caller to state the type as well
    /// as the value would just be a second thing to get wrong.
    /// </summary>
    public async Task<ApiResponse<CustomFieldItem>> SetCustomFieldAsync(
        string cardId,
        string fieldId,
        string? value,
        string? optionId)
    {
        if (!string.IsNullOrEmpty(optionId))
            return await WriteCustomFieldAsync(cardId, fieldId, new Dictionary<string, object?> { ["idValue"] = optionId });

        if (value is null)
            return ApiResponse<CustomFieldItem>.Fail("Provide --value or --option", "NO_PARAMS");

        var field = await GetCustomFieldAsync(fieldId);
        if (!field.Ok || field.Data is null)
            return ApiResponse<CustomFieldItem>.Fail(field.Error ?? "Custom field not found", field.Code ?? "NOT_FOUND");

        if (field.Data.Type == "list")
        {
            return ApiResponse<CustomFieldItem>.Fail(
                $"'{field.Data.Name}' is a list field; pass --option with an option id from --get-custom-fields",
                "INVALID_PARAM");
        }

        var valueKey = ValueKeyFor(field.Data.Type);
        if (valueKey is null)
            return ApiResponse<CustomFieldItem>.Fail($"Unsupported custom field type: {field.Data.Type}", "INVALID_PARAM");

        return await WriteCustomFieldAsync(cardId, fieldId, new Dictionary<string, object?>
        {
            ["value"] = new Dictionary<string, string> { [valueKey] = value }
        });
    }

    public async Task<ApiResponse<CustomFieldItem>> ClearCustomFieldAsync(string cardId, string fieldId)
    {
        var field = await GetCustomFieldAsync(fieldId);
        if (!field.Ok || field.Data is null)
            return ApiResponse<CustomFieldItem>.Fail(field.Error ?? "Custom field not found", field.Code ?? "NOT_FOUND");

        // A list field clears by unsetting the chosen option; the others clear by empty value.
        var body = field.Data.Type == "list"
            ? new Dictionary<string, object?> { ["idValue"] = null }
            : new Dictionary<string, object?> { ["value"] = string.Empty };

        return await WriteCustomFieldAsync(cardId, fieldId, body);
    }

    private Task<ApiResponse<CustomField>> GetCustomFieldAsync(string fieldId) =>
        GetObjectAsync<CustomField>(
            BuildUrl($"/customFields/{fieldId}"),
            "Custom field not found",
            "NOT_FOUND",
            "Custom field not found");

    private Task<ApiResponse<CustomFieldItem>> WriteCustomFieldAsync(
        string cardId,
        string fieldId,
        Dictionary<string, object?> body) =>
        SendJsonAsync<CustomFieldItem>(
            HttpMethod.Put,
            BuildUrl($"/cards/{cardId}/customField/{fieldId}/item"),
            body,
            "Failed to set custom field",
            "UPDATE_FAILED",
            "Card or custom field not found");

    private static string? ValueKeyFor(string type) => type switch
    {
        "text" => "text",
        "number" => "number",
        "date" => "date",
        "checkbox" => "checked",
        _ => null
    };
}
