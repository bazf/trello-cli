using TrelloCli.Models;
using TrelloCli.Services;
using TrelloCli.Utils;

namespace TrelloCli.Commands;

public class AttachmentCommands(TrelloApiService api, TextWriter output)
    : CommandsBase(api, output)
{
    public async Task GetAttachmentsAsync(string cardId)
    {
        if (string.IsNullOrEmpty(cardId))
        {
            Write(ApiResponse<object>.Fail("Card ID required", "MISSING_PARAM"));
            return;
        }

        var result = await Api.GetAttachmentsAsync(cardId);
        Write(result);
    }

    public async Task UploadAttachmentAsync(string cardId, string filePath, string? name = null)
    {
        if (string.IsNullOrEmpty(cardId))
        {
            Write(ApiResponse<object>.Fail("Card ID required", "MISSING_PARAM"));
            return;
        }

        if (string.IsNullOrEmpty(filePath))
        {
            Write(ApiResponse<object>.Fail("File path required", "MISSING_PARAM"));
            return;
        }

        var result = await Api.UploadAttachmentAsync(cardId, filePath, name);
        Write(result);
    }

    public async Task AttachUrlAsync(string cardId, string url, string? name = null)
    {
        if (string.IsNullOrEmpty(cardId))
        {
            Write(ApiResponse<object>.Fail("Card ID required", "MISSING_PARAM"));
            return;
        }

        if (string.IsNullOrEmpty(url))
        {
            Write(ApiResponse<object>.Fail("URL required", "MISSING_PARAM"));
            return;
        }

        var result = await Api.AttachUrlAsync(cardId, url, name);
        Write(result);
    }

    public async Task DeleteAttachmentAsync(string cardId, string attachmentId)
    {
        if (string.IsNullOrEmpty(cardId))
        {
            Write(ApiResponse<object>.Fail("Card ID required", "MISSING_PARAM"));
            return;
        }

        if (string.IsNullOrEmpty(attachmentId))
        {
            Write(ApiResponse<object>.Fail("Attachment ID required", "MISSING_PARAM"));
            return;
        }

        var result = await Api.DeleteAttachmentAsync(cardId, attachmentId);
        Write(result);
    }

    public async Task GetAttachmentAsync(string cardId, string attachmentId)
    {
        if (string.IsNullOrEmpty(cardId))
        {
            Write(ApiResponse<object>.Fail("Card ID required", "MISSING_PARAM"));
            return;
        }

        if (string.IsNullOrEmpty(attachmentId))
        {
            Write(ApiResponse<object>.Fail("Attachment ID required", "MISSING_PARAM"));
            return;
        }

        Write(await Api.GetAttachmentAsync(cardId, attachmentId));
    }

    public async Task DownloadAttachmentAsync(string cardId, string attachmentId, string? output, bool overwrite)
    {
        if (string.IsNullOrEmpty(cardId))
        {
            Write(ApiResponse<object>.Fail("Card ID required", "MISSING_PARAM"));
            return;
        }

        if (string.IsNullOrEmpty(attachmentId))
        {
            Write(ApiResponse<object>.Fail("Attachment ID required", "MISSING_PARAM"));
            return;
        }

        Write(await Api.DownloadAttachmentAsync(cardId, attachmentId, output, overwrite));
    }

    public async Task DownloadAllAttachmentsAsync(string cardId, string? outputDirectory, bool overwrite)
    {
        if (string.IsNullOrEmpty(cardId))
        {
            Write(ApiResponse<object>.Fail("Card ID required", "MISSING_PARAM"));
            return;
        }

        if (string.IsNullOrEmpty(outputDirectory))
        {
            Write(ApiResponse<object>.Fail("--output-dir is required", "MISSING_PARAM"));
            return;
        }

        Write(await Api.DownloadAllAttachmentsAsync(cardId, outputDirectory, overwrite));
    }
}
