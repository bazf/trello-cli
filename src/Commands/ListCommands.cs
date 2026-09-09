using TrelloCli.Models;
using TrelloCli.Services;
using TrelloCli.Utils;

namespace TrelloCli.Commands;

public class ListCommands(TrelloApiService api, TextWriter output)
    : CommandsBase(api, output)
{
    public async Task GetListsAsync(string boardId)
    {
        if (string.IsNullOrEmpty(boardId))
        {
            Write(ApiResponse<object>.Fail("Board ID required", "MISSING_PARAM"));
            return;
        }

        var result = await Api.GetListsAsync(boardId);
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
}
