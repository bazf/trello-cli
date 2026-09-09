using System.Text.Json.Serialization;

namespace TrelloCli.Models;

/// <summary>One attachment written to disk.</summary>
public class AttachmentDownload
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("fileName")]
    public string? FileName { get; set; }

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("bytes")]
    public long Bytes { get; set; }

    [JsonPropertyName("mimeType")]
    public string? MimeType { get; set; }
}

/// <summary>
/// The result of downloading every attachment on a card. Unlike a bulk list move,
/// a partial result is useful, so failures are reported per attachment instead of
/// aborting the run.
/// </summary>
public class AttachmentDownloadSummary
{
    [JsonPropertyName("directory")]
    public string Directory { get; set; } = string.Empty;

    [JsonPropertyName("downloaded")]
    public List<AttachmentDownload> Downloaded { get; set; } = new();

    [JsonPropertyName("skipped")]
    public List<SkippedAttachment> Skipped { get; set; } = new();

    [JsonPropertyName("failed")]
    public List<FailedAttachment> Failed { get; set; } = new();
}

/// <summary>An attachment Trello does not host, reported with the URL to fetch it from.</summary>
public class SkippedAttachment
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;
}

public class FailedAttachment
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("error")]
    public string Error { get; set; } = string.Empty;

    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;
}
