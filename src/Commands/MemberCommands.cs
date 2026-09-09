using TrelloCli.Models;
using TrelloCli.Services;

namespace TrelloCli.Commands;

public class MemberCommands(TrelloApiService api, TextWriter output)
    : CommandsBase(api, output)
{
    public async Task WhoAmIAsync() => Write(await Api.GetMeAsync());

    public async Task GetMemberAsync(string idOrUsername)
    {
        if (string.IsNullOrEmpty(idOrUsername))
        {
            Write(ApiResponse<object>.Fail("Member ID or username required", "MISSING_PARAM"));
            return;
        }

        Write(await Api.GetMemberAsync(idOrUsername));
    }

    public async Task GetMyCardsAsync(string? filter) => Write(await Api.GetMyCardsAsync(filter));

    public async Task GetBoardMembersAsync(string boardId)
    {
        if (string.IsNullOrEmpty(boardId))
        {
            Write(ApiResponse<object>.Fail("Board ID required", "MISSING_PARAM"));
            return;
        }

        Write(await Api.GetBoardMembersAsync(boardId));
    }

    public async Task GetCardMembersAsync(string cardId)
    {
        if (string.IsNullOrEmpty(cardId))
        {
            Write(ApiResponse<object>.Fail("Card ID required", "MISSING_PARAM"));
            return;
        }

        Write(await Api.GetCardMembersAsync(cardId));
    }

    public async Task AddCardMemberAsync(string cardId, string memberId)
    {
        if (!RequireCardAndMember(cardId, memberId)) return;

        Write(await Api.AddCardMemberAsync(cardId, memberId));
    }

    public async Task RemoveCardMemberAsync(string cardId, string memberId)
    {
        if (!RequireCardAndMember(cardId, memberId)) return;

        Write(await Api.RemoveCardMemberAsync(cardId, memberId));
    }

    private bool RequireCardAndMember(string cardId, string memberId)
    {
        if (string.IsNullOrEmpty(cardId))
        {
            Write(ApiResponse<object>.Fail("Card ID required", "MISSING_PARAM"));
            return false;
        }

        if (string.IsNullOrEmpty(memberId))
        {
            Write(ApiResponse<object>.Fail("Member ID required", "MISSING_PARAM"));
            return false;
        }

        return true;
    }
}
