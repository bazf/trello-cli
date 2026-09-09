using TrelloCli.Models;
using TrelloCli.Services;

namespace TrelloCli.Commands;

public class CustomFieldCommands(TrelloApiService api, TextWriter output)
    : CommandsBase(api, output)
{
    public async Task GetCustomFieldsAsync(string boardId)
    {
        if (string.IsNullOrEmpty(boardId))
        {
            Write(ApiResponse<object>.Fail("Board ID required", "MISSING_PARAM"));
            return;
        }

        Write(await Api.GetCustomFieldsAsync(boardId));
    }

    public async Task GetCardCustomFieldsAsync(string cardId)
    {
        if (string.IsNullOrEmpty(cardId))
        {
            Write(ApiResponse<object>.Fail("Card ID required", "MISSING_PARAM"));
            return;
        }

        Write(await Api.GetCardCustomFieldsAsync(cardId));
    }

    public async Task SetCustomFieldAsync(string cardId, string fieldId, string? value, string? optionId)
    {
        if (!RequireCardAndField(cardId, fieldId)) return;

        Write(await Api.SetCustomFieldAsync(cardId, fieldId, value, optionId));
    }

    public async Task ClearCustomFieldAsync(string cardId, string fieldId)
    {
        if (!RequireCardAndField(cardId, fieldId)) return;

        Write(await Api.ClearCustomFieldAsync(cardId, fieldId));
    }

    private bool RequireCardAndField(string cardId, string fieldId)
    {
        if (string.IsNullOrEmpty(cardId))
        {
            Write(ApiResponse<object>.Fail("Card ID required", "MISSING_PARAM"));
            return false;
        }

        if (string.IsNullOrEmpty(fieldId))
        {
            Write(ApiResponse<object>.Fail("Custom field ID required", "MISSING_PARAM"));
            return false;
        }

        return true;
    }
}
