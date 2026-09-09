using System.Net;
using System.Text;
using TrelloCli.Credentials;
using TrelloCli.Services;
using Xunit;

namespace TrelloCli.Tests.Services;

/// <summary>
/// Checks the request each endpoint actually sends: method, path, query and body. Nothing
/// covered this before, so a wrong verb or a misspelled Trello field name would only have
/// surfaced against the live API.
/// </summary>
public class TrelloApiRequestShapeTests
{
    [Fact]
    public async Task SearchSendsTheQueryTrelloExpects()
    {
        var (service, handler) = await CreateAsync("{}");

        await service.SearchAsync("login page", boardId: "board-1", limit: 10, cardsOnly: true);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/1/search", request.Path);
        Assert.Contains("query=login%20page", request.Query);
        Assert.Contains("modelTypes=cards", request.Query);
        Assert.Contains("idBoards=board-1", request.Query);
        Assert.Contains("cards_limit=10", request.Query);
        // Partial matching, so searching for a fragment of a title finds the card.
        Assert.Contains("partial=true", request.Query);
    }

    [Fact]
    public async Task SearchWithoutCardsOnlyAsksForBoardsAndMembersToo()
    {
        var (service, handler) = await CreateAsync("{}");

        await service.SearchAsync("q", boardId: null, limit: null, cardsOnly: false);

        var request = Assert.Single(handler.Requests);
        Assert.Contains("modelTypes=cards,boards,members", request.Query);
        Assert.DoesNotContain("idBoards", request.Query);
        Assert.DoesNotContain("cards_limit", request.Query);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(5000, 1000)]
    public async Task SearchClampsTheLimitToWhatTrelloAccepts(int requested, int expected)
    {
        var (service, handler) = await CreateAsync("{}");

        await service.SearchAsync("q", boardId: null, limit: requested, cardsOnly: true);

        Assert.Contains($"cards_limit={expected}", Assert.Single(handler.Requests).Query);
    }

    [Fact]
    public async Task AddCardMemberPostsTheMemberAsAValueRatherThanReplacingTheSet()
    {
        var (service, handler) = await CreateAsync("[]");

        await service.AddCardMemberAsync("card-1", "member-1");

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/1/cards/card-1/idMembers", request.Path);
        Assert.Equal("value=member-1", request.Content);
    }

    [Fact]
    public async Task RemoveCardMemberDeletesTheMemberSpecificPath()
    {
        var (service, handler) = await CreateAsync("[]");

        await service.RemoveCardMemberAsync("card-1", "member-1");

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Equal("/1/cards/card-1/idMembers/member-1", request.Path);
    }

    [Fact]
    public async Task AddCardLabelPostsTheLabelAsAValue()
    {
        var (service, handler) = await CreateAsync("[]");

        await service.AddCardLabelAsync("card-1", "label-1");

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/1/cards/card-1/idLabels", request.Path);
        Assert.Equal("value=label-1", request.Content);
    }

    [Fact]
    public async Task RemoveCardLabelDeletesTheLabelSpecificPath()
    {
        var (service, handler) = await CreateAsync("[]");

        await service.RemoveCardLabelAsync("card-1", "label-1");

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Equal("/1/cards/card-1/idLabels/label-1", request.Path);
    }

    [Fact]
    public async Task UpdateCommentPutsTheTextOnTheCommentAction()
    {
        var (service, handler) = await CreateAsync("{}");

        await service.UpdateCommentAsync("card-1", "action-1", "Corrected note");

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal("/1/cards/card-1/actions/action-1/comments", request.Path);
        Assert.Contains("text=Corrected%20note", request.Query);
    }

    [Fact]
    public async Task DeleteCommentDeletesTheCommentAction()
    {
        var (service, handler) = await CreateAsync("{}");

        await service.DeleteCommentAsync("card-1", "action-1");

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Equal("/1/cards/card-1/actions/action-1/comments", request.Path);
    }

    [Fact]
    public async Task GetMyCardsDefaultsToOpenCards()
    {
        var (service, handler) = await CreateAsync("[]");

        await service.GetMyCardsAsync(filter: null);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("/1/members/me/cards", request.Path);
        Assert.Equal("filter=open", request.Query);
    }

    [Fact]
    public async Task EveryNewEndpointStillKeepsCredentialsOutOfTheUrlAndBody()
    {
        var (service, handler) = await CreateAsync("{}");

        await service.SearchAsync("q", null, null, false);
        await service.SearchMembersAsync("q", 5);
        await service.GetMeAsync();
        await service.GetBoardMembersAsync("board-1");
        await service.AddCardMemberAsync("card-1", "member-1");
        await service.AddCardLabelAsync("card-1", "label-1");
        await service.UpdateCommentAsync("card-1", "action-1", "text");

        Assert.Equal(7, handler.Requests.Count);
        foreach (var request in handler.Requests)
        {
            Assert.Equal(
                "OAuth oauth_consumer_key=\"api-key-canary\", oauth_token=\"token-canary\"",
                Assert.Single(request.AuthorizationValues));
            Assert.DoesNotContain("api-key-canary", request.Path + request.Query, StringComparison.Ordinal);
            Assert.DoesNotContain("token-canary", request.Path + request.Query, StringComparison.Ordinal);
            Assert.DoesNotContain("api-key-canary", request.Content, StringComparison.Ordinal);
            Assert.DoesNotContain("token-canary", request.Content, StringComparison.Ordinal);
        }
    }

    private static async Task<(TrelloApiService Service, RecordingHandler Handler)> CreateAsync(string body)
    {
        var handler = new RecordingHandler(body);
        var config = new ConfigService(
            new FixedTokenCredentialStore("token-canary"),
            name => name == "TRELLO_API_KEY" ? "api-key-canary" : null,
            Path.Combine(Path.GetTempPath(), $"trello-shape-{Guid.NewGuid():N}.json"),
            _ => { });
        await config.LoadAsync();
        return (new TrelloApiService(config, new HttpClient(handler)), handler);
    }

    private sealed class RecordingHandler(string body) : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new RecordedRequest(
                request.Method,
                request.RequestUri!.AbsolutePath,
                Uri.UnescapeDataString(request.RequestUri.Query.TrimStart('?')).Replace(" ", "%20"),
                request.Headers.TryGetValues("Authorization", out var authorization) ? authorization.ToArray() : [],
                request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken)));

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed record RecordedRequest(
        HttpMethod Method,
        string Path,
        string Query,
        IReadOnlyList<string> AuthorizationValues,
        string Content);

    private sealed class FixedTokenCredentialStore(string token) : ICredentialStore
    {
        public Task<string?> GetTokenAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>(token);
        public Task SetTokenAsync(string value, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteTokenAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
