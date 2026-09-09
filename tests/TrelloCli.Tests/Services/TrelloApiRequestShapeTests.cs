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

    [Fact]
    public async Task CopyCardKeepsEverythingUnlessToldOtherwise()
    {
        var (service, handler) = await CreateAsync("{}");

        await service.CopyCardAsync("card-1", "list-2", name: null, position: null, keep: null);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/1/cards", request.Path);
        Assert.Contains("idCardSource=card-1", request.Content);
        Assert.Contains("idList=list-2", request.Content);
        // Trello's own default copies the name alone, which loses the description,
        // checklists, labels and members.
        Assert.Contains("keepFromSource=all", request.Content);
    }

    [Fact]
    public async Task SetCardCoverSendsTheCoverAsJsonInsideTheFormField()
    {
        var (service, handler) = await CreateAsync("{}");

        await service.SetCardCoverAsync("card-1", color: "blue", attachmentId: null, size: "full", brightness: null);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal("/1/cards/card-1", request.Path);
        var decoded = Uri.UnescapeDataString(request.Content.Replace("+", " "));
        Assert.Contains("""cover={"color":"blue","size":"full"}""", decoded);
    }

    [Fact]
    public async Task SetCardCoverWithoutAnyOptionIsRejectedWithoutARequest()
    {
        var (service, handler) = await CreateAsync("{}");

        var response = await service.SetCardCoverAsync("card-1", null, null, null, null);

        Assert.False(response.Ok);
        Assert.Equal("NO_PARAMS", response.Code);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ClearCardCoverSendsAnEmptyCover()
    {
        var (service, handler) = await CreateAsync("{}");

        await service.ClearCardCoverAsync("card-1");

        Assert.Equal("cover=", Assert.Single(handler.Requests).Content);
    }

    [Fact]
    public async Task ArchiveListPutsTheClosedFlag()
    {
        var (service, handler) = await CreateAsync("{}");

        await service.SetListClosedAsync("list-1", closed: true);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal("/1/lists/list-1/closed", request.Path);
        Assert.Equal("value=true", request.Content);
    }

    [Fact]
    public async Task MoveAllCardsResolvesTheTargetBoardBeforeMoving()
    {
        // The caller has list IDs; Trello wants the destination board alongside the
        // destination list, so the target list is read first.
        var (service, handler) = await CreateAsync("""{"id":"list-2","idBoard":"board-9"}""");

        await service.MoveAllCardsAsync("list-1", "list-2");

        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal("/1/lists/list-2", handler.Requests[0].Path);
        Assert.Equal(HttpMethod.Post, handler.Requests[1].Method);
        Assert.Equal("/1/lists/list-1/moveAllCards", handler.Requests[1].Path);
        Assert.Contains("idBoard=board-9", handler.Requests[1].Query);
        Assert.Contains("idList=list-2", handler.Requests[1].Query);
    }

    [Fact]
    public async Task GetCardActivityDefaultsToEveryActionType()
    {
        var (service, handler) = await CreateAsync("[]");

        await service.GetCardActivityAsync("card-1", limit: null, filter: null);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("/1/cards/card-1/actions", request.Path);
        Assert.Equal("filter=all", request.Query);
    }

    [Theory]
    [InlineData(null, "filter=open")]
    [InlineData("all", "filter=all")]
    [InlineData("closed", "filter=closed")]
    public async Task ReadCommandsDefaultToOpenItemsAndHonourTheFilter(string? filter, string expected)
    {
        var (service, handler) = await CreateAsync("[]");

        await service.GetBoardsAsync(filter);
        await service.GetListsAsync("board-1", filter);
        await service.GetCardsInBoardAsync("board-1", filter);

        Assert.All(handler.Requests, request => Assert.Equal(expected, request.Query));
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
