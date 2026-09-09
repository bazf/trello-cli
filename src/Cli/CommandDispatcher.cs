using TrelloCli.Commands;
using TrelloCli.Models;
using TrelloCli.Services;
using TrelloCli.Utils;

namespace TrelloCli;

public sealed class CommandDispatcher : ICommandDispatcher
{
    private readonly BoardCommands _boardCommands;
    private readonly ListCommands _listCommands;
    private readonly CardCommands _cardCommands;
    private readonly AttachmentCommands _attachmentCommands;
    private readonly ChecklistCommands _checklistCommands;
    private readonly LabelCommands _labelCommands;
    private readonly TextWriter _output;

    public CommandDispatcher(TrelloApiService api, TextWriter output)
    {
        _boardCommands = new BoardCommands(api);
        _listCommands = new ListCommands(api);
        _cardCommands = new CardCommands(api);
        _attachmentCommands = new AttachmentCommands(api);
        _checklistCommands = new ChecklistCommands(api);
        _labelCommands = new LabelCommands(api);
        _output = output;
    }

    public async Task ExecuteAsync(string[] args)
    {
        var command = args[0];

        switch (command)
        {
            case "--get-boards":
                await _boardCommands.GetBoardsAsync();
                break;

            case "--get-board":
                await _boardCommands.GetBoardAsync(GetArg(args, 1));
                break;

            case "--get-lists":
                await _listCommands.GetListsAsync(GetArg(args, 1));
                break;

            case "--create-list":
                await _listCommands.CreateListAsync(GetArg(args, 1), GetArg(args, 2));
                break;

            case "--move-list":
                await _listCommands.MoveListAsync(GetArg(args, 1), GetArg(args, 2));
                break;

            case "--bulk-move-lists":
                await _listCommands.BulkMoveListsAsync(args[1..]);
                break;

            case "--get-cards":
                await _cardCommands.GetCardsAsync(GetArg(args, 1));
                break;

            case "--get-all-cards":
                await _cardCommands.GetAllCardsAsync(GetArg(args, 1));
                break;

            case "--get-card":
                await _cardCommands.GetCardAsync(GetArg(args, 1));
                break;

            case "--create-card":
                await _cardCommands.CreateCardAsync(
                    GetArg(args, 1),
                    GetArg(args, 2),
                    GetNamedArg(args, "--desc"),
                    GetNamedArg(args, "--due"),
                    GetNamedArg(args, "--labels"),
                    GetNamedArg(args, "--members"));
                break;

            case "--update-card":
                var closedArg = GetNamedArg(args, "--closed");
                bool? closedValue = closedArg?.ToLower() switch
                {
                    "true" => true,
                    "false" => false,
                    _ => null
                };
                await _cardCommands.UpdateCardAsync(
                    GetArg(args, 1),
                    GetNamedArg(args, "--name"),
                    GetNamedArg(args, "--desc"),
                    GetNamedArg(args, "--due"),
                    GetNamedArg(args, "--labels"),
                    GetNamedArg(args, "--members"),
                    closedValue);
                break;

            case "--archive-card":
                await _cardCommands.ArchiveCardAsync(GetArg(args, 1));
                break;

            case "--unarchive-card":
                await _cardCommands.UnarchiveCardAsync(GetArg(args, 1));
                break;

            case "--move-card":
                await _cardCommands.MoveCardAsync(GetArg(args, 1), GetArg(args, 2));
                break;

            case "--delete-card":
                await _cardCommands.DeleteCardAsync(GetArg(args, 1));
                break;

            case "--get-comments":
                await _cardCommands.GetCommentsAsync(GetArg(args, 1));
                break;

            case "--add-comment":
                await _cardCommands.AddCommentAsync(GetArg(args, 1), GetArg(args, 2));
                break;

            case "--list-attachments":
                await _attachmentCommands.GetAttachmentsAsync(GetArg(args, 1));
                break;

            case "--upload-attachment":
                await _attachmentCommands.UploadAttachmentAsync(
                    GetArg(args, 1),
                    GetArg(args, 2),
                    GetNamedArg(args, "--name"));
                break;

            case "--attach-url":
                await _attachmentCommands.AttachUrlAsync(
                    GetArg(args, 1),
                    GetArg(args, 2),
                    GetNamedArg(args, "--name"));
                break;

            case "--delete-attachment":
                await _attachmentCommands.DeleteAttachmentAsync(GetArg(args, 1), GetArg(args, 2));
                break;

            case "--get-checklists":
                await _checklistCommands.GetChecklistsAsync(GetArg(args, 1));
                break;

            case "--create-checklist":
                await _checklistCommands.CreateChecklistAsync(GetArg(args, 1), GetArg(args, 2));
                break;

            case "--delete-checklist":
                await _checklistCommands.DeleteChecklistAsync(GetArg(args, 1));
                break;

            case "--add-checklist-item":
                await _checklistCommands.AddChecklistItemAsync(GetArg(args, 1), GetArg(args, 2));
                break;

            case "--update-checklist-item":
                await _checklistCommands.UpdateChecklistItemAsync(
                    GetArg(args, 1),
                    GetArg(args, 2),
                    GetArg(args, 3));
                break;

            case "--delete-checklist-item":
                await _checklistCommands.DeleteChecklistItemAsync(GetArg(args, 1), GetArg(args, 2));
                break;

            case "--get-labels":
                await _labelCommands.GetLabelsAsync(GetArg(args, 1));
                break;

            case "--create-label":
                await _labelCommands.CreateLabelAsync(
                    GetArg(args, 1),
                    GetArg(args, 2),
                    GetNamedArg(args, "--color"));
                break;

            case "--update-label":
                await _labelCommands.UpdateLabelAsync(
                    GetArg(args, 1),
                    GetNamedArg(args, "--name"),
                    GetNamedArg(args, "--color"));
                break;

            case "--delete-label":
                await _labelCommands.DeleteLabelAsync(GetArg(args, 1));
                break;

            default:
                _output.WriteLine(OutputFormatter.ToJson(
                    ApiResponse<object>.Fail($"Unknown command: {command}", "UNKNOWN_COMMAND")));
                break;
        }
    }

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
