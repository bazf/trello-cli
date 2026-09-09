using System.Net;
using TrelloCli.Models;
using TrelloCli.Utils;

namespace TrelloCli.Services;

/// <summary>
/// Attachment content downloads.
///
/// Trello serves hosted attachments from
/// <c>/cards/{card}/attachments/{attachment}/download/{fileName}</c> and requires the same
/// Authorization header every other call here already sends; query-parameter credentials were
/// switched off for this endpoint in January 2021. That endpoint answers with a redirect to a
/// presigned URL on a different host, which needs no credentials of its own and must never
/// receive ours.
///
/// The rest of the client sets <c>AllowAutoRedirect = false</c> so no redirect can ever replay
/// the header. Downloads therefore follow redirects by hand, re-deciding from the target URI on
/// every hop whether credentials belong on the request. This loop is reachable only from here.
/// </summary>
public partial class TrelloApiService
{
    internal const int MaxDownloadRedirects = 3;

    private const string TooManyRedirectsMessage = "The attachment download redirected too many times.";
    private const string RedirectBlockedMessage = "The attachment download redirected to an unsupported location.";
    private const string RedirectInvalidMessage = "The attachment download returned a redirect without a location.";
    private const string DownloadIncompleteMessage = "The attachment download ended before the whole file arrived.";

    public async Task<ApiResponse<AttachmentDownload>> DownloadAttachmentAsync(
        string cardId,
        string attachmentId,
        string? output,
        bool overwrite)
    {
        var lookup = await GetAttachmentAsync(cardId, attachmentId);
        if (!lookup.Ok || lookup.Data is null)
            return ApiResponse<AttachmentDownload>.Fail(lookup.Error ?? "Attachment not found", lookup.Code ?? "NOT_FOUND");

        var attachment = lookup.Data;
        if (!attachment.IsUpload)
        {
            // A link attachment lives on a third party's server. Report the URL and let the
            // caller fetch it; the CLI does not send Trello credentials off-host, and does not
            // make requests to arbitrary hosts on a card's behalf.
            return ApiResponse<AttachmentDownload>.Fail(
                $"Attachment {attachmentId} is a link, not a file hosted by Trello. Fetch it directly from {attachment.Url}",
                "LINK_ATTACHMENT");
        }

        var reserved = DownloadPaths.NewReservationSet();
        var destination = ResolveSingleDestination(attachment, output, overwrite, reserved, out var failure);
        if (destination is null) return ApiResponse<AttachmentDownload>.Fail(failure!.Error!, failure.Code!);

        return await DownloadToPathAsync(cardId, attachment, destination, overwrite);
    }

    public async Task<ApiResponse<AttachmentDownloadSummary>> DownloadAllAttachmentsAsync(
        string cardId,
        string outputDirectory,
        bool overwrite)
    {
        var listing = await GetAttachmentsAsync(cardId);
        if (!listing.Ok || listing.Data is null)
            return ApiResponse<AttachmentDownloadSummary>.Fail(listing.Error ?? "Card not found", listing.Code ?? "NOT_FOUND");

        string root;
        try
        {
            Directory.CreateDirectory(outputDirectory);
            root = Path.GetFullPath(outputDirectory);
        }
        catch (Exception)
        {
            return ApiResponse<AttachmentDownloadSummary>.Fail(
                "The output directory could not be created.", "DIRECTORY_NOT_FOUND");
        }

        var summary = new AttachmentDownloadSummary { Directory = root };
        var reserved = DownloadPaths.NewReservationSet();

        // Sequential on purpose: a card with many attachments would otherwise burst straight
        // through Trello's per-token rate limit.
        foreach (var attachment in listing.Data)
        {
            if (!attachment.IsUpload)
            {
                summary.Skipped.Add(new SkippedAttachment
                {
                    Id = attachment.Id,
                    Name = attachment.Name,
                    Url = attachment.Url,
                    Reason = "LINK_ATTACHMENT"
                });
                continue;
            }

            var safeName = DownloadPaths.SanitizeFileName(
                attachment.FileName ?? attachment.Name,
                $"attachment-{attachment.Id}");

            var resolved = DownloadPaths.ResolveWithin(root, safeName);
            if (resolved is null)
            {
                summary.Failed.Add(Failed(attachment, "The attachment file name resolved outside the output directory.", "PATH_TRAVERSAL"));
                continue;
            }

            var destination = DownloadPaths.ClaimUniquePath(resolved, reserved, overwrite);
            if (destination is null)
            {
                summary.Failed.Add(Failed(attachment, "No free file name was available for this attachment.", "NAME_COLLISION"));
                continue;
            }

            var download = await DownloadToPathAsync(cardId, attachment, destination, overwrite);
            if (download.Ok && download.Data is not null) summary.Downloaded.Add(download.Data);
            else summary.Failed.Add(Failed(attachment, download.Error ?? "Download failed.", download.Code ?? "ERROR"));
        }

        return ApiResponse<AttachmentDownloadSummary>.Success(summary);
    }

    private static FailedAttachment Failed(Attachment attachment, string error, string code) => new()
    {
        Id = attachment.Id,
        Name = attachment.Name,
        Error = error,
        Code = code
    };

    private string? ResolveSingleDestination(
        Attachment attachment,
        string? output,
        bool overwrite,
        ISet<string> reserved,
        out ApiResponse<AttachmentDownload>? failure)
    {
        failure = null;
        var safeName = DownloadPaths.SanitizeFileName(
            attachment.FileName ?? attachment.Name,
            $"attachment-{attachment.Id}");

        string candidate;
        if (string.IsNullOrEmpty(output))
        {
            candidate = Path.Combine(Directory.GetCurrentDirectory(), safeName);
        }
        else if (Directory.Exists(output) || Path.EndsInDirectorySeparator(output))
        {
            // --output names a directory to drop the file into, so Trello's name still applies
            // and still has to be made safe.
            var resolved = DownloadPaths.ResolveWithin(output, safeName);
            if (resolved is null)
            {
                failure = ApiResponse<AttachmentDownload>.Fail(
                    "The attachment file name resolved outside the output directory.", "PATH_TRAVERSAL");
                return null;
            }

            candidate = resolved;
        }
        else
        {
            // --output names the file itself. That path came from the user, not from Trello,
            // so it is taken literally; only a missing parent directory is rejected.
            candidate = Path.GetFullPath(output);
            var parent = Path.GetDirectoryName(candidate);
            if (string.IsNullOrEmpty(parent) || !Directory.Exists(parent))
            {
                failure = ApiResponse<AttachmentDownload>.Fail(
                    "The directory for the output path does not exist.", "DIRECTORY_NOT_FOUND");
                return null;
            }
        }

        if (File.Exists(candidate) && !overwrite)
        {
            failure = ApiResponse<AttachmentDownload>.Fail(
                $"{candidate} already exists. Pass --overwrite to replace it.", "FILE_EXISTS");
            return null;
        }

        reserved.Add(candidate);
        return candidate;
    }

    private async Task<ApiResponse<AttachmentDownload>> DownloadToPathAsync(
        string cardId,
        Attachment attachment,
        string destination,
        bool overwrite)
    {
        try
        {
            // Built from our own base URL rather than attachment.Url: a card can carry a link
            // attachment pointing anywhere, and the first hop must start on our own origin.
            var fileSegment = Uri.EscapeDataString(
                DownloadPaths.SanitizeFileName(attachment.FileName ?? attachment.Name, attachment.Id));
            var url = new Uri($"{_baseUrl}/cards/{cardId}/attachments/{attachment.Id}/download/{fileSegment}");

            using var response = await SendFollowingSafeRedirectsAsync(url);
            response.EnsureSuccessStatusCode();

            var written = await WriteStreamAtomicallyAsync(response, destination, overwrite);

            if (attachment.Bytes is { } expected && expected != written)
            {
                if (File.Exists(destination)) File.Delete(destination);
                return ApiResponse<AttachmentDownload>.Fail(DownloadIncompleteMessage, "DOWNLOAD_INCOMPLETE");
            }

            return ApiResponse<AttachmentDownload>.Success(new AttachmentDownload
            {
                Id = attachment.Id,
                Name = attachment.Name,
                FileName = attachment.FileName,
                Path = destination,
                Bytes = written,
                MimeType = attachment.MimeType
            });
        }
        catch (DownloadRedirectException exception)
        {
            return ApiResponse<AttachmentDownload>.Fail(exception.Message, exception.Code);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return ApiResponse<AttachmentDownload>.Fail("Attachment not found", "NOT_FOUND");
        }
        catch (HttpRequestException exception)
        {
            return ApiResponse<AttachmentDownload>.Fail(DescribeHttpFailure(exception), "HTTP_ERROR");
        }
        catch (Exception)
        {
            return ApiResponse<AttachmentDownload>.Fail("The attachment could not be downloaded.", "DOWNLOAD_FAILED");
        }
    }

    /// <summary>
    /// Streams the response into a temporary file beside the destination and moves it into place.
    /// The move also protects the destination: writing to it directly would follow an existing
    /// symlink, whereas a move replaces the link itself.
    /// </summary>
    private static async Task<long> WriteStreamAtomicallyAsync(
        HttpResponseMessage response,
        string destination,
        bool overwrite)
    {
        var directory = Path.GetDirectoryName(destination)!;

        // Same directory as the destination: File.Move across volumes is a copy, not atomic.
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.tmp");
        try
        {
            long written;
            await using (var file = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await using var source = await response.Content.ReadAsStreamAsync();
                await source.CopyToAsync(file);
                written = file.Length;
            }

            File.Move(temporaryPath, destination, overwrite);
            return written;
        }
        catch
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            throw;
        }
    }

    private async Task<HttpResponseMessage> SendFollowingSafeRedirectsAsync(Uri target)
    {
        var current = target;

        for (var hop = 0; hop <= MaxDownloadRedirects; hop++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, current);
            if (IsCredentialTarget(current))
                request.Headers.TryAddWithoutValidation("Authorization", AuthorizationHeaderValue());

            // ResponseHeadersRead so a large attachment streams to disk instead of being
            // buffered in memory before the redirect decision is even made.
            var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            if (!IsRedirect(response.StatusCode)) return response;

            var location = response.Headers.Location;
            response.Dispose();

            if (location is null)
                throw new DownloadRedirectException(RedirectInvalidMessage, "REDIRECT_INVALID");

            var next = location.IsAbsoluteUri ? location : new Uri(current, location);

            if (next.Scheme != Uri.UriSchemeHttps && next.Scheme != Uri.UriSchemeHttp)
                throw new DownloadRedirectException(RedirectBlockedMessage, "REDIRECT_BLOCKED");

            if (current.Scheme == Uri.UriSchemeHttps && next.Scheme != Uri.UriSchemeHttps)
                throw new DownloadRedirectException(RedirectBlockedMessage, "REDIRECT_BLOCKED");

            current = next;
        }

        throw new DownloadRedirectException(TooManyRedirectsMessage, "TOO_MANY_REDIRECTS");
    }

    private static bool IsRedirect(HttpStatusCode status) => status
        is HttpStatusCode.MovedPermanently
        or HttpStatusCode.Found
        or HttpStatusCode.SeeOther
        or HttpStatusCode.TemporaryRedirect
        or HttpStatusCode.PermanentRedirect;

    /// <summary>
    /// Whether credentials belong on a request to this URI. Re-evaluated per hop, so a redirect
    /// away from Trello silently drops the header and a redirect back to Trello restores it.
    /// </summary>
    private bool IsCredentialTarget(Uri uri) => IsSameOrigin(uri, _baseUri) || IsWellKnownTrelloHost(uri);

    private static bool IsSameOrigin(Uri left, Uri right) =>
        string.Equals(left.Scheme, right.Scheme, StringComparison.OrdinalIgnoreCase)
        && string.Equals(left.Host, right.Host, StringComparison.OrdinalIgnoreCase)
        && left.Port == right.Port;

    // Exact host matches only. A suffix test would accept "trello.com.example.net", and
    // "trello-attachments.s3.amazonaws.com" is the legitimate redirect target that must not
    // receive credentials.
    private static bool IsWellKnownTrelloHost(Uri uri) =>
        uri.Scheme == Uri.UriSchemeHttps
        && (string.Equals(uri.Host, "api.trello.com", StringComparison.OrdinalIgnoreCase)
            || string.Equals(uri.Host, "trello.com", StringComparison.OrdinalIgnoreCase));

    private sealed class DownloadRedirectException(string message, string code) : Exception(message)
    {
        public string Code { get; } = code;
    }
}
