using System.Text.Json.Serialization;

namespace TrelloCli;

/// <summary>
/// The single source of truth for the command surface: every command, its arguments,
/// its options, and the limits that apply to it. Help text, the machine-readable
/// manifest and the dispatcher all read from here so they cannot drift apart.
/// </summary>
public static class CommandCatalog
{
    public const string ToolName = "trello-cli";
    public const string Tagline = "CLI tool for Trello with AI-friendly JSON output";

    public static class Groups
    {
        public const string Discovery = "Discovery";
        public const string Authentication = "Authentication";
        public const string Board = "Board";
        public const string List = "List";
        public const string Card = "Card";
        public const string Comment = "Comment";
        public const string Member = "Member";
        public const string Search = "Search";
        public const string Workspace = "Workspace";
        public const string CustomField = "Custom field";
        public const string Label = "Label";
        public const string Attachment = "Attachment";
        public const string Checklist = "Checklist";
    }

    /// <summary>Group order used by the help output and the manifest.</summary>
    public static IReadOnlyList<string> GroupOrder { get; } =
    [
        Groups.Discovery,
        Groups.Authentication,
        Groups.Board,
        Groups.List,
        Groups.Card,
        Groups.Comment,
        Groups.Member,
        Groups.Search,
        Groups.Label,
        Groups.Attachment,
        Groups.Checklist,
        Groups.CustomField,
        Groups.Workspace
    ];

    public static IReadOnlyList<CommandDefinition> Commands { get; } = BuildCommands();

    /// <summary>Commands routed through <see cref="CommandDispatcher"/> rather than handled by the CLI itself.</summary>
    public static IReadOnlyList<CommandDefinition> DispatchedCommands { get; } =
        Commands.Where(command => command.Dispatched).ToArray();

    private static readonly Dictionary<string, CommandDefinition> Index = BuildIndex();

    /// <summary>Resolves a command by name or alias. The leading dashes are optional.</summary>
    public static CommandDefinition? Find(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var normalized = name.Trim();
        if (Index.TryGetValue(normalized, out var direct)) return direct;
        return Index.TryGetValue($"--{normalized.TrimStart('-')}", out var dashed) ? dashed : null;
    }

    /// <summary>True when the exact name (or alias) is a command the CLI accepts.</summary>
    public static bool Contains(string name) => Index.ContainsKey(name);

    /// <summary>Names close enough to an unrecognized command to be worth suggesting.</summary>
    public static IReadOnlyList<string> Suggest(string name, int max = 3)
    {
        if (string.IsNullOrWhiteSpace(name)) return [];

        var candidate = name.Trim().TrimStart('-').ToLowerInvariant();
        return Commands
            .Select(command => (command.Name, Distance: EditDistance(candidate, command.Name.TrimStart('-'))))
            .Where(entry => entry.Distance <= Math.Max(2, candidate.Length / 3))
            .OrderBy(entry => entry.Distance)
            .ThenBy(entry => entry.Name, StringComparer.Ordinal)
            .Take(max)
            .Select(entry => entry.Name)
            .ToArray();
    }

    /// <summary>The message used whenever a command name is not recognized, wherever it was typed.</summary>
    public static string DescribeUnknownCommand(string command)
    {
        var suggestions = Suggest(command);
        var hint = suggestions.Count switch
        {
            0 => string.Empty,
            1 => $" Did you mean {suggestions[0]}?",
            _ => $" Did you mean {string.Join(", ", suggestions.Take(suggestions.Count - 1))} or {suggestions[^1]}?"
        };

        return $"Unknown command: {command}.{hint} Run {ToolName} --help for the full command list, " +
            $"or {ToolName} --commands for the same catalog as JSON.";
    }

    public static CommandManifest BuildManifest(string version) => new(
        ToolName,
        version,
        Tagline,
        Output,
        Authentication,
        Discovery,
        Commands,
        ErrorCodes,
        Restrictions);

    public static OutputContract Output { get; } = new(
        "{\"ok\":true,\"data\":...}",
        "{\"ok\":false,\"error\":\"...\",\"code\":\"...\"}",
        "Every command writes exactly one single-line JSON object to stdout.",
        "The process always exits with code 0, including on failure. Read the \"ok\" field to decide success.",
        "Diagnostic warnings are written to stderr; stdout carries the JSON result only.");

    public static AuthenticationContract Authentication { get; } = new(
        $"{ToolName} --set-auth <api-key>",
        "The Trello token is read from a hidden terminal prompt and is never accepted as an argument.",
        ["TRELLO_API_KEY", "TRELLO_TOKEN"],
        "Windows Credential Manager, macOS Keychain, or Linux Secret Service (requires secret-tool from libsecret-tools plus a running, unlocked Secret Service on D-Bus).",
        "Nonblank environment credentials override persisted credentials and are unaffected by --clear-auth.",
        "https://trello.com/app-key");

    public static IReadOnlyList<string> Discovery { get; } =
    [
        $"{ToolName} --help                    # every command, grouped, with limits",
        $"{ToolName} --help --create-card      # usage, options and limits for one command",
        $"{ToolName} --commands                # the same catalog as JSON, for programmatic use",
        $"{ToolName} --commands --create-card  # JSON for one command"
    ];

    public static IReadOnlyList<ErrorCodeDefinition> ErrorCodes { get; } =
    [
        new("AUTH_ERROR", "Credentials are missing or incomplete."),
        new("UNAUTHORIZED", "Trello rejected the API key or token."),
        new("UNKNOWN_COMMAND", "The command is not recognized; run --help or --commands for the list."),
        new("MISSING_PARAM", "A required argument was not provided."),
        new("INVALID_PARAM", "An argument was provided in an unsupported form."),
        new("NO_PARAMS", "An update command was called without any field to change."),
        new("NOT_FOUND", "The board, list, card, label, checklist, item, attachment, member or comment does not exist or is not visible to the token."),
        new("FILE_NOT_FOUND", "The local file passed to --upload-attachment does not exist."),
        new("CREATE_FAILED", "Trello accepted the request but returned no usable resource."),
        new("UPDATE_FAILED", "Trello accepted the request but returned no usable resource."),
        new("UPLOAD_FAILED", "The attachment upload returned no usable resource."),
        new("ATTACH_FAILED", "The URL attachment returned no usable resource."),
        new("LINK_ATTACHMENT", "The attachment is a link rather than a file Trello hosts; the message carries the URL to fetch."),
        new("FILE_EXISTS", "The download destination already exists and --overwrite was not passed."),
        new("DIRECTORY_NOT_FOUND", "The directory for the download destination does not exist and could not be created."),
        new("PATH_TRAVERSAL", "The attachment file name resolved outside the requested directory and was refused."),
        new("NAME_COLLISION", "No free file name was available for an attachment in the output directory."),
        new("DOWNLOAD_INCOMPLETE", "The download ended before the whole file arrived; no partial file was kept."),
        new("DOWNLOAD_FAILED", "The attachment could not be downloaded."),
        new("TOO_MANY_REDIRECTS", "The download redirected more times than allowed."),
        new("REDIRECT_BLOCKED", "The download redirected to an unsupported scheme or downgraded to plain HTTP."),
        new("REDIRECT_INVALID", "The download returned a redirect with no location to follow."),
        new("HTTP_ERROR", "The Trello request failed; the message carries the HTTP status code when one was received."),
        new("TOKEN_ARGUMENT_REJECTED", "A token was passed on the command line instead of the hidden prompt."),
        new("TOKEN_REQUIRED", "The token prompt received an empty value."),
        new("TOKEN_INPUT_CANCELLED", "Token entry was cancelled."),
        new("INTERACTIVE_REQUIRED", "--set-auth needs a terminal; set TRELLO_API_KEY and TRELLO_TOKEN instead."),
        new("CREDENTIAL_STORE_UNAVAILABLE", "The operating system credential store could not be reached."),
        new("CREDENTIAL_STORE_ERROR", "The operating system credential store failed to complete the operation."),
        new("SAVE_ERROR", "Authentication could not be persisted."),
        new("CLEAR_ERROR", "Persisted authentication could not be fully removed."),
        new("ERROR", "Unclassified failure.")
    ];

    public static IReadOnlyList<RestrictionGroup> Restrictions { get; } =
    [
        new("Output and exit status",
        [
            "Each run prints one JSON object and exits 0 even when the operation failed; branch on \"ok\", never on the exit code.",
            "Failure messages are deliberately sanitized: no request URLs, request bodies or Trello response payloads are echoed."
        ]),
        new("Authentication",
        [
            "Every command except --help, --version, --commands, --set-auth and --clear-auth requires valid credentials.",
            "--set-auth needs an interactive terminal for the token prompt; in CI, containers and SSH sessions set TRELLO_API_KEY and TRELLO_TOKEN instead.",
            "--clear-auth removes persisted credentials only. It neither revokes the Trello token nor unsets environment variables.",
            "The CLI acts as the owner of the token: it can only see and change what that Trello account may see and change."
        ]),
        new("Not supported (no command exists)",
        [
            "Boards cannot be deleted. Closing one with --close-board is the reversible equivalent, and deletion is deliberately left out because it destroys every list and card on the board.",
            "Lists cannot be deleted. Archiving one with --archive-list is the closest equivalent and is reversible.",
            "Only Trello-hosted attachments can be downloaded. A link attachment is not fetched for you; --download-attachment returns its URL so you can retrieve it yourself.",
            "--move-card cannot move a card to a different board; --copy-card can copy one across.",
            "Workspaces can be read but not created or changed. Members can be read and assigned to cards, but not invited, removed from a board, or given a different role.",
            "Only comments written by the token’s own account can be edited or deleted.",
            "Custom field values can be read and set, but the fields themselves cannot be created or deleted.",
            "Stickers, power-ups, webhooks, board backgrounds and notifications are out of scope.",
            "Except for --bulk-move-lists there is no batching: one command performs one operation."
        ]),
        new("What the read commands return",
        [
            "--get-boards, --get-lists and --get-all-cards return open items unless you pass --filter closed or --filter all.",
            "An archived card is still readable with --get-card and can be restored with --unarchive-card.",
            "--get-labels returns at most 1000 labels for a board.",
            "Results are returned exactly as Trello sends them, unpaged; large boards produce large JSON documents."
        ]),
        new("Argument handling",
        [
            "Options are matched by exact name and the first occurrence wins; unknown or misspelled options are ignored silently instead of failing.",
            "The value after an option is taken literally, so quote any value containing spaces and pass a value that starts with a dash carefully.",
            "--labels and --members take comma-separated IDs and replace the entire set on the card; pass an empty string to clear it.",
            "--update-card ignores an empty --name, while --desc \"\" and --due \"\" clear those fields. Calling it without any field returns NO_PARAMS.",
            "Due dates are forwarded to Trello unvalidated; use ISO-8601 (YYYY-MM-DD or a full timestamp).",
            "Label colors are not validated locally; an unsupported color is rejected by Trello as HTTP_ERROR.",
            "--update-checklist-item takes a card ID, while --add-checklist-item and --delete-checklist-item take a checklist ID."
        ]),
        new("Irreversible operations",
        [
            "--delete-card, --delete-label, --delete-checklist, --delete-checklist-item and --delete-attachment are permanent and have no undo.",
            "--archive-card is the recoverable alternative to --delete-card.",
            "--delete-label removes the label from every card on the board.",
            "--bulk-move-lists applies moves in order and stops at the first failure; moves already applied are not rolled back."
        ]),
        new("Network and rate limits",
        [
            "Trello enforces rate limits per key and per token (documented as 300 requests per 10 seconds per API key and 100 per 10 seconds per token). A throttled request surfaces as HTTP_ERROR with status 429.",
            "No request is retried and no backoff is applied; the caller decides whether to retry.",
            "HTTP redirects are not followed.",
            "Attachment size limits are enforced by Trello and depend on the workspace plan; the file size is not checked before upload, so an oversized file fails as HTTP_ERROR."
        ])
    ];

    private static Dictionary<string, CommandDefinition> BuildIndex()
    {
        var index = new Dictionary<string, CommandDefinition>(StringComparer.Ordinal);
        foreach (var command in Commands)
        {
            index[command.Name] = command;
            foreach (var alias in command.Aliases)
                index[alias] = command;
        }

        return index;
    }

    private static int EditDistance(string left, string right)
    {
        var previous = new int[right.Length + 1];
        var current = new int[right.Length + 1];
        for (var j = 0; j <= right.Length; j++) previous[j] = j;

        for (var i = 1; i <= left.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= right.Length; j++)
            {
                var substitution = previous[j - 1] + (left[i - 1] == right[j - 1] ? 0 : 1);
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), substitution);
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length];
    }

    private static IReadOnlyList<CommandDefinition> BuildCommands() =>
    [
        // Discovery
        new("--help", Groups.Discovery, "Show this help, or the details of a single command.",
            Aliases: ["-h"],
            Arguments: [new("command", "Command to describe, with or without the leading dashes.", Required: false)],
            Examples: [$"{ToolName} --help", $"{ToolName} --help --create-card"],
            Notes: ["Running the tool with no arguments prints the same help."],
            RequiresAuth: false,
            Dispatched: false),

        new("--commands", Groups.Discovery, "Print the command catalog, limits and error codes as JSON.",
            Arguments: [new("command", "Restrict the output to a single command.", Required: false)],
            Examples: [$"{ToolName} --commands", $"{ToolName} --commands --create-card"],
            Notes: ["Intended for agents and scripts: the same information as --help in the standard {\"ok\":true,\"data\":...} envelope."],
            RequiresAuth: false,
            Dispatched: false),

        new("--version", Groups.Discovery, "Print the tool version.",
            Aliases: ["-v"],
            Examples: [$"{ToolName} --version"],
            Notes: ["Plain text, not JSON."],
            RequiresAuth: false,
            Dispatched: false),

        // Authentication
        new("--set-auth", Groups.Authentication, "Save the API key and read the token from a hidden prompt.",
            Arguments: [new("api-key", "Trello API key from https://trello.com/app-key.")],
            Examples: [$"{ToolName} --set-auth 1a2b3c"],
            Notes:
            [
                "The token is never accepted as an argument; passing one returns TOKEN_ARGUMENT_REJECTED.",
                "Requires an interactive terminal; otherwise returns INTERACTIVE_REQUIRED.",
                "The token goes to the operating system credential store, the API key to the user configuration file."
            ],
            RequiresAuth: false,
            Dispatched: false),

        new("--check-auth", Groups.Authentication, "Verify the credentials against Trello and return the member.",
            Examples: [$"{ToolName} --check-auth"],
            Notes: ["Returns UNAUTHORIZED when Trello rejects the key or token."],
            Dispatched: false),

        new("--clear-auth", Groups.Authentication, "Remove persisted credentials.",
            Examples: [$"{ToolName} --clear-auth"],
            Notes:
            [
                "Does not revoke the Trello token and does not unset TRELLO_API_KEY or TRELLO_TOKEN.",
                "Reports environmentOverridesRemainActive in its success data, and partial cleanup as an error."
            ],
            RequiresAuth: false,
            Dispatched: false),

        // Board
        new("--get-boards", Groups.Board, "List the boards of the authenticated member.",
            Options: [new("--filter", "open|closed|all", "Which boards to return; defaults to open.")],
            Examples: [$"{ToolName} --get-boards", $"{ToolName} --get-boards --filter all"],
            Notes:
            [
                "Returns open boards unless --filter says otherwise.",
                "Usually the first call: other commands need the board ID."
            ]),

        new("--get-board", Groups.Board, "Get one board.",
            Arguments: [new("board-id", "Board ID or the short link from the board URL.")],
            Examples: [$"{ToolName} --get-board 5f2c3d4e5f6a7b8c9d0e1f2a"],
            Notes: ["Boards are read-only in this CLI; there is no create, rename or delete."]),

        new("--create-board", Groups.Board, "Create a board.",
            Arguments: [new("name", "Board name.")],
            Options:
            [
                new("--desc", "text", "Board description."),
                new("--org", "workspace-id", "Workspace to create it in; see --get-organizations."),
                new("--default-lists", "true|false", "Create Trello's To Do / Doing / Done lists; defaults to true."),
                new("--permission-level", "private|org|public", "Who can see the board.")
            ],
            Examples:
            [
                $"{ToolName} --create-board \"Q3 planning\"",
                $"{ToolName} --create-board \"Q3 planning\" --org 5f2c...1f2a --default-lists false"
            ],
            Notes: ["Trello creates To Do, Doing and Done unless --default-lists false says otherwise."]),

        new("--update-board", Groups.Board, "Rename or describe a board.",
            Arguments: [new("board-id", "Board to change.")],
            Options:
            [
                new("--name", "text", "New board name."),
                new("--desc", "text", "New description; pass \"\" to clear it."),
                new("--permission-level", "private|org|public", "Who can see the board.")
            ],
            Examples: [$"{ToolName} --update-board 5f2c...1f2a --name \"Q4 planning\""],
            Notes: ["At least one option is required; without one the command returns NO_PARAMS."]),

        new("--close-board", Groups.Board, "Close a board.",
            Arguments: [new("board-id", "Board to close.")],
            Examples: [$"{ToolName} --close-board 5f2c...1f2a"],
            Notes:
            [
                "Reversible with --reopen-board, and the recoverable alternative to deleting a board.",
                "A closed board is hidden from --get-boards unless you pass --filter closed or all."
            ]),

        new("--reopen-board", Groups.Board, "Reopen a closed board.",
            Arguments: [new("board-id", "Board to reopen.")],
            Examples: [$"{ToolName} --reopen-board 5f2c...1f2a"]),

        // List
        new("--get-lists", Groups.List, "Get the lists of a board.",
            Arguments: [new("board-id", "Board the lists belong to.")],
            Options: [new("--filter", "open|closed|all", "Which lists to return; defaults to open.")],
            Examples:
            [
                $"{ToolName} --get-lists 5f2c3d4e5f6a7b8c9d0e1f2a",
                $"{ToolName} --get-lists 5f2c3d4e5f6a7b8c9d0e1f2a --filter closed"
            ],
            Notes: ["Returns open lists unless --filter says otherwise."]),

        new("--create-list", Groups.List, "Create a list on a board.",
            Arguments: [new("board-id", "Board to create the list on."), new("name", "List name.")],
            Examples: [$"{ToolName} --create-list 5f2c3d4e5f6a7b8c9d0e1f2a \"In Review\""],
            Notes: ["The position cannot be set at creation; use --move-list afterwards."]),

        new("--move-list", Groups.List, "Reposition a list on its board.",
            Arguments: [new("list-id", "List to move."), new("pos", "top, bottom, or a positive number.")],
            Examples: [$"{ToolName} --move-list 5f2c3d4e5f6a7b8c9d0e1f2a top"],
            Notes: ["A list cannot be moved to another board."]),

        new("--bulk-move-lists", Groups.List, "Reposition several lists in one call.",
            Arguments: [new("list-id:pos", "One or more <list-id>:<pos> pairs.", Repeatable: true)],
            Examples: [$"{ToolName} --bulk-move-lists 5f2c...1f2a:top 5f2c...1f2b:bottom"],
            Notes:
            [
                "Pairs are applied one at a time in the order given.",
                "Processing stops at the first failure and moves already applied are not rolled back.",
                "A pair that is not <list-id>:<pos> aborts the run with INVALID_PARAM at that point, after the earlier pairs have already moved."
            ]),

        new("--update-list", Groups.List, "Rename or reposition a list.",
            Arguments: [new("list-id", "List to change.")],
            Options: [new("--name", "text", "New list name."), new("--pos", "top|bottom|number", "New position.")],
            Examples: [$"{ToolName} --update-list 5f2c...1f2a --name \"In Review\""],
            Notes: ["At least one option is required; without one the command returns NO_PARAMS."]),

        new("--archive-list", Groups.List, "Archive a list.",
            Arguments: [new("list-id", "List to archive.")],
            Examples: [$"{ToolName} --archive-list 5f2c...1f2a"],
            Notes:
            [
                "Reversible with --unarchive-list. The list's cards are archived with it and come back with it.",
                "Archived lists are not returned by --get-lists unless you pass --filter closed or all."
            ]),

        new("--unarchive-list", Groups.List, "Restore an archived list.",
            Arguments: [new("list-id", "List to restore.")],
            Examples: [$"{ToolName} --unarchive-list 5f2c...1f2a"]),

        new("--archive-all-cards", Groups.List, "Archive every card in a list.",
            Arguments: [new("list-id", "List to empty.")],
            Examples: [$"{ToolName} --archive-all-cards 5f2c...1f2a"],
            Notes:
            [
                "Archives the cards but keeps the list. Each card can be restored with --unarchive-card.",
                "One request regardless of how many cards the list holds."
            ]),

        new("--move-all-cards", Groups.List, "Move every card from one list to another.",
            Arguments: [new("source-list-id", "List to empty."), new("target-list-id", "List to fill.")],
            Examples: [$"{ToolName} --move-all-cards 5f2c...1f2a 5f2c...1f2b"],
            Notes:
            [
                "Reads the target list first to find its board, so two requests are made.",
                "Both lists must be on the same board."
            ]),

        // Card
        new("--get-cards", Groups.Card, "Get the cards of a list.",
            Arguments: [new("list-id", "List to read.")],
            Examples: [$"{ToolName} --get-cards 5f2c3d4e5f6a7b8c9d0e1f2a"],
            Notes: ["Archived cards are omitted."]),

        new("--get-all-cards", Groups.Card, "Get every card on a board.",
            Arguments: [new("board-id", "Board to read.")],
            Options: [new("--filter", "open|closed|all", "Which cards to return; defaults to open.")],
            Examples:
            [
                $"{ToolName} --get-all-cards 5f2c3d4e5f6a7b8c9d0e1f2a",
                $"{ToolName} --get-all-cards 5f2c3d4e5f6a7b8c9d0e1f2a --filter closed"
            ],
            Notes:
            [
                "Cheaper than one --get-cards per list, and the way to find a card by name: filter the result client-side.",
                "Archived cards are omitted and the result is not paged."
            ]),

        new("--get-card", Groups.Card, "Get one card.",
            Arguments: [new("card-id", "Card ID or short link.")],
            Examples: [$"{ToolName} --get-card 5f2c3d4e5f6a7b8c9d0e1f2a"],
            Notes: ["Works for archived cards as well."]),

        new("--create-card", Groups.Card, "Create a card in a list.",
            Arguments: [new("list-id", "List that will hold the card."), new("name", "Card title.")],
            Options:
            [
                new("--desc", "text", "Card description."),
                new("--due", "date", "Due date, ISO-8601 (YYYY-MM-DD or a full timestamp)."),
                new("--labels", "ids", "Comma-separated label IDs from --get-labels."),
                new("--members", "ids", "Comma-separated member IDs that already belong to the board.")
            ],
            Examples:
            [
                $"{ToolName} --create-card 5f2c...1f2a \"Fix login bug\"",
                $"{ToolName} --create-card 5f2c...1f2a \"Fix login bug\" --desc \"Details\" --due 2026-01-15",
                $"{ToolName} --create-card 5f2c...1f2a \"Fix login bug\" --labels lbl1,lbl2 --members mbr1"
            ],
            Notes:
            [
                "Setting labels and members here costs one request; --update-card afterwards costs two.",
                "The card position inside the list cannot be chosen."
            ]),

        new("--update-card", Groups.Card, "Change fields of a card.",
            Arguments: [new("card-id", "Card to update.")],
            Options:
            [
                new("--name", "text", "New title; an empty value is ignored."),
                new("--desc", "text", "New description; \"\" clears it."),
                new("--due", "date", "New due date; \"\" clears it."),
                new("--labels", "ids", "Comma-separated label IDs; replaces the whole set, \"\" clears it."),
                new("--members", "ids", "Comma-separated member IDs; replaces the whole set, \"\" clears it."),
                new("--closed", "true|false", "Archive or restore the card.")
            ],
            Examples:
            [
                $"{ToolName} --update-card 5f2c...1f2a --name \"Fix login bug\" --due 2026-02-01",
                $"{ToolName} --update-card 5f2c...1f2a --desc \"\""
            ],
            Notes:
            [
                "At least one option is required; without one the command returns NO_PARAMS.",
                "--labels and --members overwrite rather than append: read the current values first if you mean to add."
            ]),

        new("--move-card", Groups.Card, "Move a card to another list.",
            Arguments: [new("card-id", "Card to move."), new("target-list-id", "Destination list.")],
            Examples: [$"{ToolName} --move-card 5f2c...1f2a 5f2c...1f2b"],
            Notes: ["Only lists on the same board; moving a card between boards is not supported."]),

        new("--archive-card", Groups.Card, "Archive a card.",
            Arguments: [new("card-id", "Card to archive.")],
            Examples: [$"{ToolName} --archive-card 5f2c...1f2a"],
            Notes: ["Equivalent to --update-card <card-id> --closed true, and reversible with --unarchive-card."]),

        new("--unarchive-card", Groups.Card, "Restore an archived card.",
            Arguments: [new("card-id", "Card to restore.")],
            Examples: [$"{ToolName} --unarchive-card 5f2c...1f2a"],
            Notes: ["Equivalent to --update-card <card-id> --closed false."]),

        new("--copy-card", Groups.Card, "Copy a card into a list.",
            Arguments: [new("card-id", "Card to copy."), new("target-list-id", "List to copy it into.")],
            Options:
            [
                new("--name", "text", "Name for the copy; defaults to the original's name."),
                new("--position", "top|bottom|number", "Where in the list the copy lands."),
                new("--keep", "all|attachments,checklists,comments,due,labels,members,stickers",
                    "What to carry over; defaults to all.")
            ],
            Examples:
            [
                $"{ToolName} --copy-card 5f2c...1f2a 5f2c...1f2b",
                $"{ToolName} --copy-card 5f2c...1f2a 5f2c...1f2b --name \"Retry\" --keep checklists,labels"
            ],
            Notes:
            [
                "Copies everything by default. Trello's own default is the name alone, which is rarely what a copy is for.",
                "The destination list may be on another board, unlike --move-card."
            ]),

        new("--set-card-position", Groups.Card, "Move a card within its list.",
            Arguments: [new("card-id", "Card to move."), new("position", "top, bottom, or a number.")],
            Examples: [$"{ToolName} --set-card-position 5f2c...1f2a top"],
            Notes: ["Changes the order inside the list; use --move-card to change list."]),

        new("--set-due-complete", Groups.Card, "Mark a card's due date done or not done.",
            Arguments: [new("card-id", "Card to change."), new("state", "true or false.")],
            Examples: [$"{ToolName} --set-due-complete 5f2c...1f2a true"],
            Notes: ["Ticks the due date itself; it neither archives the card nor moves it."]),

        new("--set-start-date", Groups.Card, "Set or clear a card's start date.",
            Arguments: [new("card-id", "Card to change."), new("date", "ISO-8601 date, or \"\" to clear it.")],
            Examples:
            [
                $"{ToolName} --set-start-date 5f2c...1f2a 2026-03-01",
                $"{ToolName} --set-start-date 5f2c...1f2a \"\""
            ],
            Notes: ["Forwarded to Trello unvalidated, like --due."]),

        new("--set-card-cover", Groups.Card, "Set a card's cover.",
            Arguments: [new("card-id", "Card to change.")],
            Options:
            [
                new("--color", "color", "Cover color, for example red or blue."),
                new("--attachment", "attachment-id", "Use an existing image attachment as the cover."),
                new("--size", "normal|full", "How much of the card the cover takes."),
                new("--brightness", "light|dark", "Text contrast over the cover.")
            ],
            Examples:
            [
                $"{ToolName} --set-card-cover 5f2c...1f2a --color blue --size full",
                $"{ToolName} --set-card-cover 5f2c...1f2a --attachment 5f2c...1f2b"
            ],
            Notes:
            [
                "At least one option is required; without one the command returns NO_PARAMS.",
                "An attachment cover must already be on the card and must be an image."
            ]),

        new("--clear-card-cover", Groups.Card, "Remove a card's cover.",
            Arguments: [new("card-id", "Card to change.")],
            Examples: [$"{ToolName} --clear-card-cover 5f2c...1f2a"],
            Notes: ["Clears the cover only; an attachment used as one stays on the card."]),

        new("--get-card-activity", Groups.Card, "Read a card's activity feed.",
            Arguments: [new("card-id", "Card to read.")],
            Options:
            [
                new("--limit", "n", "Maximum entries to return."),
                new("--filter", "action-types", "Comma-separated Trello action types; defaults to all.")
            ],
            Examples:
            [
                $"{ToolName} --get-card-activity 5f2c...1f2a --limit 20",
                $"{ToolName} --get-card-activity 5f2c...1f2a --filter updateCard,commentCard"
            ],
            Notes:
            [
                "Answers who moved or changed a card and when.",
                "Each entry's data field differs by action type and is passed through as Trello sends it."
            ]),

        new("--add-card-label", Groups.Card, "Add one label to a card.",
            Arguments: [new("card-id", "Card to label."), new("label-id", "Label to add.")],
            Examples: [$"{ToolName} --add-card-label 5f2c...1f2a 5f2c...1f2b"],
            Notes:
            [
                "Adds one label and leaves the rest in place, unlike --labels on --update-card which replaces the whole set.",
                "Returns the card's resulting label ids."
            ]),

        new("--remove-card-label", Groups.Card, "Remove one label from a card.",
            Arguments: [new("card-id", "Card to change."), new("label-id", "Label to remove.")],
            Examples: [$"{ToolName} --remove-card-label 5f2c...1f2a 5f2c...1f2b"],
            Notes: ["Removes the label from this card only; the label itself stays on the board."]),

        new("--delete-card", Groups.Card, "Delete a card.",
            Arguments: [new("card-id", "Card to delete.")],
            Examples: [$"{ToolName} --delete-card 5f2c...1f2a"],
            Notes: ["Cannot be undone. Prefer --archive-card unless deletion is explicitly requested."],
            Destructive: true),

        // Comment
        new("--get-comments", Groups.Comment, "Get the comments on a card.",
            Arguments: [new("card-id", "Card to read.")],
            Examples: [$"{ToolName} --get-comments 5f2c...1f2a"]),

        new("--add-comment", Groups.Comment, "Add a comment to a card.",
            Arguments: [new("card-id", "Card to comment on."), new("text", "Comment body.")],
            Examples: [$"{ToolName} --add-comment 5f2c...1f2a \"Deployed to staging\""],
            Notes: ["Comments cannot be edited or deleted through this CLI."]),

        new("--update-comment", Groups.Comment, "Rewrite an existing comment.",
            Arguments:
            [
                new("card-id", "Card carrying the comment."),
                new("comment-id", "Comment to rewrite; the id from --get-comments."),
                new("text", "Replacement text.")
            ],
            Examples: [$"{ToolName} --update-comment 5f2c...1f2a 5f2c...1f2c \"Corrected note\""],
            Notes: ["Only comments the token's own account wrote can be edited."]),

        new("--delete-comment", Groups.Comment, "Delete a comment from a card.",
            Arguments:
            [
                new("card-id", "Card carrying the comment."),
                new("comment-id", "Comment to delete; the id from --get-comments.")
            ],
            Examples: [$"{ToolName} --delete-comment 5f2c...1f2a 5f2c...1f2c"],
            Notes: ["Cannot be undone.", "Only comments the token's own account wrote can be deleted."],
            Destructive: true),

        // Member
        new("--whoami", Groups.Member, "Show the account the current token belongs to.",
            Examples: [$"{ToolName} --whoami"],
            Notes: ["Everything the CLI does happens as this member, so this is what the token can see and change."]),

        new("--get-member", Groups.Member, "Look up a member by id or username.",
            Arguments: [new("member", "Member id or username.")],
            Examples: [$"{ToolName} --get-member alexdoe"]),

        new("--get-members", Groups.Member, "List the members of a board.",
            Arguments: [new("board-id", "Board to read.")],
            Examples: [$"{ToolName} --get-members 5f2c...1f2a"],
            Notes: ["These are the ids --members and --add-card-member accept."]),

        new("--get-card-members", Groups.Member, "List the members assigned to a card.",
            Arguments: [new("card-id", "Card to read.")],
            Examples: [$"{ToolName} --get-card-members 5f2c...1f2a"]),

        new("--get-my-cards", Groups.Member, "List the cards assigned to the current member.",
            Options: [new("--filter", "open|closed|all", "Which cards to return; defaults to open.")],
            Examples: [$"{ToolName} --get-my-cards", $"{ToolName} --get-my-cards --filter all"],
            Notes: ["Spans every board the member can see, so it is not limited to one board."]),

        new("--add-card-member", Groups.Member, "Assign a member to a card.",
            Arguments: [new("card-id", "Card to assign to."), new("member-id", "Member to assign.")],
            Examples: [$"{ToolName} --add-card-member 5f2c...1f2a 5f2c...1f2d"],
            Notes: ["Adds one member and leaves the rest in place, unlike --members on --update-card which replaces the whole set."]),

        new("--remove-card-member", Groups.Member, "Unassign a member from a card.",
            Arguments: [new("card-id", "Card to change."), new("member-id", "Member to remove.")],
            Examples: [$"{ToolName} --remove-card-member 5f2c...1f2a 5f2c...1f2d"]),

        // Search
        new("--search", Groups.Search, "Search Trello for cards and boards.",
            Arguments: [new("query", "What to search for.")],
            Options:
            [
                new("--board", "board-id", "Restrict the search to one board."),
                new("--limit", "n", "Maximum results per model type."),
                new("--cards-only", "", "Return cards only, omitting boards and members.")
            ],
            Examples:
            [
                $"{ToolName} --search \"login page\"",
                $"{ToolName} --search \"login page\" --board 5f2c...1f2a --limit 10 --cards-only"
            ],
            Notes:
            [
                "The way to find an id when you only know what the card says; without it you would have to read whole boards with --get-all-cards.",
                "Matches partial words, and searches everything the token can see unless --board narrows it.",
                "Trello's own search operators work inside the query, for example \"label:red\" or \"due:week\".",
                "Returns data.cards, data.boards and data.members; each is empty when nothing matched."
            ]),

        new("--search-members", Groups.Search, "Search for members by name or username.",
            Arguments: [new("query", "Name or username fragment.")],
            Options: [new("--limit", "n", "Maximum results, up to 20.")],
            Examples: [$"{ToolName} --search-members alex"]),

        // Label
        new("--get-labels", Groups.Label, "List the labels defined on a board.",
            Arguments: [new("board-id", "Board to read.")],
            Examples: [$"{ToolName} --get-labels 5f2c...1f2a"],
            Notes: ["Returns at most 1000 labels.", "Run this first to get the IDs for --labels."]),

        new("--create-label", Groups.Label, "Create a label on a board.",
            Arguments: [new("board-id", "Board to create the label on."), new("name", "Label name.")],
            Options: [new("--color", "color", "green, yellow, orange, red, purple, blue, sky, lime, pink or black, each also with a _light or _dark suffix.")],
            Examples: [$"{ToolName} --create-label 5f2c...1f2a \"bug\" --color red"],
            Notes: ["Without --color the label is created with no color.", "Colors are validated by Trello, not locally: an unsupported value returns HTTP_ERROR."]),

        new("--update-label", Groups.Label, "Rename or recolor a label.",
            Arguments: [new("label-id", "Label to update.")],
            Options: [new("--name", "text", "New label name."), new("--color", "color", "New color.")],
            Examples: [$"{ToolName} --update-label 5f2c...1f2a --name \"defect\" --color orange"],
            Notes: ["At least one option is required; without one the command returns NO_PARAMS."]),

        new("--delete-label", Groups.Label, "Delete a label.",
            Arguments: [new("label-id", "Label to delete.")],
            Examples: [$"{ToolName} --delete-label 5f2c...1f2a"],
            Notes: ["Cannot be undone and removes the label from every card on the board."],
            Destructive: true),

        // Attachment
        new("--list-attachments", Groups.Attachment, "List the attachments on a card.",
            Arguments: [new("card-id", "Card to read.")],
            Examples: [$"{ToolName} --list-attachments 5f2c...1f2a"],
            Notes: ["The returned url is how an attachment is linked onto another card with --attach-url."]),

        new("--upload-attachment", Groups.Attachment, "Upload a local file to a card.",
            Arguments: [new("card-id", "Card to attach to."), new("file-path", "Path to a local file.")],
            Options: [new("--name", "text", "Attachment name; defaults to the file name.")],
            Examples: [$"{ToolName} --upload-attachment 5f2c...1f2a ./spec.pdf --name \"Spec\""],
            Notes:
            [
                "A missing file returns FILE_NOT_FOUND before any request is sent.",
                "The size limit belongs to Trello and depends on the workspace plan; it is not checked locally.",
                "Retrieve the file again with --download-attachment."
            ]),

        new("--attach-url", Groups.Attachment, "Attach a URL to a card.",
            Arguments: [new("card-id", "Card to attach to."), new("url", "URL to attach.")],
            Options: [new("--name", "text", "Attachment name.")],
            Examples: [$"{ToolName} --attach-url 5f2c...1f2a https://example.com/doc.pdf --name \"Doc\""],
            Notes: ["Also the way to share an existing attachment with another card: take the url from --list-attachments."]),

        new("--get-attachment", Groups.Attachment, "Read one attachment's metadata.",
            Arguments: [new("card-id", "Card holding the attachment."), new("attachment-id", "Attachment to read.")],
            Examples: [$"{ToolName} --get-attachment 5f2c...1f2a 5f2c...1f2b"],
            Notes: ["isUpload tells you whether Trello hosts the file and --download-attachment can fetch it."]),

        new("--download-attachment", Groups.Attachment, "Download a Trello-hosted attachment to a local file.",
            Arguments: [new("card-id", "Card holding the attachment."), new("attachment-id", "Attachment to download.")],
            Options:
            [
                new("--output", "path", "Destination file, or a directory to place it in. Defaults to the current directory."),
                new("--overwrite", "", "Replace the destination file if it already exists.")
            ],
            Examples:
            [
                $"{ToolName} --download-attachment 5f2c...1f2a 5f2c...1f2b",
                $"{ToolName} --download-attachment 5f2c...1f2a 5f2c...1f2b --output ./spec.pdf --overwrite"
            ],
            Notes:
            [
                "Only attachments Trello hosts, where isUpload is true. A link attachment returns LINK_ATTACHMENT together with its URL, which you can fetch yourself.",
                "The file name Trello reports is sanitized before use, so the download always lands inside the directory you named.",
                "Without --overwrite an existing destination returns FILE_EXISTS; nothing is written.",
                "The file is written in full or not at all: a truncated transfer returns DOWNLOAD_INCOMPLETE and leaves no partial file."
            ]),

        new("--download-all-attachments", Groups.Attachment, "Download every Trello-hosted attachment on a card.",
            Arguments: [new("card-id", "Card to download from.")],
            Options:
            [
                new("--output-dir", "path", "Directory to write into. Required; created when missing."),
                new("--overwrite", "", "Replace destination files that already exist.")
            ],
            Examples: [$"{ToolName} --download-all-attachments 5f2c...1f2a --output-dir ./attachments"],
            Notes:
            [
                "Reports partial success: data.downloaded, data.skipped and data.failed each list the attachments in that state, and one failure does not stop the rest.",
                "Link attachments appear under skipped with their URL rather than being fetched.",
                "Attachments sharing a file name are numbered file.pdf, file-2.pdf and so on, so none overwrites another.",
                "Downloads run one at a time to stay inside Trello's rate limit; a card with many attachments takes a while."
            ]),

        new("--delete-attachment", Groups.Attachment, "Delete an attachment from a card.",
            Arguments: [new("card-id", "Card holding the attachment."), new("attachment-id", "Attachment to delete.")],
            Examples: [$"{ToolName} --delete-attachment 5f2c...1f2a 5f2c...1f2b"],
            Notes: ["Cannot be undone."],
            Destructive: true),

        // Checklist
        new("--get-checklists", Groups.Checklist, "Get the checklists of a card, including their items.",
            Arguments: [new("card-id", "Card to read.")],
            Examples: [$"{ToolName} --get-checklists 5f2c...1f2a"],
            Notes: ["The only way to obtain checklist IDs and item IDs."]),

        new("--create-checklist", Groups.Checklist, "Create a checklist on a card.",
            Arguments: [new("card-id", "Card to create the checklist on."), new("name", "Checklist name.")],
            Examples: [$"{ToolName} --create-checklist 5f2c...1f2a \"Release steps\""],
            Notes: ["Checklists cannot be renamed; delete and recreate instead."]),

        new("--delete-checklist", Groups.Checklist, "Delete a checklist and its items.",
            Arguments: [new("checklist-id", "Checklist to delete.")],
            Examples: [$"{ToolName} --delete-checklist 5f2c...1f2a"],
            Notes: ["Cannot be undone."],
            Destructive: true),

        new("--add-checklist-item", Groups.Checklist, "Add an item to a checklist.",
            Arguments: [new("checklist-id", "Checklist to add to."), new("name", "Item text.")],
            Examples: [$"{ToolName} --add-checklist-item 5f2c...1f2a \"Tag the release\""],
            Notes: ["Takes the checklist ID, unlike --update-checklist-item, which takes the card ID.", "Items cannot be renamed or reordered."]),

        new("--update-checklist-item", Groups.Checklist, "Mark a checklist item complete or incomplete.",
            Arguments:
            [
                new("card-id", "Card holding the checklist, not the checklist ID."),
                new("item-id", "Checklist item to update."),
                new("state", "complete or incomplete.")
            ],
            Examples: [$"{ToolName} --update-checklist-item 5f2c...1f2a 5f2c...1f2b complete"],
            Notes:
            [
                "This is the one checklist command that takes a card ID; Trello requires it for this endpoint.",
                "Any other state returns INVALID_PARAM.",
                "Only the state can change; the item text cannot."
            ]),

        new("--update-checklist", Groups.Checklist, "Rename or reposition a checklist.",
            Arguments: [new("checklist-id", "Checklist to change.")],
            Options: [new("--name", "text", "New checklist name."), new("--pos", "top|bottom|number", "New position.")],
            Examples: [$"{ToolName} --update-checklist 5f2c...1f2a --name \"Release steps\""],
            Notes: ["At least one option is required; without one the command returns NO_PARAMS."]),

        new("--rename-checklist-item", Groups.Checklist, "Rename an item in a checklist.",
            Arguments: [new("card-id", "Card holding the checklist."), new("item-id", "Item to rename.")],
            Options: [new("--name", "text", "New item text.")],
            Examples: [$"{ToolName} --rename-checklist-item 5f2c...1f2a 5f2c...1f2b --name \"Ship it\""],
            Notes: ["Takes a card ID, like --update-checklist-item and unlike the add and delete item commands."]),

        new("--move-checklist-item", Groups.Checklist, "Reorder an item within its checklist.",
            Arguments: [new("card-id", "Card holding the checklist."), new("item-id", "Item to move.")],
            Options: [new("--pos", "top|bottom|number", "New position.")],
            Examples: [$"{ToolName} --move-checklist-item 5f2c...1f2a 5f2c...1f2b --pos top"],
            Notes: ["Takes a card ID, like --update-checklist-item."]),

        new("--delete-checklist-item", Groups.Checklist, "Delete an item from a checklist.",
            Arguments: [new("checklist-id", "Checklist holding the item."), new("item-id", "Item to delete.")],
            Examples: [$"{ToolName} --delete-checklist-item 5f2c...1f2a 5f2c...1f2b"],
            Notes: ["Cannot be undone."],
            Destructive: true),

        // Custom field
        new("--get-custom-fields", Groups.CustomField, "List the custom fields defined on a board.",
            Arguments: [new("board-id", "Board to read.")],
            Examples: [$"{ToolName} --get-custom-fields 5f2c...1f2a"],
            Notes:
            [
                "Each field carries its type: text, number, date, checkbox or list.",
                "A list field also carries its options; those ids are what --set-custom-field --option takes."
            ]),

        new("--get-card-custom-fields", Groups.CustomField, "Read the custom field values set on a card.",
            Arguments: [new("card-id", "Card to read.")],
            Examples: [$"{ToolName} --get-card-custom-fields 5f2c...1f2a"],
            Notes: ["Only fields with a value on this card appear; the shape of value depends on the field type."]),

        new("--set-custom-field", Groups.CustomField, "Set a custom field on a card.",
            Arguments: [new("card-id", "Card to change."), new("field-id", "Custom field to set.")],
            Options:
            [
                new("--value", "text", "Value for a text, number, date or checkbox field."),
                new("--option", "option-id", "Chosen option for a list field.")
            ],
            Examples:
            [
                $"{ToolName} --set-custom-field 5f2c...1f2a 5f2c...1f2b --value \"In review\"",
                $"{ToolName} --set-custom-field 5f2c...1f2a 5f2c...1f2b --option 5f2c...1f2e"
            ],
            Notes:
            [
                "Reads the field first to learn its type, so one --value works for text, number, date and checkbox fields; that costs an extra request.",
                "A list field needs --option, not --value; --get-custom-fields lists the option ids.",
                "Use true or false as the value of a checkbox field, and an ISO-8601 timestamp for a date field."
            ]),

        new("--clear-custom-field", Groups.CustomField, "Clear a custom field on a card.",
            Arguments: [new("card-id", "Card to change."), new("field-id", "Custom field to clear.")],
            Examples: [$"{ToolName} --clear-custom-field 5f2c...1f2a 5f2c...1f2b"],
            Notes: ["Clears the value on this card; the field stays defined on the board."]),

        // Workspace
        new("--get-organizations", Groups.Workspace, "List the workspaces the member belongs to.",
            Examples: [$"{ToolName} --get-organizations"],
            Notes: ["Trello's API calls workspaces organizations, which is why these commands are named that way."]),

        new("--get-organization", Groups.Workspace, "Get one workspace.",
            Arguments: [new("workspace-id", "Workspace id or name.")],
            Examples: [$"{ToolName} --get-organization 5f2c...1f2a"]),

        new("--get-organization-boards", Groups.Workspace, "List the boards in a workspace.",
            Arguments: [new("workspace-id", "Workspace id or name.")],
            Examples: [$"{ToolName} --get-organization-boards 5f2c...1f2a"],
            Notes: ["Narrower than --get-boards, which spans every board the member can see."]),

        new("--get-organization-members", Groups.Workspace, "List the members of a workspace.",
            Arguments: [new("workspace-id", "Workspace id or name.")],
            Examples: [$"{ToolName} --get-organization-members 5f2c...1f2a"])
    ];
}

public sealed record CommandArgument(string Name, string Description, bool Required = true, bool Repeatable = false);

public sealed record CommandOption(string Name, string Value, string Description)
{
    /// <summary>How the option reads in usage output; a switch taking no value shows its name alone.</summary>
    public string Display => string.IsNullOrEmpty(Value) ? Name : $"{Name} <{Value}>";
}

public sealed record CommandDefinition(
    string Name,
    string Group,
    string Summary,
    IReadOnlyList<string>? Aliases = null,
    IReadOnlyList<CommandArgument>? Arguments = null,
    IReadOnlyList<CommandOption>? Options = null,
    IReadOnlyList<string>? Examples = null,
    IReadOnlyList<string>? Notes = null,
    bool RequiresAuth = true,
    bool Destructive = false,
    [property: JsonIgnore] bool Dispatched = true)
{
    public IReadOnlyList<string> Aliases { get; } = Aliases ?? [];
    public IReadOnlyList<CommandArgument> Arguments { get; } = Arguments ?? [];
    public IReadOnlyList<CommandOption> Options { get; } = Options ?? [];
    public IReadOnlyList<string> Examples { get; } = Examples ?? [];
    public IReadOnlyList<string> Notes { get; } = Notes ?? [];

    /// <summary>The command line shape, e.g. <c>trello-cli --create-card &lt;list-id&gt; &lt;name&gt; [--desc &lt;text&gt;]</c>.</summary>
    public string Usage
    {
        get
        {
            var parts = new List<string> { CommandCatalog.ToolName, Name };
            foreach (var argument in Arguments)
            {
                var token = $"<{argument.Name}>";
                if (argument.Repeatable) token += "...";
                parts.Add(argument.Required ? token : $"[{token}]");
            }

            parts.AddRange(Options.Select(option => $"[{option.Display}]"));
            return string.Join(' ', parts);
        }
    }
}

public sealed record ErrorCodeDefinition(string Code, string Meaning);

public sealed record RestrictionGroup(string Title, IReadOnlyList<string> Items);

public sealed record OutputContract(
    string Success,
    string Error,
    string Stdout,
    string ExitCode,
    string Stderr);

public sealed record AuthenticationContract(
    string Setup,
    string TokenEntry,
    IReadOnlyList<string> EnvironmentVariables,
    string Storage,
    string Precedence,
    string CredentialsUrl);

public sealed record CommandManifest(
    string Tool,
    string Version,
    string Description,
    OutputContract Output,
    AuthenticationContract Authentication,
    IReadOnlyList<string> Discovery,
    IReadOnlyList<CommandDefinition> Commands,
    IReadOnlyList<ErrorCodeDefinition> ErrorCodes,
    IReadOnlyList<RestrictionGroup> Restrictions);
