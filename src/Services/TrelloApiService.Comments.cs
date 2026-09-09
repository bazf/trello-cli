using TrelloCli.Models;

namespace TrelloCli.Services;

public partial class TrelloApiService
{
    public Task<ApiResponse<List<Comment>>> GetCommentsAsync(string cardId) =>
        GetListAsync<Comment>(BuildUrl($"/cards/{cardId}/actions", "filter=commentCard"), "Card not found");

    public Task<ApiResponse<Comment>> AddCommentAsync(string cardId, string text) =>
        SendForObjectAsync<Comment>(
            HttpMethod.Post,
            BuildUrl($"/cards/{cardId}/actions/comments", $"text={Uri.EscapeDataString(text)}"),
            content: null,
            "Failed to add comment",
            "CREATE_FAILED",
            "Card not found");
}
