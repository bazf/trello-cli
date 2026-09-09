using TrelloCli.Models;

namespace TrelloCli.Services;

public partial class TrelloApiService
{
    public Task<ApiResponse<List<Label>>> GetLabelsAsync(string boardId) =>
        GetListAsync<Label>(BuildUrl($"/boards/{boardId}/labels", "limit=1000"), "Board not found");

    public Task<ApiResponse<Label>> CreateLabelAsync(string boardId, string name, string? color) =>
        SendForObjectAsync<Label>(
            HttpMethod.Post,
            BuildUrl("/labels"),
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["idBoard"] = boardId,
                ["name"] = name,
                ["color"] = color ?? string.Empty
            }),
            "Failed to create label",
            "CREATE_FAILED",
            notFoundMessage: null);

    public Task<ApiResponse<Label>> UpdateLabelAsync(string labelId, string? name, string? color)
    {
        var formData = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(name)) formData["name"] = name;
        if (color != null) formData["color"] = color;

        if (formData.Count == 0)
            return Task.FromResult(ApiResponse<Label>.Fail("No update parameters provided", "NO_PARAMS"));

        return SendForObjectAsync<Label>(
            HttpMethod.Put,
            BuildUrl($"/labels/{labelId}"),
            new FormUrlEncodedContent(formData),
            "Failed to update label",
            "UPDATE_FAILED",
            "Label not found");
    }

    public Task<ApiResponse<bool>> DeleteLabelAsync(string labelId) =>
        SendForSuccessAsync(HttpMethod.Delete, BuildUrl($"/labels/{labelId}"), "Label not found");
}
