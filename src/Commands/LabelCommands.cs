using TrelloCli.Models;
using TrelloCli.Services;
using TrelloCli.Utils;

namespace TrelloCli.Commands;

public class LabelCommands(TrelloApiService api, TextWriter output)
    : CommandsBase(api, output)
{
    public async Task GetLabelsAsync(string boardId)
    {
        if (string.IsNullOrEmpty(boardId))
        {
            Write(ApiResponse<object>.Fail("Board ID required", "MISSING_PARAM"));
            return;
        }

        var result = await Api.GetLabelsAsync(boardId);
        Write(result);
    }

    public async Task CreateLabelAsync(string boardId, string name, string? color)
    {
        if (string.IsNullOrEmpty(boardId))
        {
            Write(ApiResponse<object>.Fail("Board ID required", "MISSING_PARAM"));
            return;
        }

        if (string.IsNullOrEmpty(name))
        {
            Write(ApiResponse<object>.Fail("Label name required", "MISSING_PARAM"));
            return;
        }

        var result = await Api.CreateLabelAsync(boardId, name, color);
        Write(result);
    }

    public async Task UpdateLabelAsync(string labelId, string? name, string? color)
    {
        if (string.IsNullOrEmpty(labelId))
        {
            Write(ApiResponse<object>.Fail("Label ID required", "MISSING_PARAM"));
            return;
        }

        var result = await Api.UpdateLabelAsync(labelId, name, color);
        Write(result);
    }

    public async Task DeleteLabelAsync(string labelId)
    {
        if (string.IsNullOrEmpty(labelId))
        {
            Write(ApiResponse<object>.Fail("Label ID required", "MISSING_PARAM"));
            return;
        }

        var result = await Api.DeleteLabelAsync(labelId);
        Write(result);
    }
}
