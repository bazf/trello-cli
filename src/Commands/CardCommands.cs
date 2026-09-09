using TrelloCli.Models;
using TrelloCli.Services;
using TrelloCli.Utils;

namespace TrelloCli.Commands;

public class CardCommands(TrelloApiService api, TextWriter output)
    : CommandsBase(api, output)
{
    public async Task GetCardsAsync(string listId)
    {
        if (string.IsNullOrEmpty(listId))
        {
            Write(ApiResponse<object>.Fail("List ID required", "MISSING_PARAM"));
            return;
        }

        var result = await Api.GetCardsInListAsync(listId);
        Write(result);
    }

    public async Task GetAllCardsAsync(string boardId)
    {
        if (string.IsNullOrEmpty(boardId))
        {
            Write(ApiResponse<object>.Fail("Board ID required", "MISSING_PARAM"));
            return;
        }

        var result = await Api.GetCardsInBoardAsync(boardId);
        Write(result);
    }

    public async Task GetCardAsync(string cardId)
    {
        if (string.IsNullOrEmpty(cardId))
        {
            Write(ApiResponse<object>.Fail("Card ID required", "MISSING_PARAM"));
            return;
        }

        var result = await Api.GetCardAsync(cardId);
        Write(result);
    }

    public async Task CreateCardAsync(string listId, string name, string? desc = null, string? due = null, string? labels = null, string? members = null)
    {
        if (string.IsNullOrEmpty(listId))
        {
            Write(ApiResponse<object>.Fail("List ID required", "MISSING_PARAM"));
            return;
        }

        if (string.IsNullOrEmpty(name))
        {
            Write(ApiResponse<object>.Fail("Card name required", "MISSING_PARAM"));
            return;
        }

        var result = await Api.CreateCardAsync(listId, name, desc, due, labels, members);
        Write(result);
    }

    public async Task UpdateCardAsync(string cardId, string? name, string? desc, string? due, string? labels, string? members, bool? closed = null)
    {
        if (string.IsNullOrEmpty(cardId))
        {
            Write(ApiResponse<object>.Fail("Card ID required", "MISSING_PARAM"));
            return;
        }

        var result = await Api.UpdateCardAsync(cardId, name, desc, due, null, labels, members, closed);
        Write(result);
    }

    public async Task ArchiveCardAsync(string cardId)
    {
        if (string.IsNullOrEmpty(cardId))
        {
            Write(ApiResponse<object>.Fail("Card ID required", "MISSING_PARAM"));
            return;
        }

        var result = await Api.UpdateCardAsync(cardId, closed: true);
        Write(result);
    }

    public async Task UnarchiveCardAsync(string cardId)
    {
        if (string.IsNullOrEmpty(cardId))
        {
            Write(ApiResponse<object>.Fail("Card ID required", "MISSING_PARAM"));
            return;
        }

        var result = await Api.UpdateCardAsync(cardId, closed: false);
        Write(result);
    }

    public async Task MoveCardAsync(string cardId, string listId)
    {
        if (string.IsNullOrEmpty(cardId))
        {
            Write(ApiResponse<object>.Fail("Card ID required", "MISSING_PARAM"));
            return;
        }

        if (string.IsNullOrEmpty(listId))
        {
            Write(ApiResponse<object>.Fail("List ID required", "MISSING_PARAM"));
            return;
        }

        var result = await Api.MoveCardAsync(cardId, listId);
        Write(result);
    }

    public async Task DeleteCardAsync(string cardId)
    {
        if (string.IsNullOrEmpty(cardId))
        {
            Write(ApiResponse<object>.Fail("Card ID required", "MISSING_PARAM"));
            return;
        }

        var result = await Api.DeleteCardAsync(cardId);
        Write(result);
    }

    public async Task GetCommentsAsync(string cardId)
    {
        if (string.IsNullOrEmpty(cardId))
        {
            Write(ApiResponse<object>.Fail("Card ID required", "MISSING_PARAM"));
            return;
        }

        var result = await Api.GetCommentsAsync(cardId);
        Write(result);
    }

    public async Task AddCommentAsync(string cardId, string text)
    {
        if (string.IsNullOrEmpty(cardId))
        {
            Write(ApiResponse<object>.Fail("Card ID required", "MISSING_PARAM"));
            return;
        }

        if (string.IsNullOrEmpty(text))
        {
            Write(ApiResponse<object>.Fail("Comment text required", "MISSING_PARAM"));
            return;
        }

        var result = await Api.AddCommentAsync(cardId, text);
        Write(result);
    }

    public async Task UpdateCommentAsync(string cardId, string commentId, string text)
    {
        if (string.IsNullOrEmpty(cardId))
        {
            Write(ApiResponse<object>.Fail("Card ID required", "MISSING_PARAM"));
            return;
        }

        if (string.IsNullOrEmpty(commentId))
        {
            Write(ApiResponse<object>.Fail("Comment ID required", "MISSING_PARAM"));
            return;
        }

        if (string.IsNullOrEmpty(text))
        {
            Write(ApiResponse<object>.Fail("Comment text required", "MISSING_PARAM"));
            return;
        }

        Write(await Api.UpdateCommentAsync(cardId, commentId, text));
    }

    public async Task DeleteCommentAsync(string cardId, string commentId)
    {
        if (string.IsNullOrEmpty(cardId))
        {
            Write(ApiResponse<object>.Fail("Card ID required", "MISSING_PARAM"));
            return;
        }

        if (string.IsNullOrEmpty(commentId))
        {
            Write(ApiResponse<object>.Fail("Comment ID required", "MISSING_PARAM"));
            return;
        }

        Write(await Api.DeleteCommentAsync(cardId, commentId));
    }

    public async Task AddCardLabelAsync(string cardId, string labelId)
    {
        if (!RequireCardAndLabel(cardId, labelId)) return;

        Write(await Api.AddCardLabelAsync(cardId, labelId));
    }

    public async Task RemoveCardLabelAsync(string cardId, string labelId)
    {
        if (!RequireCardAndLabel(cardId, labelId)) return;

        Write(await Api.RemoveCardLabelAsync(cardId, labelId));
    }

    private bool RequireCardAndLabel(string cardId, string labelId)
    {
        if (string.IsNullOrEmpty(cardId))
        {
            Write(ApiResponse<object>.Fail("Card ID required", "MISSING_PARAM"));
            return false;
        }

        if (string.IsNullOrEmpty(labelId))
        {
            Write(ApiResponse<object>.Fail("Label ID required", "MISSING_PARAM"));
            return false;
        }

        return true;
    }
}
