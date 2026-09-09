using TrelloCli.Models;
using TrelloCli.Services;
using TrelloCli.Utils;

namespace TrelloCli.Commands;

public class ListCommands(TrelloApiService api, TextWriter output)
    : CommandsBase(api, output)
{
    public async Task GetListsAsync(string boardId, string? filter = null)
    {
        if (string.IsNullOrEmpty(boardId))
        {
            Write(ApiResponse<object>.Fail("Board ID required", "MISSING_PARAM"));
            return;
        }

        var result = await Api.GetListsAsync(boardId, filter);
        Write(result);
    }

    public async Task CreateListAsync(string boardId, string name)
    {
        if (string.IsNullOrEmpty(boardId))
        {
            Write(ApiResponse<object>.Fail("Board ID required", "MISSING_PARAM"));
            return;
        }

        if (string.IsNullOrEmpty(name))
        {
            Write(ApiResponse<object>.Fail("List name required", "MISSING_PARAM"));
            return;
        }

        var result = await Api.CreateListAsync(boardId, name);
        Write(result);
    }

    public async Task MoveListAsync(string listId, string pos)
    {
        if (string.IsNullOrEmpty(listId))
        {
            Write(ApiResponse<object>.Fail("List ID required", "MISSING_PARAM"));
            return;
        }

        if (string.IsNullOrEmpty(pos))
        {
            Write(ApiResponse<object>.Fail("Position required (top, bottom, or a number)", "MISSING_PARAM"));
            return;
        }

        var result = await Api.MoveListAsync(listId, pos);
        Write(result);
    }

    public async Task BulkMoveListsAsync(string[] pairs)
    {
        if (pairs.Length == 0)
        {
            Write(ApiResponse<object>.Fail("At least one list-id:pos pair required", "MISSING_PARAM"));
            return;
        }

        var results = new List<object>();
        foreach (var pair in pairs)
        {
            var parts = pair.Split(':', 2);
            if (parts.Length != 2 || string.IsNullOrEmpty(parts[0]) || string.IsNullOrEmpty(parts[1]))
            {
                Write(ApiResponse<object>.Fail($"Invalid format '{pair}' — expected list-id:pos", "INVALID_PARAM"));
                return;
            }

            var result = await Api.MoveListAsync(parts[0], parts[1]);
            results.Add(new { listId = parts[0], pos = parts[1], ok = result.Ok, data = result.Data, error = result.Error });

            if (!result.Ok)
            {
                Write(ApiResponse<List<object>>.Fail($"Failed on '{pair}': {result.Error}", result.Code ?? "ERROR"));
                return;
            }
        }

        Write(ApiResponse<List<object>>.Success(results));
    }

    public async Task UpdateListAsync(string listId, string? name, string? pos)
    {
        if (!RequireList(listId)) return;

        Write(await Api.UpdateListAsync(listId, name, pos));
    }

    public async Task ArchiveListAsync(string listId)
    {
        if (!RequireList(listId)) return;

        Write(await Api.SetListClosedAsync(listId, closed: true));
    }

    public async Task UnarchiveListAsync(string listId)
    {
        if (!RequireList(listId)) return;

        Write(await Api.SetListClosedAsync(listId, closed: false));
    }

    public async Task ArchiveAllCardsAsync(string listId)
    {
        if (!RequireList(listId)) return;

        Write(await Api.ArchiveAllCardsAsync(listId));
    }

    public async Task MoveAllCardsAsync(string sourceListId, string targetListId)
    {
        if (!RequireList(sourceListId)) return;

        if (string.IsNullOrEmpty(targetListId))
        {
            Write(ApiResponse<object>.Fail("Target list ID required", "MISSING_PARAM"));
            return;
        }

        Write(await Api.MoveAllCardsAsync(sourceListId, targetListId));
    }

    private bool RequireList(string listId)
    {
        if (!string.IsNullOrEmpty(listId)) return true;

        Write(ApiResponse<object>.Fail("List ID required", "MISSING_PARAM"));
        return false;
    }
}
