using System.Net;
using System.Net.Sockets;
using System.Text;
using TrelloCli;
using TrelloCli.Credentials;
using TrelloCli.Services;
using Xunit;

namespace TrelloCli.Tests.Services;

public class TrelloApiServiceTests
{
    private const string ApiKeyCanary = "api-key-canary";
    private const string TokenCanary = "token-canary";
    private const string ExpectedAuthorization = "OAuth oauth_consumer_key=\"api-key-canary\", oauth_token=\"token-canary\"";

    [Fact]
    public async Task RepresentativeRequests_UseExactlyOneAuthorizationHeaderAndKeepCredentialsOutOfUrisAndBodies()
    {
        var handler = new RecordingHandler(request => SuccessFor(request.RequestUri!.AbsolutePath));
        var service = await CreateServiceAsync(handler);

        await service.GetBoardsAsync();
        await service.CreateCardAsync("list-id", "Card name", desc: "Details");
        await service.UpdateCardAsync("card-id", name: "Renamed");
        await service.DeleteCardAsync("card-id");
        using var upload = new TemporaryFile("upload body");
        await service.UploadAttachmentAsync("card-id", upload.Path, "photo.txt");

        Assert.Collection(
            handler.Requests,
            request => Assert.Equal(HttpMethod.Get, request.Method),
            request => Assert.Equal(HttpMethod.Post, request.Method),
            request => Assert.Equal(HttpMethod.Put, request.Method),
            request => Assert.Equal(HttpMethod.Delete, request.Method),
            request => Assert.Equal(HttpMethod.Post, request.Method));

        foreach (var request in handler.Requests)
        {
            Assert.Collection(request.AuthorizationValues,
                authorization => Assert.Equal(ExpectedAuthorization, authorization));
            Assert.DoesNotContain(ApiKeyCanary, request.Uri, StringComparison.Ordinal);
            Assert.DoesNotContain(TokenCanary, request.Uri, StringComparison.Ordinal);
            Assert.DoesNotContain(ApiKeyCanary, request.Content, StringComparison.Ordinal);
            Assert.DoesNotContain(TokenCanary, request.Content, StringComparison.Ordinal);
        }

        Assert.Equal("filter=open", handler.Requests[0].Query);
        Assert.Contains("idList=list-id", handler.Requests[1].Content);
        Assert.Contains("name=Renamed", handler.Requests[2].Content);
        Assert.Contains("upload body", handler.Requests[4].Content);
    }

    [Fact]
    public async Task CheckAuth_UsesTheAuthorizationHeaderAndLeavesCredentialsOutOfTheUri()
    {
        var handler = new RecordingHandler(_ => JsonResponse("{\"id\":\"member-id\",\"username\":\"member\",\"fullName\":\"Member Name\"}"));
        var service = await CreateServiceAsync(handler);

        var response = await service.CheckAuthAsync();

        Assert.True(response.Ok);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Collection(request.AuthorizationValues,
            authorization => Assert.Equal(ExpectedAuthorization, authorization));
        Assert.Equal("fields=id,username,fullName", request.Query);
        Assert.DoesNotContain(ApiKeyCanary, request.Uri, StringComparison.Ordinal);
        Assert.DoesNotContain(TokenCanary, request.Uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CheckAuthCommand_UsesTheSafeHttpPipelineWithoutWritingCredentials()
    {
        var handler = new RecordingHandler(_ => JsonResponse("{\"id\":\"member-id\",\"username\":\"member\",\"fullName\":\"Member Name\"}"));
        var config = await CreateConfigAsync();
        var api = new TrelloApiService(config, new HttpClient(handler));
        using var output = new StringWriter();
        using var error = new StringWriter();
        var application = new CliApplication(
            config,
            new FixedSecretReader(),
            output,
            error,
            new ApiServiceFactory(api));

        await application.RunAsync(["--check-auth"]);

        Assert.Contains("member-id", output.ToString());
        Assert.DoesNotContain(ApiKeyCanary, output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(TokenCanary, output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(ApiKeyCanary, error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(TokenCanary, error.ToString(), StringComparison.Ordinal);
        Assert.Collection(Assert.Single(handler.Requests).AuthorizationValues,
            authorization => Assert.Equal(ExpectedAuthorization, authorization));
    }

    [Fact]
    public async Task RedirectResponse_IsNotFollowedAndReturnsASanitizedHttpFailure()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Found)
        {
            Headers = { Location = new Uri("https://credentials.example/redirect") }
        });
        var service = await CreateServiceAsync(handler);

        var response = await service.GetBoardsAsync();

        Assert.False(response.Ok);
        Assert.Equal("HTTP_ERROR", response.Code);
        Assert.Equal("HTTP request failed.", response.Error);
        Assert.Single(handler.Requests);
        Assert.Equal("https://api.trello.com/1/members/me/boards?filter=open", handler.Requests[0].Uri);
    }

    [Fact]
    public async Task ProductionClient_DoesNotFollowACrossHostRedirectThatCouldReceiveTheAuthorizationHeader()
    {
        using var servers = new RedirectingLoopbackServers();
        var config = await CreateConfigAsync();
        using var http = TrelloApiService.CreateProductionHttpClient();
        var service = new TrelloApiService(config, http, servers.SourceBaseUrl);
        var initialRequest = servers.RespondWithCrossHostRedirectAsync();
        var redirectedRequest = servers.WaitForRedirectTargetRequestAsync();

        var response = await service.GetBoardsAsync();

        Assert.False(response.Ok);
        Assert.Equal("HTTP_ERROR", response.Code);
        Assert.Equal("HTTP request failed.", response.Error);
        Assert.Contains($"Authorization: {ExpectedAuthorization}", await initialRequest, StringComparison.Ordinal);
        Assert.Null(await redirectedRequest);
    }

    [Fact]
    public async Task HandlerExceptions_ReturnStableSanitizedErrorsWithoutCredentialCanaries()
    {
        const string exceptionCanary = "handler-exception-token-canary";
        var handler = new RecordingHandler(_ => throw new InvalidOperationException(exceptionCanary));
        var service = await CreateServiceAsync(handler);

        var response = await service.GetBoardsAsync();

        Assert.False(response.Ok);
        Assert.Equal("ERROR", response.Code);
        Assert.Equal("Unexpected error occurred.", response.Error);
        Assert.DoesNotContain(exceptionCanary, response.Error, StringComparison.Ordinal);
        Assert.DoesNotContain(ApiKeyCanary, response.Error, StringComparison.Ordinal);
        Assert.DoesNotContain(TokenCanary, response.Error, StringComparison.Ordinal);
    }

    private static async Task<TrelloApiService> CreateServiceAsync(HttpMessageHandler handler) =>
        new(await CreateConfigAsync(), new HttpClient(handler));

    private static async Task<ConfigService> CreateConfigAsync()
    {
        var config = new ConfigService(
            new FixedTokenCredentialStore(TokenCanary),
            name => name == "TRELLO_API_KEY" ? ApiKeyCanary : null,
            Path.Combine(Path.GetTempPath(), $"trello-api-tests-{Guid.NewGuid():N}.json"),
            _ => { });
        await config.LoadAsync();
        return config;
    }

    private static HttpResponseMessage SuccessFor(string path) => path switch
    {
        "/1/members/me/boards" => JsonResponse("[]"),
        "/1/cards" => JsonResponse("{}"),
        "/1/cards/card-id" => JsonResponse("{}"),
        "/1/cards/card-id/attachments" => JsonResponse("{}"),
        _ => new HttpResponseMessage(HttpStatusCode.OK)
    };

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json)
    };

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new RecordedRequest(
                request.Method,
                request.RequestUri!.ToString(),
                request.RequestUri.Query.TrimStart('?'),
                request.Headers.TryGetValues("Authorization", out var authorizationValues)
                    ? authorizationValues.ToArray()
                    : [],
                request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken)));
            return response(request);
        }
    }

    private sealed record RecordedRequest(
        HttpMethod Method,
        string Uri,
        string Query,
        IReadOnlyList<string> AuthorizationValues,
        string Content);

    private sealed class FixedTokenCredentialStore(string token) : ICredentialStore
    {
        public Task<string?> GetTokenAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>(token);
        public Task SetTokenAsync(string value, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteTokenAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FixedSecretReader : ISecretReader
    {
        public SecretReadResult ReadToken(TextWriter errorWriter) => SecretReadResult.Success("unused-token");
    }

    private sealed class ApiServiceFactory(TrelloApiService api) : ICliServiceFactory
    {
        public CliServices Create(ConfigService config) => new(api, new IgnoredCommandDispatcher());
    }

    private sealed class IgnoredCommandDispatcher : ICommandDispatcher
    {
        public Task ExecuteAsync(string[] args) => Task.CompletedTask;
    }

    private sealed class TemporaryFile : IDisposable
    {
        public TemporaryFile(string content)
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"trello-upload-{Guid.NewGuid():N}.txt");
            File.WriteAllText(Path, content);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (File.Exists(Path)) File.Delete(Path);
        }
    }

    private sealed class RedirectingLoopbackServers : IDisposable
    {
        private readonly TcpListener _source = new(IPAddress.IPv6Loopback, 0);
        private readonly TcpListener _target = new(IPAddress.Loopback, 0);

        public RedirectingLoopbackServers()
        {
            _source.Start();
            _target.Start();
        }

        public string SourceBaseUrl => $"http://[::1]:{((IPEndPoint)_source.LocalEndpoint).Port}/1";

        public async Task<string> RespondWithCrossHostRedirectAsync()
        {
            using var client = await _source.AcceptTcpClientAsync();
            var request = await ReadRequestAsync(client);
            var targetUrl = $"http://127.0.0.1:{((IPEndPoint)_target.LocalEndpoint).Port}/redirected";
            await WriteResponseAsync(client, "302 Found", $"Location: {targetUrl}\r\n");
            return request;
        }

        public async Task<string?> WaitForRedirectTargetRequestAsync()
        {
            var accept = _target.AcceptTcpClientAsync();
            if (await Task.WhenAny(accept, Task.Delay(TimeSpan.FromMilliseconds(300))) != accept)
                return null;

            using var client = await accept;
            var request = await ReadRequestAsync(client);
            await WriteResponseAsync(client, "200 OK", "", "[]");
            return request;
        }

        private static async Task<string> ReadRequestAsync(TcpClient client)
        {
            using var reader = new StreamReader(client.GetStream(), Encoding.ASCII, leaveOpen: true);
            var lines = new List<string>();
            string? line;
            while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync())) lines.Add(line);
            return string.Join("\n", lines);
        }

        private static async Task WriteResponseAsync(TcpClient client, string status, string headers, string body = "")
        {
            var response = $"HTTP/1.1 {status}\r\nContent-Length: {Encoding.UTF8.GetByteCount(body)}\r\nConnection: close\r\n{headers}\r\n{body}";
            await client.GetStream().WriteAsync(Encoding.UTF8.GetBytes(response));
        }

        public void Dispose()
        {
            _source.Stop();
            _target.Stop();
        }
    }
}
