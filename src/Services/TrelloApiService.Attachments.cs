using System.Net.Http.Headers;
using TrelloCli.Models;

namespace TrelloCli.Services;

public partial class TrelloApiService
{
    public Task<ApiResponse<List<Attachment>>> GetAttachmentsAsync(string cardId) =>
        GetListAsync<Attachment>(BuildUrl($"/cards/{cardId}/attachments"), "Card not found");

    public Task<ApiResponse<Attachment>> GetAttachmentAsync(string cardId, string attachmentId) =>
        GetObjectAsync<Attachment>(
            BuildUrl($"/cards/{cardId}/attachments/{attachmentId}"),
            "Attachment not found",
            "NOT_FOUND",
            "Attachment not found");

    public Task<ApiResponse<Attachment>> UploadAttachmentAsync(string cardId, string filePath, string? name = null) =>
        ExecuteAsync(async () =>
        {
            if (!File.Exists(filePath))
                return ApiResponse<Attachment>.Fail($"File not found: {filePath}", "FILE_NOT_FOUND");

            using var content = new MultipartFormDataContent();
            using var fileStream = File.OpenRead(filePath);
            var streamContent = new StreamContent(fileStream);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue(GetMimeType(filePath));
            content.Add(streamContent, "file", name ?? Path.GetFileName(filePath));

            if (!string.IsNullOrEmpty(name)) content.Add(new StringContent(name), "name");

            var response = await SendAsync(HttpMethod.Post, BuildUrl($"/cards/{cardId}/attachments"), content);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync();
            var attachment = System.Text.Json.JsonSerializer.Deserialize<Attachment>(body);

            return attachment is not null
                ? ApiResponse<Attachment>.Success(attachment)
                : ApiResponse<Attachment>.Fail("Failed to upload attachment", "UPLOAD_FAILED");
        }, "Card not found");

    public Task<ApiResponse<Attachment>> AttachUrlAsync(string cardId, string attachUrl, string? name = null)
    {
        var formData = new Dictionary<string, string> { ["url"] = attachUrl };
        if (!string.IsNullOrEmpty(name)) formData["name"] = name;

        return SendForObjectAsync<Attachment>(
            HttpMethod.Post,
            BuildUrl($"/cards/{cardId}/attachments"),
            new FormUrlEncodedContent(formData),
            "Failed to attach URL",
            "ATTACH_FAILED",
            "Card not found");
    }

    public Task<ApiResponse<bool>> DeleteAttachmentAsync(string cardId, string attachmentId) =>
        SendForSuccessAsync(
            HttpMethod.Delete,
            BuildUrl($"/cards/{cardId}/attachments/{attachmentId}"),
            "Attachment not found");
}
