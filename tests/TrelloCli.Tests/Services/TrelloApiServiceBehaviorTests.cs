using System.Net;
using System.Text;
using TrelloCli.Credentials;
using TrelloCli.Models;
using TrelloCli.Services;
using Xunit;

namespace TrelloCli.Tests.Services;

/// <summary>
/// Pins the failure envelope of every API method. The methods do not agree with each other —
/// some map 404 to NOT_FOUND and some let it fall through to HTTP_ERROR, and the "Trello
/// answered but sent nothing usable" code differs per method — and those differences are
/// documented in the error-code table, so they are behaviour rather than accident. Refactoring
/// the shared error handling must not quietly change any of them.
/// </summary>
public class TrelloApiServiceBehaviorTests
{
    [Fact]
    public async Task EveryMethodReportsTheSameFailureEnvelopeAsBefore()
    {
        var snapshot = await BuildSnapshotAsync();

        Assert.Equal(Expected.Trim(), snapshot.Trim());
    }

    private static async Task<string> BuildSnapshotAsync()
    {
        var builder = new StringBuilder();

        foreach (var (scenario, responder) in Scenarios)
        {
            foreach (var (name, invoke) in Invocations)
            {
                var service = await CreateServiceAsync(new StubHandler(responder));
                var (ok, code, error) = await invoke(service);
                builder.AppendLine($"{scenario} | {name} | ok={ok} | {code} | {error}");
            }
        }

        return builder.ToString();
    }

    private static readonly (string Scenario, Func<HttpRequestMessage, HttpResponseMessage> Responder)[] Scenarios =
    [
        ("404", _ => new HttpResponseMessage(HttpStatusCode.NotFound)),
        ("429", _ => new HttpResponseMessage(HttpStatusCode.TooManyRequests)),
        ("null-body", _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("null") })
    ];

    private static readonly (string Name, Func<TrelloApiService, Task<(bool, string?, string?)>> Invoke)[] Invocations =
    [
        ("GetBoards", async api => Envelope(await api.GetBoardsAsync())),
        ("GetBoard", async api => Envelope(await api.GetBoardAsync("b"))),
        ("GetLists", async api => Envelope(await api.GetListsAsync("b"))),
        ("MoveList", async api => Envelope(await api.MoveListAsync("l", "top"))),
        ("CreateList", async api => Envelope(await api.CreateListAsync("b", "n"))),
        ("GetCardsInList", async api => Envelope(await api.GetCardsInListAsync("l"))),
        ("GetCardsInBoard", async api => Envelope(await api.GetCardsInBoardAsync("b"))),
        ("GetCard", async api => Envelope(await api.GetCardAsync("c"))),
        ("CreateCard", async api => Envelope(await api.CreateCardAsync("l", "n"))),
        ("UpdateCard", async api => Envelope(await api.UpdateCardAsync("c", name: "n"))),
        ("MoveCard", async api => Envelope(await api.MoveCardAsync("c", "l"))),
        ("DeleteCard", async api => Envelope(await api.DeleteCardAsync("c"))),
        ("GetComments", async api => Envelope(await api.GetCommentsAsync("c"))),
        ("AddComment", async api => Envelope(await api.AddCommentAsync("c", "t"))),
        ("GetLabels", async api => Envelope(await api.GetLabelsAsync("b"))),
        ("CreateLabel", async api => Envelope(await api.CreateLabelAsync("b", "n", "green"))),
        ("UpdateLabel", async api => Envelope(await api.UpdateLabelAsync("l", "n", null))),
        ("DeleteLabel", async api => Envelope(await api.DeleteLabelAsync("l"))),
        ("CheckAuth", async api => Envelope(await api.CheckAuthAsync())),
        ("GetAttachments", async api => Envelope(await api.GetAttachmentsAsync("c"))),
        ("GetAttachment", async api => Envelope(await api.GetAttachmentAsync("c", "a"))),
        ("AttachUrl", async api => Envelope(await api.AttachUrlAsync("c", "https://example.com"))),
        ("DeleteAttachment", async api => Envelope(await api.DeleteAttachmentAsync("c", "a"))),
        ("GetChecklists", async api => Envelope(await api.GetChecklistsAsync("c"))),
        ("CreateChecklist", async api => Envelope(await api.CreateChecklistAsync("c", "n"))),
        ("DeleteChecklist", async api => Envelope(await api.DeleteChecklistAsync("cl"))),
        ("AddChecklistItem", async api => Envelope(await api.AddChecklistItemAsync("cl", "n"))),
        ("UpdateChecklistItem", async api => Envelope(await api.UpdateChecklistItemAsync("c", "i", "complete"))),
        ("DeleteChecklistItem", async api => Envelope(await api.DeleteChecklistItemAsync("cl", "i")))
    ];

    private static (bool, string?, string?) Envelope<T>(ApiResponse<T> response) =>
        (response.Ok, response.Code, response.Error);

    private static async Task<TrelloApiService> CreateServiceAsync(HttpMessageHandler handler)
    {
        var config = new ConfigService(
            new FixedTokenCredentialStore("token-canary"),
            name => name == "TRELLO_API_KEY" ? "api-key-canary" : null,
            Path.Combine(Path.GetTempPath(), $"trello-behavior-{Guid.NewGuid():N}.json"),
            _ => { });
        await config.LoadAsync();
        return new TrelloApiService(config, new HttpClient(handler));
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }

    private sealed class FixedTokenCredentialStore(string token) : ICredentialStore
    {
        public Task<string?> GetTokenAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>(token);
        public Task SetTokenAsync(string value, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteTokenAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private const string Expected = """
        404 | GetBoards | ok=False | HTTP_ERROR | HTTP request failed with status 404.
        404 | GetBoard | ok=False | NOT_FOUND | Board not found
        404 | GetLists | ok=False | NOT_FOUND | Board not found
        404 | MoveList | ok=False | NOT_FOUND | List not found
        404 | CreateList | ok=False | HTTP_ERROR | HTTP request failed with status 404.
        404 | GetCardsInList | ok=False | NOT_FOUND | List not found
        404 | GetCardsInBoard | ok=False | NOT_FOUND | Board not found
        404 | GetCard | ok=False | NOT_FOUND | Card not found
        404 | CreateCard | ok=False | HTTP_ERROR | HTTP request failed with status 404.
        404 | UpdateCard | ok=False | NOT_FOUND | Card not found
        404 | MoveCard | ok=False | NOT_FOUND | Card not found
        404 | DeleteCard | ok=False | NOT_FOUND | Card not found
        404 | GetComments | ok=False | NOT_FOUND | Card not found
        404 | AddComment | ok=False | NOT_FOUND | Card not found
        404 | GetLabels | ok=False | NOT_FOUND | Board not found
        404 | CreateLabel | ok=False | HTTP_ERROR | HTTP request failed with status 404.
        404 | UpdateLabel | ok=False | NOT_FOUND | Label not found
        404 | DeleteLabel | ok=False | NOT_FOUND | Label not found
        404 | CheckAuth | ok=False | HTTP_ERROR | HTTP request failed with status 404.
        404 | GetAttachments | ok=False | NOT_FOUND | Card not found
        404 | GetAttachment | ok=False | NOT_FOUND | Attachment not found
        404 | AttachUrl | ok=False | NOT_FOUND | Card not found
        404 | DeleteAttachment | ok=False | NOT_FOUND | Attachment not found
        404 | GetChecklists | ok=False | NOT_FOUND | Card not found
        404 | CreateChecklist | ok=False | NOT_FOUND | Card not found
        404 | DeleteChecklist | ok=False | NOT_FOUND | Checklist not found
        404 | AddChecklistItem | ok=False | NOT_FOUND | Checklist not found
        404 | UpdateChecklistItem | ok=False | NOT_FOUND | Card or checklist item not found
        404 | DeleteChecklistItem | ok=False | NOT_FOUND | Checklist or item not found
        429 | GetBoards | ok=False | HTTP_ERROR | HTTP request failed with status 429.
        429 | GetBoard | ok=False | HTTP_ERROR | HTTP request failed with status 429.
        429 | GetLists | ok=False | HTTP_ERROR | HTTP request failed with status 429.
        429 | MoveList | ok=False | HTTP_ERROR | HTTP request failed with status 429.
        429 | CreateList | ok=False | HTTP_ERROR | HTTP request failed with status 429.
        429 | GetCardsInList | ok=False | HTTP_ERROR | HTTP request failed with status 429.
        429 | GetCardsInBoard | ok=False | HTTP_ERROR | HTTP request failed with status 429.
        429 | GetCard | ok=False | HTTP_ERROR | HTTP request failed with status 429.
        429 | CreateCard | ok=False | HTTP_ERROR | HTTP request failed with status 429.
        429 | UpdateCard | ok=False | HTTP_ERROR | HTTP request failed with status 429.
        429 | MoveCard | ok=False | HTTP_ERROR | HTTP request failed with status 429.
        429 | DeleteCard | ok=False | HTTP_ERROR | HTTP request failed with status 429.
        429 | GetComments | ok=False | HTTP_ERROR | HTTP request failed with status 429.
        429 | AddComment | ok=False | HTTP_ERROR | HTTP request failed with status 429.
        429 | GetLabels | ok=False | HTTP_ERROR | HTTP request failed with status 429.
        429 | CreateLabel | ok=False | HTTP_ERROR | HTTP request failed with status 429.
        429 | UpdateLabel | ok=False | HTTP_ERROR | HTTP request failed with status 429.
        429 | DeleteLabel | ok=False | HTTP_ERROR | HTTP request failed with status 429.
        429 | CheckAuth | ok=False | HTTP_ERROR | HTTP request failed with status 429.
        429 | GetAttachments | ok=False | HTTP_ERROR | HTTP request failed with status 429.
        429 | GetAttachment | ok=False | HTTP_ERROR | HTTP request failed with status 429.
        429 | AttachUrl | ok=False | HTTP_ERROR | HTTP request failed with status 429.
        429 | DeleteAttachment | ok=False | HTTP_ERROR | HTTP request failed with status 429.
        429 | GetChecklists | ok=False | HTTP_ERROR | HTTP request failed with status 429.
        429 | CreateChecklist | ok=False | HTTP_ERROR | HTTP request failed with status 429.
        429 | DeleteChecklist | ok=False | HTTP_ERROR | HTTP request failed with status 429.
        429 | AddChecklistItem | ok=False | HTTP_ERROR | HTTP request failed with status 429.
        429 | UpdateChecklistItem | ok=False | HTTP_ERROR | HTTP request failed with status 429.
        429 | DeleteChecklistItem | ok=False | HTTP_ERROR | HTTP request failed with status 429.
        null-body | GetBoards | ok=True |  | 
        null-body | GetBoard | ok=False | NOT_FOUND | Board not found
        null-body | GetLists | ok=True |  | 
        null-body | MoveList | ok=False | UPDATE_FAILED | Failed to move list
        null-body | CreateList | ok=False | CREATE_FAILED | Failed to create list
        null-body | GetCardsInList | ok=True |  | 
        null-body | GetCardsInBoard | ok=True |  | 
        null-body | GetCard | ok=False | NOT_FOUND | Card not found
        null-body | CreateCard | ok=False | CREATE_FAILED | Failed to create card
        null-body | UpdateCard | ok=False | UPDATE_FAILED | Failed to update card
        null-body | MoveCard | ok=False | UPDATE_FAILED | Failed to update card
        null-body | DeleteCard | ok=True |  | 
        null-body | GetComments | ok=True |  | 
        null-body | AddComment | ok=False | CREATE_FAILED | Failed to add comment
        null-body | GetLabels | ok=True |  | 
        null-body | CreateLabel | ok=False | CREATE_FAILED | Failed to create label
        null-body | UpdateLabel | ok=False | UPDATE_FAILED | Failed to update label
        null-body | DeleteLabel | ok=True |  | 
        null-body | CheckAuth | ok=False | ERROR | Unexpected error occurred.
        null-body | GetAttachments | ok=True |  | 
        null-body | GetAttachment | ok=False | NOT_FOUND | Attachment not found
        null-body | AttachUrl | ok=False | ATTACH_FAILED | Failed to attach URL
        null-body | DeleteAttachment | ok=True |  | 
        null-body | GetChecklists | ok=True |  | 
        null-body | CreateChecklist | ok=False | CREATE_FAILED | Failed to create checklist
        null-body | DeleteChecklist | ok=True |  | 
        null-body | AddChecklistItem | ok=False | CREATE_FAILED | Failed to add checklist item
        null-body | UpdateChecklistItem | ok=False | UPDATE_FAILED | Failed to update checklist item
        null-body | DeleteChecklistItem | ok=True |  |
        """;
}
