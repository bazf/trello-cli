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

    // The comment ID is the ID of the commentCard action that --get-comments returns.
    public Task<ApiResponse<Comment>> UpdateCommentAsync(string cardId, string commentId, string text) =>
        SendForObjectAsync<Comment>(
            HttpMethod.Put,
            BuildUrl($"/cards/{cardId}/actions/{commentId}/comments", $"text={Uri.EscapeDataString(text)}"),
            content: null,
            "Failed to update comment",
            "UPDATE_FAILED",
            "Card or comment not found");

    public Task<ApiResponse<bool>> DeleteCommentAsync(string cardId, string commentId) =>
        SendForSuccessAsync(
            HttpMethod.Delete,
            BuildUrl($"/cards/{cardId}/actions/{commentId}/comments"),
            "Card or comment not found");
}
