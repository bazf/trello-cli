---
name: trello-cli
description: Trello board, list and card management via CLI. Activate when user mentions "Trello" - examples: "Show my Trello tasks", "Add card to Trello", "Move on Trello", "Trello board", "List Trello", "Trello cards".
---

# Trello CLI

Manage Trello boards, lists, and cards using the `trello-cli` command.

## Important Rules

1. **Only activate when "Trello" is mentioned** - Do not interfere with Notion, Jira, or other tools
2. **Check JSON output after each command** - `ok: true` means success, `ok: false` means error
3. **Follow the workflow**: First find board ID, then list ID, then perform card operations
4. **Never guess a command or a limit** - ask the CLI, it documents itself (below)

## Discovering Commands

```bash
trello-cli --help                 # every command, grouped, plus the global limits
trello-cli --help --create-card   # usage, options and limits for one command
trello-cli --commands             # the same catalog as JSON: commands, limits, error codes
trello-cli --commands --create-card
trello-cli --version              # tool version
```

`--commands` returns `{"ok":true,"data":{"commands":[...],"errorCodes":[...],"restrictions":[...]}}`,
so a single call describes the whole surface. An unrecognized command comes back as
`UNKNOWN_COMMAND` with the closest match, so recovery is mechanical.

## Quick Reference

### Authentication

```bash
trello-cli --set-auth <api-key>  # Enter the token at the hidden prompt
trello-cli --check-auth
trello-cli --clear-auth          # Environment overrides remain active
```

Never pass a token as an argument. For headless use, set both
`TRELLO_API_KEY` and `TRELLO_TOKEN`. Tokens use Windows Credential Manager,
macOS Keychain, or Linux Secret Service. Linux requires `libsecret-tools` and
a running, unlocked Secret Service on D-Bus. Legacy plaintext tokens are
deleted only after secure-store read-back succeeds; migration failure preserves
the legacy file and emits a sanitized warning. Clearing persisted auth does not
revoke the Trello token or unset environment variables.

### Board & List

```bash
trello-cli --get-boards                              # List all open boards
trello-cli --get-board <board-id>                    # Get one board
trello-cli --get-lists <board-id>                    # Get open lists in board
trello-cli --create-list <board-id> "<name>"         # Create list
trello-cli --move-list <list-id> <top|bottom|number> # Reposition a list
trello-cli --bulk-move-lists <id>:top <id>:bottom    # Reposition several lists
```

### Cards

```bash
trello-cli --get-cards <list-id>                     # Cards in one list
trello-cli --get-all-cards <board-id>                # All cards in board
trello-cli --get-card <card-id>                      # One card with full details
trello-cli --create-card <list-id> "<name>"          # Create card
trello-cli --create-card <list-id> "<name>" --desc "<desc>" --due "2025-01-15"
trello-cli --create-card <list-id> "<name>" --labels "<id1>,<id2>" --members "<id1>,<id2>"
trello-cli --update-card <card-id> --name "<name>" --desc "<desc>"
trello-cli --move-card <card-id> <list-id>           # Move card
trello-cli --archive-card <card-id>                  # Archive card
trello-cli --unarchive-card <card-id>                # Unarchive card
trello-cli --delete-card <card-id>                   # Delete card (permanent!)
trello-cli --get-comments <card-id>                  # Get comments
trello-cli --add-comment <card-id> "<text>"          # Add comment
```

### Labels

```bash
trello-cli --get-labels <board-id>                              # List labels on board
trello-cli --create-label <board-id> "<name>" --color <color>   # Create label
trello-cli --update-label <label-id> --name "<name>" --color <color>
trello-cli --delete-label <label-id>                            # Delete label
```

### Attachments

```bash
trello-cli --list-attachments <card-id>              # List attachments
trello-cli --upload-attachment <card-id> <file-path> [--name "<name>"]
trello-cli --attach-url <card-id> <url> [--name "<name>"]
trello-cli --delete-attachment <card-id> <attach-id>
```

**Note:** Downloading attachments is not supported - Trello's download API requires browser authentication. Use `--attach-url` to link attachments between cards.

### Checklists

```bash
trello-cli --get-checklists <card-id>                           # Get checklists on card
trello-cli --create-checklist <card-id> "<name>"                # Create checklist
trello-cli --delete-checklist <checklist-id>                    # Delete checklist
trello-cli --add-checklist-item <checklist-id> "<name>"         # Add item
trello-cli --update-checklist-item <card-id> <item-id> <state>  # complete/incomplete
trello-cli --delete-checklist-item <checklist-id> <item-id>     # Delete item
```

## Typical Workflows

### List All Tasks
```bash
trello-cli --get-boards           # → Get board ID
trello-cli --get-all-cards <id>   # → See all cards
```

### Add New Task
```bash
trello-cli --get-boards           # → Get board ID
trello-cli --get-lists <id>       # → Find "To Do" or "Backlog" list ID
trello-cli --create-card <list-id> "<task-name>"
```

### Move to Done
```bash
trello-cli --get-lists <board-id> # → Find "Done" list ID
trello-cli --move-card <card-id> <done-list-id>
```

## Restrictions

What this CLI cannot do, so you do not plan around it:

- Boards are read-only: no create, rename, close or delete. Lists can only be created and repositioned.
- No search: use `--get-all-cards` and filter the JSON yourself.
- Cards cannot be reordered inside a list, and `--move-card` cannot cross boards.
- Attachments cannot be downloaded; use `--attach-url` with the `url` from `--list-attachments`.
- Comments can be read and added, never edited or deleted. Members, custom fields, power-ups and webhooks are out of scope.
- `--get-boards`, `--get-lists` and `--get-all-cards` return open items only; an archived card is still readable with `--get-card`.
- `--labels` and `--members` replace the whole set on the card; pass `""` to clear.
- `--update-checklist-item` takes a **card** ID; `--add-checklist-item` and `--delete-checklist-item` take a **checklist** ID.
- Every `--delete-*` is permanent. Prefer `--archive-card`.
- The exit code is always 0 and unknown options are ignored silently, so always read `ok`.
- Trello rate limits apply (429 arrives as `HTTP_ERROR`); nothing is retried automatically.

The full list, including per-command limits, is in `trello-cli --help` and
`trello-cli --commands`.

## When Uncertain

If you encounter an error or don't know how to proceed:

```bash
trello-cli --help                 # all commands and limits
trello-cli --help <command>       # detail for one command
trello-cli --commands             # machine-readable catalog
```

For detailed command reference, see [REFERENCE.md](REFERENCE.md).

## Example Scenarios

**User:** "Show my Trello tasks"
→ Run `--get-boards`, then `--get-all-cards <board-id>`

**User:** "Add a new task to Trello"
→ Run `--get-boards`, `--get-lists`, then `--create-card`

**User:** "Move the card to Done on Trello"
→ Run `--get-lists` to find Done ID, then `--move-card`

**User:** "Update the Trello card description"
→ Run `--update-card <card-id> --desc "<new-desc>"`

**User:** "Upload this file to the Trello card"
→ Run `--upload-attachment <card-id> "<file-path>"`

**User:** "Link an attachment to another Trello card"
→ Run `--list-attachments <source-card-id>` to get the URL, then `--attach-url <target-card-id> <url>`

**User:** "Add a checklist to this Trello card"
→ Run `--create-checklist <card-id> "<checklist-name>"`, then `--add-checklist-item` for each item

**User:** "Mark the checklist item as done on Trello"
→ Run `--update-checklist-item <card-id> <item-id> complete`
