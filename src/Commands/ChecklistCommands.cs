using TrelloCli.Models;
using TrelloCli.Services;
using TrelloCli.Utils;

namespace TrelloCli.Commands;

public class ChecklistCommands(TrelloApiService api, TextWriter output)
    : CommandsBase(api, output)
{
    public async Task GetChecklistsAsync(string cardId)
    {
        if (string.IsNullOrEmpty(cardId))
        {
            Write(ApiResponse<object>.Fail("Card ID required", "MISSING_PARAM"));
            return;
        }

        var result = await Api.GetChecklistsAsync(cardId);
        Write(result);
    }

    public async Task CreateChecklistAsync(string cardId, string name)
    {
        if (string.IsNullOrEmpty(cardId))
        {
            Write(ApiResponse<object>.Fail("Card ID required", "MISSING_PARAM"));
            return;
        }

        if (string.IsNullOrEmpty(name))
        {
            Write(ApiResponse<object>.Fail("Checklist name required", "MISSING_PARAM"));
            return;
        }

        var result = await Api.CreateChecklistAsync(cardId, name);
        Write(result);
    }

    public async Task DeleteChecklistAsync(string checklistId)
    {
        if (string.IsNullOrEmpty(checklistId))
        {
            Write(ApiResponse<object>.Fail("Checklist ID required", "MISSING_PARAM"));
            return;
        }

        var result = await Api.DeleteChecklistAsync(checklistId);
        Write(result);
    }

    public async Task AddChecklistItemAsync(string checklistId, string name)
    {
        if (string.IsNullOrEmpty(checklistId))
        {
            Write(ApiResponse<object>.Fail("Checklist ID required", "MISSING_PARAM"));
            return;
        }

        if (string.IsNullOrEmpty(name))
        {
            Write(ApiResponse<object>.Fail("Item name required", "MISSING_PARAM"));
            return;
        }

        var result = await Api.AddChecklistItemAsync(checklistId, name);
        Write(result);
    }

    public async Task UpdateChecklistItemAsync(string cardId, string checkItemId, string state)
    {
        if (string.IsNullOrEmpty(cardId))
        {
            Write(ApiResponse<object>.Fail("Card ID required", "MISSING_PARAM"));
            return;
        }

        if (string.IsNullOrEmpty(checkItemId))
        {
            Write(ApiResponse<object>.Fail("Checklist item ID required", "MISSING_PARAM"));
            return;
        }

        if (string.IsNullOrEmpty(state))
        {
            Write(ApiResponse<object>.Fail("State required (complete or incomplete)", "MISSING_PARAM"));
            return;
        }

        var normalizedState = state.ToLowerInvariant();
        if (normalizedState != "complete" && normalizedState != "incomplete")
        {
            Write(ApiResponse<object>.Fail("State must be 'complete' or 'incomplete'", "INVALID_PARAM"));
            return;
        }

        var result = await Api.UpdateChecklistItemAsync(cardId, checkItemId, normalizedState);
        Write(result);
    }

    public async Task DeleteChecklistItemAsync(string checklistId, string checkItemId)
    {
        if (string.IsNullOrEmpty(checklistId))
        {
            Write(ApiResponse<object>.Fail("Checklist ID required", "MISSING_PARAM"));
            return;
        }

        if (string.IsNullOrEmpty(checkItemId))
        {
            Write(ApiResponse<object>.Fail("Checklist item ID required", "MISSING_PARAM"));
            return;
        }

        var result = await Api.DeleteChecklistItemAsync(checklistId, checkItemId);
        Write(result);
    }
}
