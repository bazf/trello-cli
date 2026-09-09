# Trello CLI - AI Instruction

You have access to a CLI tool called `trello-cli` that allows you to interact with Trello boards, lists, and cards. Use this tool to help users manage their Trello tasks.

## Output Format

All responses are compact JSON:
- Success: `{"ok":true,"data":...}`
- Error: `{"ok":false,"error":"...","code":"..."}`

## Discovering Commands

The CLI is self-documenting; never guess a command name or a limit:

```bash
trello-cli --help                 # every command, grouped, plus the global limits
trello-cli --help --create-card   # usage, options and limits for one command
trello-cli --commands             # the same catalog as JSON: commands, limits, error codes
trello-cli --commands --create-card
```

`--commands` answers in the usual envelope, so one call gives you the entire surface:

```json
{"ok":true,"data":{"tool":"trello-cli","version":"2.0.0","commands":[...],"errorCodes":[...],"restrictions":[...]}}
```

An unknown command returns `{"ok":false,"code":"UNKNOWN_COMMAND"}` with the closest
match and a pointer back to `--help` and `--commands`, so recovery needs no guessing.

## Available Commands

### Authentication

```bash
# Save the API key; enter the token at the hidden terminal prompt
trello-cli --set-auth <api-key>

# Check if authenticated
trello-cli --check-auth

# Remove persisted API-key configuration and the OS-stored token
trello-cli --clear-auth
```

Never pass the token as a positional argument. For headless use, set both
`TRELLO_API_KEY` and `TRELLO_TOKEN`; nonblank environment credentials override
persisted values. Environment variables remain active after `--clear-auth`.

Tokens are stored in Windows Credential Manager (target `trello-cli`, username
`trello-token`), macOS Keychain (service `trello-cli`, account
`trello-token`), or Linux Secret Service (`service=trello-cli`,
`account=trello-token`). Linux requires `secret-tool` from `libsecret-tools`
and a running, unlocked Secret Service on the D-Bus session.

Version 2.0.0 migrates legacy plaintext configuration only after writing and
exactly reading back the secure token. On any migration failure the original
file is preserved, the token remains usable for that run, and only a sanitized
warning is emitted. `--clear-auth` attempts both persisted locations, reports
partial cleanup, and neither revokes the Trello token nor unsets environment
variables.

### Board Operations

```bash
# List all boards
trello-cli --get-boards

# Get specific board
trello-cli --get-board <board-id>
```

### List Operations

```bash
# Get all lists in a board (archived lists are not returned)
trello-cli --get-lists <board-id>

# Create a new list
trello-cli --create-list <board-id> "<list-name>"

# Reposition a list: top, bottom, or a number
trello-cli --move-list <list-id> top

# Reposition several lists in one call (<list-id>:<pos> pairs)
trello-cli --bulk-move-lists <list-id>:top <list-id>:bottom
```

Lists cannot be renamed, archived or deleted through this CLI. `--bulk-move-lists`
applies the pairs in order and stops at the first failure; the moves already applied
stay applied.

### Card Operations

```bash
# Get cards in a specific list
trello-cli --get-cards <list-id>

# Get ALL cards in a board
trello-cli --get-all-cards <board-id>

# Get specific card details
trello-cli --get-card <card-id>

# Create a new card
trello-cli --create-card <list-id> "<card-name>"
trello-cli --create-card <list-id> "<card-name>" --desc "<description>"
trello-cli --create-card <list-id> "<card-name>" --desc "<description>" --due "2025-01-15"
trello-cli --create-card <list-id> "<card-name>" --labels "<id1>,<id2>" --members "<id1>,<id2>"

# Update a card
trello-cli --update-card <card-id> --name "<new-name>"
trello-cli --update-card <card-id> --desc "<new-description>"
trello-cli --update-card <card-id> --due "2025-01-15"
trello-cli --update-card <card-id> --name "<name>" --desc "<desc>" --due "<date>"

# Move card to another list (same board only)
trello-cli --move-card <card-id> <target-list-id>

# Archive / restore a card (reversible; prefer this over deleting)
trello-cli --archive-card <card-id>
trello-cli --unarchive-card <card-id>

# Delete a card permanently (cannot be undone)
trello-cli --delete-card <card-id>

# Get comments on a card
trello-cli --get-comments <card-id>

# Add a comment to a card
trello-cli --add-comment <card-id> "<comment-text>"
```

### Label Operations

```bash
# List all labels on a board
trello-cli --get-labels <board-id>

# Create a new label
trello-cli --create-label <board-id> "<label-name>" --color <color>
# Valid colors: green, yellow, orange, red, purple, blue, sky, lime, pink, black
# Suffix _light or _dark also valid (e.g. green_dark, red_light). Empty color = no color.

# Update a label
trello-cli --update-label <label-id> --name "<new-name>" --color <new-color>

# Delete a label
trello-cli --delete-label <label-id>
```

### Attachment Operations

```bash
# List attachments on a card
trello-cli --list-attachments <card-id>

# Upload a local file
trello-cli --upload-attachment <card-id> <file-path> --name "<attachment-name>"

# Attach a URL (also how an attachment is linked onto another card)
trello-cli --attach-url <card-id> <url> --name "<attachment-name>"

# Delete an attachment (permanent)
trello-cli --delete-attachment <card-id> <attachment-id>
```

Downloading attachment content is not supported: Trello's download API requires
browser authentication. Take the `url` from `--list-attachments` and pass it to
`--attach-url` when a file has to reach another card.

### Checklist Operations

```bash
# Get checklists and their items (the only source of checklist and item IDs)
trello-cli --get-checklists <card-id>

# Create and delete checklists
trello-cli --create-checklist <card-id> "<checklist-name>"
trello-cli --delete-checklist <checklist-id>

# Items: note that updating takes the CARD id, adding and deleting take the CHECKLIST id
trello-cli --add-checklist-item <checklist-id> "<item-name>"
trello-cli --update-checklist-item <card-id> <item-id> complete
trello-cli --update-checklist-item <card-id> <item-id> incomplete
trello-cli --delete-checklist-item <checklist-id> <item-id>
```

## Response Examples

### Get Boards
```json
{"ok":true,"data":[{"id":"abc123","name":"My Project","url":"https://trello.com/b/abc123"}]}
```

### Get Lists
```json
{"ok":true,"data":[{"id":"list1","name":"To Do","boardId":"abc123"},{"id":"list2","name":"In Progress","boardId":"abc123"},{"id":"list3","name":"Done","boardId":"abc123"}]}
```

### Get Cards
```json
{"ok":true,"data":[{"id":"card1","name":"Fix bug","desc":"Fix login issue","listId":"list1","due":"2025-01-15"}]}
```

### Create Card
```json
{"ok":true,"data":{"id":"newcard123","name":"New Task","listId":"list1"}}
```

### Get Comments
```json
{"ok":true,"data":[{"id":"action123","date":"2025-01-15T10:30:00.000Z","data":{"text":"This is a comment"},"memberCreator":{"id":"member123","fullName":"John Doe","username":"johndoe"}}]}
```

### Add Comment
```json
{"ok":true,"data":{"id":"action456","date":"2025-01-15T11:00:00.000Z","data":{"text":"New comment"},"memberCreator":{"id":"member123","fullName":"John Doe","username":"johndoe"}}}
```

### Error Response
```json
{"ok":false,"error":"Card not found","code":"NOT_FOUND"}
```

## Error Codes

The same table is returned by `trello-cli --commands` under `data.errorCodes`.

| Code | Meaning |
|------|---------|
| `AUTH_ERROR` | Credentials are missing or incomplete. |
| `UNAUTHORIZED` | Trello rejected the API key or token. |
| `UNKNOWN_COMMAND` | The command is not recognized; run --help or --commands for the list. |
| `MISSING_PARAM` | A required argument was not provided. |
| `INVALID_PARAM` | An argument was provided in an unsupported form. |
| `NO_PARAMS` | An update command was called without any field to change. |
| `NOT_FOUND` | The board, list, card, label, checklist, item or attachment does not exist or is not visible to the token. |
| `FILE_NOT_FOUND` | The local file passed to --upload-attachment does not exist. |
| `CREATE_FAILED` | Trello accepted the request but returned no usable resource. |
| `UPDATE_FAILED` | Trello accepted the request but returned no usable resource. |
| `UPLOAD_FAILED` | The attachment upload returned no usable resource. |
| `ATTACH_FAILED` | The URL attachment returned no usable resource. |
| `HTTP_ERROR` | The Trello request failed; the message carries the HTTP status code when one was received. |
| `TOKEN_ARGUMENT_REJECTED` | A token was passed on the command line instead of the hidden prompt. |
| `TOKEN_REQUIRED` | The token prompt received an empty value. |
| `TOKEN_INPUT_CANCELLED` | Token entry was cancelled. |
| `INTERACTIVE_REQUIRED` | --set-auth needs a terminal; set TRELLO_API_KEY and TRELLO_TOKEN instead. |
| `CREDENTIAL_STORE_UNAVAILABLE` | The operating system credential store could not be reached. |
| `CREDENTIAL_STORE_ERROR` | The operating system credential store failed to complete the operation. |
| `SAVE_ERROR` | Authentication could not be persisted. |
| `CLEAR_ERROR` | Persisted authentication could not be fully removed. |
| `ERROR` | Unclassified failure. |

## Restrictions

Printed by `trello-cli --help` and returned by `trello-cli --commands` under
`data.restrictions`.

**Output and exit status**

- Each run prints one JSON object and exits 0 even when the operation failed; branch on "ok", never on the exit code.
- Failure messages are deliberately sanitized: no request URLs, request bodies or Trello response payloads are echoed.

**Authentication**

- Every command except `--help`, `--version`, `--commands`, `--set-auth` and `--clear-auth` requires valid credentials.
- `--set-auth` needs an interactive terminal for the token prompt; in CI, containers and SSH sessions set `TRELLO_API_KEY` and `TRELLO_TOKEN` instead.
- `--clear-auth` removes persisted credentials only. It neither revokes the Trello token nor unsets environment variables.
- The CLI acts as the owner of the token: it can only see and change what that Trello account may see and change.

**Not supported (no command exists)**

- Boards are read-only: they cannot be created, renamed, closed or deleted.
- Lists can be created and repositioned only; renaming, archiving and deleting a list are not available.
- Downloading attachment content is not supported, because Trello's download endpoint requires browser authentication. Use `--attach-url` to link an existing attachment onto another card.
- No search command. Fetch with `--get-all-cards` and filter the JSON on the client side.
- Cards cannot be repositioned inside a list, and `--move-card` cannot move a card to a different board.
- No member, workspace or organization management; `--members` only assigns member IDs that already belong to the board.
- Comments can be read and added, but not edited or deleted.
- Custom fields, stickers, power-ups, webhooks, board backgrounds and notifications are out of scope.
- Except for `--bulk-move-lists` there is no batching: one command performs one operation.

**What the read commands return**

- `--get-boards`, `--get-lists` and `--get-all-cards` return open items only; closed boards, archived lists and archived cards are omitted.
- An archived card is still readable with `--get-card` and can be restored with `--unarchive-card`.
- `--get-labels` returns at most 1000 labels for a board.
- Results are returned exactly as Trello sends them, unpaged; large boards produce large JSON documents.

**Argument handling**

- Options are matched by exact name and the first occurrence wins; unknown or misspelled options are ignored silently instead of failing.
- The value after an option is taken literally, so quote any value containing spaces and pass a value that starts with a dash carefully.
- `--labels` and `--members` take comma-separated IDs and replace the entire set on the card; pass an empty string to clear it.
- `--update-card` ignores an empty `--name`, while `--desc` "" and `--due` "" clear those fields. Calling it without any field returns `NO_PARAMS`.
- Due dates are forwarded to Trello unvalidated; use ISO-8601 (YYYY-MM-DD or a full timestamp).
- Label colors are not validated locally; an unsupported color is rejected by Trello as `HTTP_ERROR`.
- `--update-checklist-item` takes a card ID, while `--add-checklist-item` and `--delete-checklist-item` take a checklist ID.

**Irreversible operations**

- `--delete-card`, `--delete-label`, `--delete-checklist`, `--delete-checklist-item` and `--delete-attachment` are permanent and have no undo.
- `--archive-card` is the recoverable alternative to `--delete-card`.
- `--delete-label` removes the label from every card on the board.
- `--bulk-move-lists` applies moves in order and stops at the first failure; moves already applied are not rolled back.

**Network and rate limits**

- Trello enforces rate limits per key and per token (documented as 300 requests per 10 seconds per API key and 100 per 10 seconds per token). A throttled request surfaces as `HTTP_ERROR` with status 429.
- No request is retried and no backoff is applied; the caller decides whether to retry.
- HTTP redirects are not followed.
- Attachment size limits are enforced by Trello and depend on the workspace plan; the file size is not checked before upload, so an oversized file fails as `HTTP_ERROR`.

## Workflow Examples

### Example 1: List all tasks in a board

```bash
# Step 1: Get boards to find the board ID
trello-cli --get-boards

# Step 2: Get all cards in that board
trello-cli --get-all-cards <board-id>
```

### Example 2: Create a task in "To Do" list

```bash
# Step 1: Get lists to find "To Do" list ID
trello-cli --get-lists <board-id>

# Step 2: Create card in that list
trello-cli --create-card <todo-list-id> "Implement feature X" --desc "Details here"
```

### Example 3: Move task from "To Do" to "Done"

```bash
# Step 1: Get lists to find target list ID
trello-cli --get-lists <board-id>

# Step 2: Move the card
trello-cli --move-card <card-id> <done-list-id>
```

### Example 4: Update task with due date

```bash
trello-cli --update-card <card-id> --due "2025-01-20" --desc "Updated description"
```

## Best Practices

1. **Always check `ok` field** in response before processing data
2. **Cache board and list IDs** when possible to reduce API calls
3. **Use `--get-all-cards`** instead of multiple `--get-cards` calls when you need all cards
4. **Quote strings with spaces** in card names and descriptions
5. **Use ISO date format** (YYYY-MM-DD) for due dates

## Common Patterns

### Find a card by name
```bash
# Get all cards and filter by name in the response
trello-cli --get-all-cards <board-id>
# Then search for the card name in the JSON response
```

### Get board overview
```bash
# Get board info
trello-cli --get-board <board-id>

# Get all lists
trello-cli --get-lists <board-id>

# Get all cards
trello-cli --get-all-cards <board-id>
```

### Create complete task
```bash
trello-cli --create-card <list-id> "Task Name" --desc "Task description with details" --due "2025-02-01"
```
