using TrelloCli.Models;
using TrelloCli.Services;
using TrelloCli.Utils;

namespace TrelloCli.Commands;

public class BoardCommands(TrelloApiService api, TextWriter output)
    : CommandsBase(api, output)
{
    public async Task GetBoardsAsync(string? filter = null)
    {
        var result = await Api.GetBoardsAsync(filter);
        Write(result);
    }

    public async Task GetBoardAsync(string boardId)
    {
        if (string.IsNullOrEmpty(boardId))
        {
            Write(ApiResponse<object>.Fail("Board ID required", "MISSING_PARAM"));
            return;
        }

        var result = await Api.GetBoardAsync(boardId);
        Write(result);
    }

    public async Task CreateBoardAsync(string name, string? desc, string? organizationId, string? defaultLists, string? permissionLevel)
    {
        if (string.IsNullOrEmpty(name))
        {
            Write(ApiResponse<object>.Fail("Board name required", "MISSING_PARAM"));
            return;
        }

        bool? lists = defaultLists?.ToLowerInvariant() switch
        {
            "true" => true,
            "false" => false,
            null => null,
            _ => null
        };

        if (defaultLists is not null && lists is null)
        {
            Write(ApiResponse<object>.Fail("--default-lists must be true or false", "INVALID_PARAM"));
            return;
        }

        Write(await Api.CreateBoardAsync(name, desc, organizationId, lists, permissionLevel));
    }

    public async Task UpdateBoardAsync(string boardId, string? name, string? desc, string? permissionLevel)
    {
        if (!RequireBoard(boardId)) return;

        Write(await Api.UpdateBoardAsync(boardId, name, desc, permissionLevel));
    }

    public async Task CloseBoardAsync(string boardId)
    {
        if (!RequireBoard(boardId)) return;

        Write(await Api.SetBoardClosedAsync(boardId, closed: true));
    }

    public async Task ReopenBoardAsync(string boardId)
    {
        if (!RequireBoard(boardId)) return;

        Write(await Api.SetBoardClosedAsync(boardId, closed: false));
    }

    private bool RequireBoard(string boardId)
    {
        if (!string.IsNullOrEmpty(boardId)) return true;

        Write(ApiResponse<object>.Fail("Board ID required", "MISSING_PARAM"));
        return false;
    }
}
