using System.Net;
using System.Net.Sockets;
using System.Text;
using TrelloCli.Credentials;
using TrelloCli.Services;
using Xunit;

namespace TrelloCli.Tests.Services;

/// <summary>
/// The handler-based download tests never touch HttpClient's own redirect machinery, because a
/// stub handler short-circuits it. This one drives the real production client over real sockets,
/// which is what proves the Authorization header does not reach the redirect target: HttpClient
/// forwards a manually set header across a redirect, so the guarantee rests entirely on
/// AllowAutoRedirect being off and the hop being taken by hand.
///
/// Two ports on the loopback address are two origins, the same way a browser treats them, so the
/// redirect target is "another host" as far as the credential check is concerned. That avoids
/// needing IPv6 to express it, which containers frequently do not have -- the cross-host redirect
/// test in TrelloApiServiceTests returns early without one, so it does not run everywhere.
/// </summary>
public class AttachmentDownloadLoopbackTests
{
    private const string ApiKeyCanary = "api-key-canary";
    private const string TokenCanary = "token-canary";
    private const string FileBody = "downloaded-bytes";

    [Fact]
    public async Task ProductionClientDownloadsThroughACrossHostRedirectWithoutLeakingCredentials()
    {
        using var trello = new LoopbackServer(IPAddress.Loopback);
        using var storage = new LoopbackServer(IPAddress.Loopback);

        var storageUrl = $"http://127.0.0.1:{storage.Port}/presigned/spec.pdf?signature=abc";

        var trelloRequests = trello.ServeAsync(request =>
            request.Contains("/download/", StringComparison.Ordinal)
                ? Redirect(storageUrl)
                : Ok("{\"id\":\"a1\",\"name\":\"Spec\",\"fileName\":\"spec.pdf\",\"isUpload\":true}"));

        var storageRequests = storage.ServeAsync(_ => Ok(FileBody));

        using var directory = new TemporaryDirectory();
        using var http = TrelloApiService.CreateProductionHttpClient();
        var service = new TrelloApiService(await CreateConfigAsync(), http, $"http://127.0.0.1:{trello.Port}/1");

        var response = await service.DownloadAttachmentAsync("card-1", "a1", directory.Path, overwrite: false);

        Assert.True(response.Ok, response.Error);
        Assert.Equal(FileBody, await File.ReadAllTextAsync(response.Data!.Path));

        // The serve loops run until their listener closes, so close them before collecting.
        trello.Dispose();
        storage.Dispose();

        var toTrello = await trelloRequests;
        var toStorage = await storageRequests;

        Assert.Equal(2, toTrello.Count);
        Assert.All(toTrello, request => Assert.Contains("Authorization: OAuth", request, StringComparison.Ordinal));

        // The whole point: the presigned host is reached, and sees no credentials.
        var storageRequest = Assert.Single(toStorage);
        Assert.DoesNotContain("Authorization", storageRequest, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(ApiKeyCanary, storageRequest, StringComparison.Ordinal);
        Assert.DoesNotContain(TokenCanary, storageRequest, StringComparison.Ordinal);
    }

    private static string Ok(string body) =>
        "HTTP/1.1 200 OK\r\n" +
        "Content-Type: application/json\r\n" +
        $"Content-Length: {Encoding.UTF8.GetByteCount(body)}\r\n" +
        "Connection: close\r\n\r\n" + body;

    private static string Redirect(string location) =>
        "HTTP/1.1 302 Found\r\n" +
        $"Location: {location}\r\n" +
        "Content-Length: 0\r\n" +
        "Connection: close\r\n\r\n";

    private static async Task<ConfigService> CreateConfigAsync()
    {
        var config = new ConfigService(
            new FixedTokenCredentialStore(TokenCanary),
            name => name == "TRELLO_API_KEY" ? ApiKeyCanary : null,
            Path.Combine(Path.GetTempPath(), $"trello-loopback-{Guid.NewGuid():N}.json"),
            _ => { });
        await config.LoadAsync();
        return config;
    }

    private sealed class LoopbackServer : IDisposable
    {
        private readonly TcpListener _listener;
        private bool _disposed;

        public LoopbackServer(IPAddress address)
        {
            _listener = new TcpListener(address, 0);
            _listener.Start();
        }

        public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

        /// <summary>Answers requests until the test disposes the listener, returning what it saw.</summary>
        public Task<List<string>> ServeAsync(Func<string, string> respond) => Task.Run(async () =>
        {
            var seen = new List<string>();
            try
            {
                while (true)
                {
                    using var client = await _listener.AcceptTcpClientAsync();
                    await using var stream = client.GetStream();

                    var buffer = new byte[8192];
                    var read = await stream.ReadAsync(buffer);
                    var request = Encoding.UTF8.GetString(buffer, 0, read);
                    seen.Add(request);

                    var response = Encoding.UTF8.GetBytes(respond(request));
                    await stream.WriteAsync(response);
                    await stream.FlushAsync();
                }
            }
            catch (Exception)
            {
                // The listener is disposed once the download finishes; that ends the loop.
            }

            return seen;
        });

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;
            _listener.Dispose();
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
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"trello-loopback-dl-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
        }
    }
}
