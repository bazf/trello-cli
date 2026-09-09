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

    public async Task GetAllCardsAsync(string boardId, string? filter = null)
    {
        if (string.IsNullOrEmpty(boardId))
        {
            Write(ApiResponse<object>.Fail("Board ID required", "MISSING_PARAM"));
            return;
        }

        var result = await Api.GetCardsInBoardAsync(boardId, filter);
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

    public async Task SetCardPositionAsync(string cardId, string position)
    {
        if (!Require(cardId, "Card ID required")) return;
        if (!Require(position, "Position required (top, bottom or a number)")) return;

        Write(await Api.SetCardPositionAsync(cardId, position));
    }

    public async Task SetDueCompleteAsync(string cardId, string state)
    {
        if (!Require(cardId, "Card ID required")) return;

        bool? complete = state?.ToLowerInvariant() switch
        {
            "true" => true,
            "false" => false,
            _ => null
        };

        if (complete is null)
        {
            Write(ApiResponse<object>.Fail("State must be true or false", "INVALID_PARAM"));
            return;
        }

        Write(await Api.SetDueCompleteAsync(cardId, complete.Value));
    }

    // An empty date is meaningful here: it clears the start date.
    public async Task SetStartDateAsync(string cardId, string start)
    {
        if (!Require(cardId, "Card ID required")) return;

        Write(await Api.SetStartDateAsync(cardId, start ?? string.Empty));
    }

    public async Task SetCardCoverAsync(string cardId, string? color, string? attachmentId, string? size, string? brightness)
    {
        if (!Require(cardId, "Card ID required")) return;

        Write(await Api.SetCardCoverAsync(cardId, color, attachmentId, size, brightness));
    }

    public async Task ClearCardCoverAsync(string cardId)
    {
        if (!Require(cardId, "Card ID required")) return;

        Write(await Api.ClearCardCoverAsync(cardId));
    }

    public async Task CopyCardAsync(string cardId, string targetListId, string? name, string? position, string? keep)
    {
        if (!Require(cardId, "Card ID required")) return;
        if (!Require(targetListId, "Target list ID required")) return;

        Write(await Api.CopyCardAsync(cardId, targetListId, name, position, keep));
    }

    public async Task GetCardActivityAsync(string cardId, string? limit, string? filter)
    {
        if (!Require(cardId, "Card ID required")) return;

        int? parsed = null;
        if (!string.IsNullOrEmpty(limit))
        {
            if (!int.TryParse(limit, out var value) || value <= 0)
            {
                Write(ApiResponse<object>.Fail("--limit must be a positive whole number", "INVALID_PARAM"));
                return;
            }

            parsed = value;
        }

        Write(await Api.GetCardActivityAsync(cardId, parsed, filter));
    }

    private bool Require(string? value, string message)
    {
        if (!string.IsNullOrEmpty(value)) return true;

        Write(ApiResponse<object>.Fail(message, "MISSING_PARAM"));
        return false;
    }
}
