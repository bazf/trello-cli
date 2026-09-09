using TrelloCli.Models;
using TrelloCli.Services;
using TrelloCli.Utils;

namespace TrelloCli.Commands;

public class BoardCommands(TrelloApiService api, TextWriter output)
    : CommandsBase(api, output)
{
    public async Task GetBoardsAsync()
    {
        var result = await Api.GetBoardsAsync();
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
}
