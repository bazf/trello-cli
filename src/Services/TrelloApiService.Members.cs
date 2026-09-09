using TrelloCli.Models;

namespace TrelloCli.Services;

public partial class TrelloApiService
{
    private const string MemberFields = "fields=id,username,fullName,initials,avatarUrl,url";

    public Task<ApiResponse<Member>> GetMeAsync() =>
        GetObjectAsync<Member>(BuildUrl("/members/me", MemberFields), "Member not found", "NOT_FOUND", "Member not found");

    public Task<ApiResponse<Member>> GetMemberAsync(string idOrUsername) =>
        GetObjectAsync<Member>(
            BuildUrl($"/members/{idOrUsername}", MemberFields),
            "Member not found",
            "NOT_FOUND",
            "Member not found");

    public Task<ApiResponse<List<Card>>> GetMyCardsAsync(string? filter)
    {
        var selected = string.IsNullOrEmpty(filter) ? "open" : filter;
        return GetListAsync<Card>(BuildUrl("/members/me/cards", $"filter={selected}"), notFoundMessage: null);
    }

    public Task<ApiResponse<List<Member>>> GetBoardMembersAsync(string boardId) =>
        GetListAsync<Member>(BuildUrl($"/boards/{boardId}/members", MemberFields), "Board not found");

    public Task<ApiResponse<List<Member>>> GetCardMembersAsync(string cardId) =>
        GetListAsync<Member>(BuildUrl($"/cards/{cardId}/members", MemberFields), "Card not found");

    // Adds one member, in contrast to --members on --update-card which replaces the whole set.
    public Task<ApiResponse<List<Member>>> AddCardMemberAsync(string cardId, string memberId) =>
        SendForListAsync<Member>(
            HttpMethod.Post,
            BuildUrl($"/cards/{cardId}/idMembers"),
            new FormUrlEncodedContent(new Dictionary<string, string> { ["value"] = memberId }),
            "Card or member not found");

    public Task<ApiResponse<List<Member>>> RemoveCardMemberAsync(string cardId, string memberId) =>
        SendForListAsync<Member>(
            HttpMethod.Delete,
            BuildUrl($"/cards/{cardId}/idMembers/{memberId}"),
            content: null,
            "Card or member not found");
}
