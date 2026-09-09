using TrelloCli.Commands;
using TrelloCli.Models;
using TrelloCli.Utils;

namespace TrelloCli;

public sealed class CommandDispatcher : ICommandDispatcher
{
    private readonly Dictionary<string, Func<string[], Task>> _handlers;
    private readonly TextWriter _output;

    public CommandDispatcher(Services.TrelloApiService api, TextWriter output)
    {
        var board = new BoardCommands(api, output);
        var lists = new ListCommands(api, output);
        var cards = new CardCommands(api, output);
        var attachments = new AttachmentCommands(api, output);
        var checklists = new ChecklistCommands(api, output);
        var labels = new LabelCommands(api, output);
        var members = new MemberCommands(api, output);
        var search = new SearchCommands(api, output);
        _output = output;

        // Keys must match CommandCatalog.DispatchedCommands; a test asserts both directions.
        _handlers = new Dictionary<string, Func<string[], Task>>(StringComparer.Ordinal)
        {
            ["--get-boards"] = _ => board.GetBoardsAsync(),
            ["--get-board"] = args => board.GetBoardAsync(GetArg(args, 1)),

            ["--get-lists"] = args => lists.GetListsAsync(GetArg(args, 1)),
            ["--create-list"] = args => lists.CreateListAsync(GetArg(args, 1), GetArg(args, 2)),
            ["--move-list"] = args => lists.MoveListAsync(GetArg(args, 1), GetArg(args, 2)),
            ["--bulk-move-lists"] = args => lists.BulkMoveListsAsync(args[1..]),

            ["--get-cards"] = args => cards.GetCardsAsync(GetArg(args, 1)),
            ["--get-all-cards"] = args => cards.GetAllCardsAsync(GetArg(args, 1)),
            ["--get-card"] = args => cards.GetCardAsync(GetArg(args, 1)),
            ["--create-card"] = args => cards.CreateCardAsync(
                GetArg(args, 1),
                GetArg(args, 2),
                GetNamedArg(args, "--desc"),
                GetNamedArg(args, "--due"),
                GetNamedArg(args, "--labels"),
                GetNamedArg(args, "--members")),
            ["--update-card"] = args => cards.UpdateCardAsync(
                GetArg(args, 1),
                GetNamedArg(args, "--name"),
                GetNamedArg(args, "--desc"),
                GetNamedArg(args, "--due"),
                GetNamedArg(args, "--labels"),
                GetNamedArg(args, "--members"),
                GetClosedArg(args)),
            ["--archive-card"] = args => cards.ArchiveCardAsync(GetArg(args, 1)),
            ["--unarchive-card"] = args => cards.UnarchiveCardAsync(GetArg(args, 1)),
            ["--move-card"] = args => cards.MoveCardAsync(GetArg(args, 1), GetArg(args, 2)),
            ["--delete-card"] = args => cards.DeleteCardAsync(GetArg(args, 1)),
            ["--get-comments"] = args => cards.GetCommentsAsync(GetArg(args, 1)),
            ["--add-comment"] = args => cards.AddCommentAsync(GetArg(args, 1), GetArg(args, 2)),

            ["--list-attachments"] = args => attachments.GetAttachmentsAsync(GetArg(args, 1)),
            ["--upload-attachment"] = args => attachments.UploadAttachmentAsync(
                GetArg(args, 1),
                GetArg(args, 2),
                GetNamedArg(args, "--name")),
            ["--attach-url"] = args => attachments.AttachUrlAsync(
                GetArg(args, 1),
                GetArg(args, 2),
                GetNamedArg(args, "--name")),
            ["--get-attachment"] = args => attachments.GetAttachmentAsync(GetArg(args, 1), GetArg(args, 2)),
            ["--download-attachment"] = args => attachments.DownloadAttachmentAsync(
                GetArg(args, 1),
                GetArg(args, 2),
                GetNamedArg(args, "--output"),
                HasFlag(args, "--overwrite")),
            ["--download-all-attachments"] = args => attachments.DownloadAllAttachmentsAsync(
                GetArg(args, 1),
                GetNamedArg(args, "--output-dir"),
                HasFlag(args, "--overwrite")),
            ["--delete-attachment"] = args => attachments.DeleteAttachmentAsync(GetArg(args, 1), GetArg(args, 2)),

            ["--get-checklists"] = args => checklists.GetChecklistsAsync(GetArg(args, 1)),
            ["--create-checklist"] = args => checklists.CreateChecklistAsync(GetArg(args, 1), GetArg(args, 2)),
            ["--delete-checklist"] = args => checklists.DeleteChecklistAsync(GetArg(args, 1)),
            ["--add-checklist-item"] = args => checklists.AddChecklistItemAsync(GetArg(args, 1), GetArg(args, 2)),
            ["--update-checklist-item"] = args => checklists.UpdateChecklistItemAsync(
                GetArg(args, 1),
                GetArg(args, 2),
                GetArg(args, 3)),
            ["--delete-checklist-item"] = args => checklists.DeleteChecklistItemAsync(GetArg(args, 1), GetArg(args, 2)),

            ["--update-comment"] = args => cards.UpdateCommentAsync(GetArg(args, 1), GetArg(args, 2), GetArg(args, 3)),
            ["--delete-comment"] = args => cards.DeleteCommentAsync(GetArg(args, 1), GetArg(args, 2)),

            ["--add-card-label"] = args => cards.AddCardLabelAsync(GetArg(args, 1), GetArg(args, 2)),
            ["--remove-card-label"] = args => cards.RemoveCardLabelAsync(GetArg(args, 1), GetArg(args, 2)),

            ["--whoami"] = _ => members.WhoAmIAsync(),
            ["--get-member"] = args => members.GetMemberAsync(GetArg(args, 1)),
            ["--get-my-cards"] = args => members.GetMyCardsAsync(GetNamedArg(args, "--filter")),
            ["--get-members"] = args => members.GetBoardMembersAsync(GetArg(args, 1)),
            ["--get-card-members"] = args => members.GetCardMembersAsync(GetArg(args, 1)),
            ["--add-card-member"] = args => members.AddCardMemberAsync(GetArg(args, 1), GetArg(args, 2)),
            ["--remove-card-member"] = args => members.RemoveCardMemberAsync(GetArg(args, 1), GetArg(args, 2)),

            ["--search"] = args => search.SearchAsync(
                GetArg(args, 1),
                GetNamedArg(args, "--board"),
                GetNamedArg(args, "--limit"),
                HasFlag(args, "--cards-only")),
            ["--search-members"] = args => search.SearchMembersAsync(
                GetArg(args, 1),
                GetNamedArg(args, "--limit")),

            ["--get-labels"] = args => labels.GetLabelsAsync(GetArg(args, 1)),
            ["--create-label"] = args => labels.CreateLabelAsync(
                GetArg(args, 1),
                GetArg(args, 2),
                GetNamedArg(args, "--color")),
            ["--update-label"] = args => labels.UpdateLabelAsync(
                GetArg(args, 1),
                GetNamedArg(args, "--name"),
                GetNamedArg(args, "--color")),
            ["--delete-label"] = args => labels.DeleteLabelAsync(GetArg(args, 1))
        };
    }

    /// <summary>Commands this dispatcher accepts. Compared against the catalog by a parity test.</summary>
    internal IReadOnlyCollection<string> KnownCommands => _handlers.Keys;

    public async Task ExecuteAsync(string[] args)
    {
        var command = args[0];

        if (!_handlers.TryGetValue(command, out var handler))
        {
            _output.WriteLine(OutputFormatter.ToJson(
                ApiResponse<object>.Fail(CommandCatalog.DescribeUnknownCommand(command), "UNKNOWN_COMMAND")));
            return;
        }

        await handler(args);
    }

    private static bool? GetClosedArg(string[] args) => GetNamedArg(args, "--closed")?.ToLower() switch
    {
        "true" => true,
        "false" => false,
        _ => null
    };

    /// <summary>True when a valueless switch such as --overwrite is present.</summary>
    private static bool HasFlag(string[] args, string name) =>
        Array.IndexOf(args, name) >= 0;

    private static string GetArg(string[] args, int index) =>
        args.Length > index ? args[index] : string.Empty;

    private static string? GetNamedArg(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name) return args[i + 1];
        }

        return null;
    }
}
