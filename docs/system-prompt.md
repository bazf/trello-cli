# Trello CLI Tool

You can manage Trello via `trello-cli`. All outputs are JSON: `{"ok":true,"data":...}` or `{"ok":false,"error":"...","code":"..."}`.

## Discovering commands

The CLI documents itself; run these instead of guessing:

```bash
trello-cli --help                 # every command, grouped, plus the global limits
trello-cli --help <command>       # usage, options and limits for one command
trello-cli --commands             # the same catalog as JSON (commands, error codes, restrictions)
```

## Commands

| Command | Usage |
|---------|-------|
| `--help` | Show this help, or the details of a single command. `--help [<command>]` |
| `--commands` | Print the command catalog, limits and error codes as JSON. `--commands [<command>]` |
| `--version` | Print the tool version. `--version` |
| `--set-auth` | Save the API key and read the token from a hidden prompt. `--set-auth <api-key>` |
| `--check-auth` | Verify the credentials against Trello and return the member. `--check-auth` |
| `--clear-auth` | Remove persisted credentials. `--clear-auth` |
| `--get-boards` | List the open boards of the authenticated member. `--get-boards` |
| `--get-board` | Get one board. `--get-board <board-id>` |
| `--get-lists` | Get the open lists of a board. `--get-lists <board-id>` |
| `--create-list` | Create a list on a board. `--create-list <board-id> <name>` |
| `--move-list` | Reposition a list on its board. `--move-list <list-id> <pos>` |
| `--bulk-move-lists` | Reposition several lists in one call. `--bulk-move-lists <list-id:pos>...` |
| `--get-cards` | Get the cards of a list. `--get-cards <list-id>` |
| `--get-all-cards` | Get every open card on a board. `--get-all-cards <board-id>` |
| `--get-card` | Get one card. `--get-card <card-id>` |
| `--create-card` | Create a card in a list. `--create-card <list-id> <name> [--desc <text>] [--due <date>] [--labels <ids>] [--members <ids>]` |
| `--update-card` | Change fields of a card. `--update-card <card-id> [--name <text>] [--desc <text>] [--due <date>] [--labels <ids>] [--members <ids>] [--closed <true|false>]` |
| `--move-card` | Move a card to another list. `--move-card <card-id> <target-list-id>` |
| `--archive-card` | Archive a card. `--archive-card <card-id>` |
| `--unarchive-card` | Restore an archived card. `--unarchive-card <card-id>` |
| `--delete-card` | Delete a card. `--delete-card <card-id>` |
| `--get-comments` | Get the comments on a card. `--get-comments <card-id>` |
| `--add-comment` | Add a comment to a card. `--add-comment <card-id> <text>` |
| `--update-comment` | Rewrite an existing comment. `--update-comment <card-id> <comment-id> <text>` |
| `--delete-comment` | Delete a comment. `--delete-comment <card-id> <comment-id>` |
| `--add-card-label` | Add one label to a card, keeping the others. `--add-card-label <card-id> <label-id>` |
| `--remove-card-label` | Remove one label from a card. `--remove-card-label <card-id> <label-id>` |
| `--whoami` | Show the account the current token belongs to. `--whoami` |
| `--get-member` | Look up a member by id or username. `--get-member <member>` |
| `--get-members` | List the members of a board. `--get-members <board-id>` |
| `--get-card-members` | List the members assigned to a card. `--get-card-members <card-id>` |
| `--get-my-cards` | List cards assigned to you. `--get-my-cards [--filter <open\|closed\|all>]` |
| `--add-card-member` | Assign a member to a card. `--add-card-member <card-id> <member-id>` |
| `--remove-card-member` | Unassign a member from a card. `--remove-card-member <card-id> <member-id>` |
| `--search` | Search for cards and boards. `--search <query> [--board <board-id>] [--limit <n>] [--cards-only]` |
| `--search-members` | Search for members. `--search-members <query> [--limit <n>]` |
| `--get-labels` | List the labels defined on a board. `--get-labels <board-id>` |
| `--create-label` | Create a label on a board. `--create-label <board-id> <name> [--color <color>]` |
| `--update-label` | Rename or recolor a label. `--update-label <label-id> [--name <text>] [--color <color>]` |
| `--delete-label` | Delete a label. `--delete-label <label-id>` |
| `--list-attachments` | List the attachments on a card. `--list-attachments <card-id>` |
| `--upload-attachment` | Upload a local file to a card. `--upload-attachment <card-id> <file-path> [--name <text>]` |
| `--attach-url` | Attach a URL to a card. `--attach-url <card-id> <url> [--name <text>]` |
| `--get-attachment` | Read one attachment's metadata. `--get-attachment <card-id> <attachment-id>` |
| `--download-attachment` | Download a Trello-hosted attachment. `--download-attachment <card-id> <attachment-id> [--output <path>] [--overwrite]` |
| `--download-all-attachments` | Download every Trello-hosted attachment on a card. `--download-all-attachments <card-id> [--output-dir <path>] [--overwrite]` |
| `--delete-attachment` | Delete an attachment from a card. `--delete-attachment <card-id> <attachment-id>` |
| `--get-checklists` | Get the checklists of a card, including their items. `--get-checklists <card-id>` |
| `--create-checklist` | Create a checklist on a card. `--create-checklist <card-id> <name>` |
| `--delete-checklist` | Delete a checklist and its items. `--delete-checklist <checklist-id>` |
| `--add-checklist-item` | Add an item to a checklist. `--add-checklist-item <checklist-id> <name>` |
| `--update-checklist-item` | Mark a checklist item complete or incomplete. `--update-checklist-item <card-id> <item-id> <state>` |
| `--delete-checklist-item` | Delete an item from a checklist. `--delete-checklist-item <checklist-id> <item-id>` |

## Restrictions

- Boards are read-only; lists can only be created and repositioned. No search command:
  use `--get-all-cards` and filter client-side.
- `--get-boards`, `--get-lists` and `--get-all-cards` skip archived items; `--get-card`
  still reads an archived card.
- `--move-card` stays on the same board, and cards cannot be reordered inside a list.
- Attachments cannot be downloaded; comments cannot be edited or deleted; members,
  custom fields, power-ups and webhooks are not supported.
- `--labels` and `--members` replace the whole set on a card; `""` clears it.
- `--update-checklist-item` takes a card ID; the other item commands take a checklist ID.
- Every `--delete-*` is permanent; `--archive-card` is the reversible option.
- The exit code is always 0 and unknown options are ignored silently, so branch on `ok`.
- Trello rate limits surface as `HTTP_ERROR` (429) and nothing is retried automatically.

## Authentication safety

Never place a Trello token on the command line. Interactive setup is
`trello-cli --set-auth <api-key>` and reads the token through a hidden terminal
prompt. In headless environments, set both `TRELLO_API_KEY` and `TRELLO_TOKEN`.

The token is stored in Windows Credential Manager, macOS Keychain, or Linux
Secret Service. Linux needs `libsecret-tools` plus a running, unlocked Secret
Service on D-Bus. Legacy plaintext tokens are removed only after an exact
secure-store read-back; migration failure preserves the legacy file and emits a
sanitized warning. `--clear-auth` removes persisted data but does not revoke a
Trello token or unset environment variables.

## Workflow

1. `--get-boards` → find board ID
2. `--get-lists <board-id>` → find list IDs (To Do, In Progress, Done, etc.)
3. Use card commands with the IDs

## Examples

```bash
trello-cli --get-boards
trello-cli --get-all-cards 6abc123
trello-cli --create-card 6list789 "New task" --desc "Details" --due "2025-01-20"
trello-cli --move-card 6card456 6donelist123
```
