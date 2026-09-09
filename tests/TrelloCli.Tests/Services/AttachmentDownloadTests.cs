using System.Net;
using System.Text;
using TrelloCli.Credentials;
using TrelloCli.Services;
using Xunit;

namespace TrelloCli.Tests.Services;

/// <summary>
/// Downloading is the one place the client follows a redirect, and Trello's download endpoint
/// redirects to a presigned URL on a host that must never see our credentials. These tests pin
/// that boundary; the redirect tests in <see cref="TrelloApiServiceTests"/> cover the ordinary
/// request path, which still refuses to follow redirects at all.
/// </summary>
public class AttachmentDownloadTests
{
    private const string ApiKeyCanary = "api-key-canary";
    private const string TokenCanary = "token-canary";
    private const string ExpectedAuthorization = "OAuth oauth_consumer_key=\"api-key-canary\", oauth_token=\"token-canary\"";

    private const string CardId = "card-id";
    private const string AttachmentId = "attachment-id";
    private const string MetadataPath = "/1/cards/card-id/attachments/attachment-id";
    private const string DownloadPathPrefix = "/1/cards/card-id/attachments/attachment-id/download/";
    private const string S3Url = "https://trello-attachments.s3.amazonaws.com/5f2c/spec.pdf?X-Amz-Signature=abc";
    private const string FileBody = "attachment body";

    [Fact]
    public async Task Download_AttachesAuthorizationToTrelloButNotToTheRedirectTarget()
    {
        var handler = new RoutingHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path == MetadataPath) return Json(Metadata());
            if (path.StartsWith(DownloadPathPrefix, StringComparison.Ordinal)) return Redirect(S3Url);
            return File(FileBody);
        });

        using var directory = new TemporaryDirectory();
        var service = await CreateServiceAsync(handler);

        var response = await service.DownloadAttachmentAsync(CardId, AttachmentId, directory.Path, overwrite: false);

        Assert.True(response.Ok, response.Error);
        Assert.Equal(3, handler.Requests.Count);

        // The Trello hops carry credentials.
        Assert.Equal(ExpectedAuthorization, Assert.Single(handler.Requests[0].AuthorizationValues));
        Assert.Equal(ExpectedAuthorization, Assert.Single(handler.Requests[1].AuthorizationValues));

        // The presigned S3 hop must not. This is the property the whole design exists for.
        Assert.Empty(handler.Requests[2].AuthorizationValues);
        Assert.Equal(S3Url, handler.Requests[2].Uri);

        foreach (var request in handler.Requests)
        {
            Assert.DoesNotContain(ApiKeyCanary, request.Uri, StringComparison.Ordinal);
            Assert.DoesNotContain(TokenCanary, request.Uri, StringComparison.Ordinal);
        }

        Assert.Equal(FileBody, await System.IO.File.ReadAllTextAsync(response.Data!.Path));
        Assert.Equal(FileBody.Length, response.Data.Bytes);

        // A presigned URL is itself a credential for that object; it must not reach the caller.
        var serialized = TrelloCli.Utils.OutputFormatter.ToJson(response);
        Assert.DoesNotContain("X-Amz-Signature", serialized, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Download_KeepsCredentialsWhenTheRedirectStaysOnATrelloHost()
    {
        var handler = new RoutingHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path == MetadataPath) return Json(Metadata());
            if (request.RequestUri.Host == "api.trello.com" && path.StartsWith(DownloadPathPrefix, StringComparison.Ordinal))
                return Redirect("https://trello.com/1/cards/card-id/attachments/attachment-id/download/spec.pdf");
            return File(FileBody);
        });

        using var directory = new TemporaryDirectory();
        var service = await CreateServiceAsync(handler);

        var response = await service.DownloadAttachmentAsync(CardId, AttachmentId, directory.Path, overwrite: false);

        Assert.True(response.Ok, response.Error);
        Assert.Equal("trello.com", new Uri(handler.Requests[2].Uri).Host);
        Assert.Equal(ExpectedAuthorization, Assert.Single(handler.Requests[2].AuthorizationValues));
    }

    [Fact]
    public async Task Download_StopsAfterTheRedirectLimit()
    {
        var handler = new RoutingHandler(request =>
            request.RequestUri!.AbsolutePath == MetadataPath
                ? Json(Metadata())
                : Redirect("https://api.trello.com/1/again"));

        using var directory = new TemporaryDirectory();
        var service = await CreateServiceAsync(handler);

        var response = await service.DownloadAttachmentAsync(CardId, AttachmentId, directory.Path, overwrite: false);

        Assert.False(response.Ok);
        Assert.Equal("TOO_MANY_REDIRECTS", response.Code);
        Assert.Equal(1 + TrelloApiService.MaxDownloadRedirects + 1, handler.Requests.Count);
        Assert.Empty(Directory.GetFileSystemEntries(directory.Path));
    }

    [Fact]
    public async Task Download_RefusesAnHttpsToHttpDowngrade()
    {
        var handler = new RoutingHandler(request =>
            request.RequestUri!.AbsolutePath == MetadataPath
                ? Json(Metadata())
                : Redirect("http://api.trello.com/1/insecure"));

        using var directory = new TemporaryDirectory();
        var service = await CreateServiceAsync(handler);

        var response = await service.DownloadAttachmentAsync(CardId, AttachmentId, directory.Path, overwrite: false);

        Assert.False(response.Ok);
        Assert.Equal("REDIRECT_BLOCKED", response.Code);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task Download_RefusesARedirectWithoutALocation()
    {
        var handler = new RoutingHandler(request =>
            request.RequestUri!.AbsolutePath == MetadataPath
                ? Json(Metadata())
                : new HttpResponseMessage(HttpStatusCode.Found));

        using var directory = new TemporaryDirectory();
        var service = await CreateServiceAsync(handler);

        var response = await service.DownloadAttachmentAsync(CardId, AttachmentId, directory.Path, overwrite: false);

        Assert.False(response.Ok);
        Assert.Equal("REDIRECT_INVALID", response.Code);
    }

    [Fact]
    public async Task Download_RejectsALinkAttachmentWithoutRequestingAnything()
    {
        var handler = new RoutingHandler(_ => Json(
            "{\"id\":\"attachment-id\",\"name\":\"Spec\",\"url\":\"https://evil.example/spec.pdf\",\"isUpload\":false}"));

        using var directory = new TemporaryDirectory();
        var service = await CreateServiceAsync(handler);

        var response = await service.DownloadAttachmentAsync(CardId, AttachmentId, directory.Path, overwrite: false);

        Assert.False(response.Ok);
        Assert.Equal("LINK_ATTACHMENT", response.Code);
        Assert.Contains("https://evil.example/spec.pdf", response.Error!, StringComparison.Ordinal);

        // Only the metadata lookup: the CLI never fetches a third-party host on a card's behalf.
        Assert.Single(handler.Requests);
        Assert.Equal(MetadataPath, new Uri(handler.Requests[0].Uri).AbsolutePath);
    }

    [Fact]
    public async Task Download_IgnoresTheUrlTheAttachmentReportsAndUsesTheApiBase()
    {
        var handler = new RoutingHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path == MetadataPath)
                return Json("{\"id\":\"attachment-id\",\"name\":\"Spec\",\"fileName\":\"spec.pdf\"," +
                            "\"url\":\"https://evil.example/spec.pdf\",\"isUpload\":true}");
            return File(FileBody);
        });

        using var directory = new TemporaryDirectory();
        var service = await CreateServiceAsync(handler);

        var response = await service.DownloadAttachmentAsync(CardId, AttachmentId, directory.Path, overwrite: false);

        Assert.True(response.Ok, response.Error);
        Assert.All(handler.Requests, request =>
            Assert.StartsWith("https://api.trello.com/1/", request.Uri, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Download_RefusesToOverwriteUnlessAsked()
    {
        using var directory = new TemporaryDirectory();
        var existing = Path.Combine(directory.Path, "spec.pdf");
        await System.IO.File.WriteAllTextAsync(existing, "original");

        var service = await CreateServiceAsync(new RoutingHandler(SuccessfulDownload));
        var refused = await service.DownloadAttachmentAsync(CardId, AttachmentId, directory.Path, overwrite: false);

        Assert.False(refused.Ok);
        Assert.Equal("FILE_EXISTS", refused.Code);
        Assert.Equal("original", await System.IO.File.ReadAllTextAsync(existing));

        var replaced = await service.DownloadAttachmentAsync(CardId, AttachmentId, directory.Path, overwrite: true);

        Assert.True(replaced.Ok, replaced.Error);
        Assert.Equal(FileBody, await System.IO.File.ReadAllTextAsync(existing));
    }

    [Fact]
    public async Task Download_ReportsATruncatedTransferAndKeepsNoPartialFile()
    {
        var handler = new RoutingHandler(request =>
            request.RequestUri!.AbsolutePath == MetadataPath
                // Trello claims a larger file than the body that arrives.
                ? Json("{\"id\":\"attachment-id\",\"name\":\"Spec\",\"fileName\":\"spec.pdf\"," +
                       "\"isUpload\":true,\"bytes\":99999}")
                : File(FileBody));

        using var directory = new TemporaryDirectory();
        var service = await CreateServiceAsync(handler);

        var response = await service.DownloadAttachmentAsync(CardId, AttachmentId, directory.Path, overwrite: false);

        Assert.False(response.Ok);
        Assert.Equal("DOWNLOAD_INCOMPLETE", response.Code);
        Assert.Empty(Directory.GetFileSystemEntries(directory.Path));
    }

    [Fact]
    public async Task Download_LeavesNoTemporaryFileWhenTheTransferFails()
    {
        var handler = new RoutingHandler(request =>
            request.RequestUri!.AbsolutePath == MetadataPath
                ? Json(Metadata())
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new FailingContent() });

        using var directory = new TemporaryDirectory();
        var service = await CreateServiceAsync(handler);

        var response = await service.DownloadAttachmentAsync(CardId, AttachmentId, directory.Path, overwrite: false);

        Assert.False(response.Ok);
        Assert.Empty(Directory.GetFileSystemEntries(directory.Path));
    }

    [Fact]
    public async Task DownloadAll_SkipsLinkAttachmentsAndNumbersCollidingNames()
    {
        var handler = new RoutingHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/1/cards/card-id/attachments")
                return Json("""
                    [
                      {"id":"a1","name":"Report","fileName":"report.pdf","isUpload":true},
                      {"id":"a2","name":"Report","fileName":"report.pdf","isUpload":true},
                      {"id":"a3","name":"Homepage","url":"https://example.com/page","isUpload":false}
                    ]
                    """);
            return File(FileBody);
        });

        using var directory = new TemporaryDirectory();
        var target = Path.Combine(directory.Path, "attachments");
        var service = await CreateServiceAsync(handler);

        var response = await service.DownloadAllAttachmentsAsync(CardId, target, overwrite: false);

        Assert.True(response.Ok, response.Error);
        var summary = response.Data!;
        Assert.Equal(2, summary.Downloaded.Count);
        Assert.Empty(summary.Failed);

        // Both uploads land, neither overwrites the other.
        Assert.Equal(
            ["report-2.pdf", "report.pdf"],
            summary.Downloaded.Select(download => Path.GetFileName(download.Path)).Order().ToArray());

        var skipped = Assert.Single(summary.Skipped);
        Assert.Equal("a3", skipped.Id);
        Assert.Equal("LINK_ATTACHMENT", skipped.Reason);
        Assert.Equal("https://example.com/page", skipped.Url);

        // The link's host was never contacted.
        Assert.All(handler.Requests, request =>
            Assert.StartsWith("https://api.trello.com/1/", request.Uri, StringComparison.Ordinal));
    }

    [Fact]
    public async Task DownloadAll_KeepsATraversingFileNameInsideTheOutputDirectory()
    {
        var handler = new RoutingHandler(request =>
            request.RequestUri!.AbsolutePath == "/1/cards/card-id/attachments"
                ? Json("""[{"id":"a1","name":"Evil","fileName":"../../escaped.txt","isUpload":true}]""")
                : File(FileBody));

        using var directory = new TemporaryDirectory();
        var target = Path.Combine(directory.Path, "attachments");
        var service = await CreateServiceAsync(handler);

        var response = await service.DownloadAllAttachmentsAsync(CardId, target, overwrite: false);

        Assert.True(response.Ok, response.Error);
        var download = Assert.Single(response.Data!.Downloaded);
        Assert.StartsWith(Path.GetFullPath(target) + Path.DirectorySeparatorChar, download.Path, StringComparison.Ordinal);
        Assert.False(System.IO.File.Exists(Path.Combine(directory.Path, "escaped.txt")));
    }

    [Fact]
    public async Task Download_ReplacesASymlinkRatherThanWritingThroughIt()
    {
        // File.CreateSymbolicLink needs elevation on Windows.
        if (OperatingSystem.IsWindows()) return;

        using var directory = new TemporaryDirectory();
        var secret = Path.Combine(directory.Path, "secret.txt");
        await System.IO.File.WriteAllTextAsync(secret, "do not touch");

        var destination = Path.Combine(directory.Path, "spec.pdf");
        System.IO.File.CreateSymbolicLink(destination, secret);

        var service = await CreateServiceAsync(new RoutingHandler(SuccessfulDownload));
        var response = await service.DownloadAttachmentAsync(CardId, AttachmentId, destination, overwrite: true);

        Assert.True(response.Ok, response.Error);
        Assert.Equal("do not touch", await System.IO.File.ReadAllTextAsync(secret));
        Assert.Equal(FileBody, await System.IO.File.ReadAllTextAsync(destination));
        Assert.Null(new FileInfo(destination).LinkTarget);
    }

    private static HttpResponseMessage SuccessfulDownload(HttpRequestMessage request) =>
        request.RequestUri!.AbsolutePath == MetadataPath ? Json(Metadata()) : File(FileBody);

    private static string Metadata() =>
        "{\"id\":\"attachment-id\",\"name\":\"Spec\",\"fileName\":\"spec.pdf\"," +
        "\"mimeType\":\"application/pdf\",\"isUpload\":true}";

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage File(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body) };

    private static HttpResponseMessage Redirect(string location) =>
        new(HttpStatusCode.Found) { Headers = { Location = new Uri(location) } };

    private static async Task<TrelloApiService> CreateServiceAsync(HttpMessageHandler handler)
    {
        var config = new ConfigService(
            new FixedTokenCredentialStore(TokenCanary),
            name => name == "TRELLO_API_KEY" ? ApiKeyCanary : null,
            Path.Combine(Path.GetTempPath(), $"trello-download-tests-{Guid.NewGuid():N}.json"),
            _ => { });
        await config.LoadAsync();
        return new TrelloApiService(config, new HttpClient(handler));
    }

    private sealed class RoutingHandler(Func<HttpRequestMessage, HttpResponseMessage> route) : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new RecordedRequest(
                request.RequestUri!.ToString(),
                request.Headers.TryGetValues("Authorization", out var authorization) ? authorization.ToArray() : []));
            return Task.FromResult(route(request));
        }
    }

    private sealed record RecordedRequest(string Uri, IReadOnlyList<string> AuthorizationValues);

    private sealed class FailingContent : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            throw new IOException("the connection dropped");

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }

    private sealed class FixedTokenCredentialStore(string token) : ICredentialStore
    {
        public Task<string?> GetTokenAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>(token);
        public Task SetTokenAsync(string value, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteTokenAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"trello-download-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
        }
    }
}
