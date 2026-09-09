# Trello CLI - Complete Reference

## Discovering Commands

This reference is a copy; the CLI itself is the authority and answers the same
questions at runtime:

```bash
trello-cli --help                 # every command, grouped, plus the global limits
trello-cli --help --create-card   # usage, options and limits for one command
trello-cli --commands             # the same catalog as JSON: commands, limits, error codes
trello-cli --commands --create-card
```

Use `--commands` when parsing: it returns `{"ok":true,"data":{...}}` with `commands`
(each with `usage`, `arguments`, `options`, `notes`), `errorCodes` and `restrictions`.
An unrecognized command returns `UNKNOWN_COMMAND` naming the closest match.

---

## Output Format

All commands return JSON:

```json
// Success
{"ok": true, "data": [...]}

// Error
{"ok": false, "error": "Error message", "code": "ERROR_CODE"}
```

### Error Codes

| Code | Meaning |
|------|---------|
| `AUTH_ERROR` | Credentials are missing or incomplete. |
| `UNAUTHORIZED` | Trello rejected the API key or token. |
| `UNKNOWN_COMMAND` | The command is not recognized; run --help or --commands for the list. |
| `MISSING_PARAM` | A required argument was not provided. |
| `INVALID_PARAM` | An argument was provided in an unsupported form. |
| `NO_PARAMS` | An update command was called without any field to change. |
| `NOT_FOUND` | The board, list, card, label, checklist, item, attachment, member or comment does not exist or is not visible to the token. |
| `FILE_NOT_FOUND` | The local file passed to --upload-attachment does not exist. |
| `CREATE_FAILED` | Trello accepted the request but returned no usable resource. |
| `UPDATE_FAILED` | Trello accepted the request but returned no usable resource. |
| `UPLOAD_FAILED` | The attachment upload returned no usable resource. |
| `ATTACH_FAILED` | The URL attachment returned no usable resource. |
| `LINK_ATTACHMENT` | The attachment is a link rather than a file Trello hosts; the message carries the URL to fetch. |
| `FILE_EXISTS` | The download destination already exists and `--overwrite` was not passed. |
| `DIRECTORY_NOT_FOUND` | The directory for the download destination does not exist and could not be created. |
| `PATH_TRAVERSAL` | The attachment file name resolved outside the requested directory and was refused. |
| `NAME_COLLISION` | No free file name was available for an attachment in the output directory. |
| `DOWNLOAD_INCOMPLETE` | The download ended before the whole file arrived; no partial file was kept. |
| `DOWNLOAD_FAILED` | The attachment could not be downloaded. |
| `TOO_MANY_REDIRECTS` | The download redirected more times than allowed. |
| `REDIRECT_BLOCKED` | The download redirected to an unsupported scheme or downgraded to plain HTTP. |
| `REDIRECT_INVALID` | The download returned a redirect with no location to follow. |
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

---

## Commands

### Authentication

```bash
# Check if authenticated
trello-cli --check-auth
# Returns: {"ok":true,"data":{"id":"...","username":"...","fullName":"..."}}

# Set credentials (one-time); enter the token at the hidden terminal prompt
trello-cli --set-auth <api-key>

# Clear saved credentials
trello-cli --clear-auth
```

Never pass the token as another argument. For CI and other headless sessions,
set both `TRELLO_API_KEY` and `TRELLO_TOKEN`; environment credentials take
precedence over persisted credentials. `--clear-auth` removes the persisted API
key and OS-stored token, but it does not revoke the Trello token or unset the
environment. Its success data reports `environmentOverridesRemainActive`, and
partial deletion is reported as an error.

Token storage is platform-native:

- Windows Credential Manager: target `trello-cli`, username `trello-token`.
- macOS Keychain: service `trello-cli`, account `trello-token`.
- Linux Secret Service: `service=trello-cli`, `account=trello-token`. Install
  `secret-tool` from `libsecret-tools` and ensure an unlocked Secret Service is
  available on the D-Bus session; installing the CLI alone does not start one.

On upgrade to 2.0.0, a legacy plaintext token is written to the secure store,
read back, and exactly verified before the configuration file is atomically
rewritten without the token. Any failure preserves the original file and token
for the current run and emits only a sanitized warning so migration can retry.

### Board Operations

```bash
# List all boards
trello-cli --get-boards
# Returns: {"ok":true,"data":[{"id":"...","name":"Board Name","url":"..."}]}

# Get specific board
trello-cli --get-board <board-id>
# Returns: {"ok":true,"data":{"id":"...","name":"...","desc":"...","url":"..."}}
```

### List Operations

```bash
# Get all lists in a board
trello-cli --get-lists <board-id>
# Returns: {"ok":true,"data":[{"id":"...","name":"To Do"},{"id":"...","name":"Done"}]}

# Create new list
trello-cli --create-list <board-id> "<list-name>"
# Returns: {"ok":true,"data":{"id":"...","name":"..."}}

# Reposition a list (top, bottom, or a number)
trello-cli --move-list <list-id> top
# Returns: {"ok":true,"data":{"id":"...","name":"...","pos":...}}

# Reposition several lists at once, as <list-id>:<pos> pairs
trello-cli --bulk-move-lists <list-id>:top <list-id>:2 <list-id>:bottom
# Returns: {"ok":true,"data":[{"listId":"...","pos":"top","ok":true,"data":{...}}]}
```

Only open lists are returned by `--get-lists`. Lists cannot be renamed, archived or
deleted here. `--bulk-move-lists` is sequential and stops at the first failure, so the
pairs before it stay applied and the response reports where it stopped.

#### Updating and Archiving Lists

```bash
# Rename or reposition
trello-cli --update-list <list-id> --name "In Review"
trello-cli --update-list <list-id> --pos top

# Archive and restore; a list's cards are archived with it and return with it
trello-cli --archive-list <list-id>
trello-cli --unarchive-list <list-id>

# Empty a list without removing it
trello-cli --archive-all-cards <list-id>
trello-cli --move-all-cards <source-list-id> <target-list-id>
```

Lists cannot be deleted; `--archive-list` is the reversible equivalent. Archived lists
are hidden from `--get-lists` unless you pass `--filter closed` or `--filter all`.
`--move-all-cards` reads the target list first to find its board, so it makes two
requests, and both lists must be on the same board.

### Card Operations

#### Reading Cards

```bash
# Get cards in a specific list
trello-cli --get-cards <list-id>

# Get ALL cards in a board (recommended)
trello-cli --get-all-cards <board-id>

# Get single card with full details
trello-cli --get-card <card-id>
# Returns: {"ok":true,"data":{"id":"...","name":"...","desc":"...","due":"...","idList":"..."}}
```

#### Creating Cards

```bash
# Simple card
trello-cli --create-card <list-id> "<card-name>"

# Card with description
trello-cli --create-card <list-id> "<card-name>" --desc "<description>"

# Card with due date (ISO format)
trello-cli --create-card <list-id> "<card-name>" --due "2025-01-15"

# Full card
trello-cli --create-card <list-id> "<card-name>" --desc "<description>" --due "2025-01-15"

# Card with labels and/or members (single API call - preferred over create-then-update)
trello-cli --create-card <list-id> "<card-name>" --labels "<id1>,<id2>"
trello-cli --create-card <list-id> "<card-name>" --members "<id1>,<id2>"
trello-cli --create-card <list-id> "<card-name>" --desc "<desc>" --labels "<id1>" --members "<id1>"
```

#### Updating Cards

```bash
# Update name only
trello-cli --update-card <card-id> --name "<new-name>"

# Update description only
trello-cli --update-card <card-id> --desc "<new-description>"

# Update due date only
trello-cli --update-card <card-id> --due "2025-01-20"

# Update multiple fields at once
trello-cli --update-card <card-id> --name "<name>" --desc "<desc>" --due "<date>"

# Clear due date
trello-cli --update-card <card-id> --due ""

# Archive/unarchive card
trello-cli --update-card <card-id> --closed true
trello-cli --update-card <card-id> --closed false
```

#### Moving Cards

```bash
# Move card to another list
trello-cli --move-card <card-id> <target-list-id>
```

#### Archiving Cards

```bash
# Archive a card (set closed=true)
trello-cli --archive-card <card-id>
# Returns: {"ok":true,"data":{"id":"...","name":"...","closed":true,...}}

# Unarchive a card (set closed=false)
trello-cli --unarchive-card <card-id>
# Returns: {"ok":true,"data":{"id":"...","name":"...","closed":false,...}}

# Alternative: use --update-card with --closed flag
trello-cli --update-card <card-id> --closed true
trello-cli --update-card <card-id> --closed false
```

#### Deleting Cards

```bash
# Delete card permanently (cannot be undone!)
trello-cli --delete-card <card-id>
# Returns: {"ok":true,"data":true}
```

#### Copying Cards

```bash
# Copy a card, carrying everything over
trello-cli --copy-card <card-id> <target-list-id>

# Rename the copy, place it, or narrow what comes along
trello-cli --copy-card <card-id> <target-list-id> --name "Retry" --position top
trello-cli --copy-card <card-id> <target-list-id> --keep checklists,labels
```

`--keep` defaults to `all`. Trello's own default is the name alone, which is rarely
what a copy is for. Unlike `--move-card`, the destination list may be on another board.

#### Positioning, Dates and Covers

```bash
# Reorder a card inside its list (--move-card changes which list it is in)
trello-cli --set-card-position <card-id> top
trello-cli --set-card-position <card-id> 65535

# Tick or untick the due date; this does not archive or move the card
trello-cli --set-due-complete <card-id> true

# Set or clear a start date
trello-cli --set-start-date <card-id> 2026-03-01
trello-cli --set-start-date <card-id> ""

# Cover: a color, or an image already attached to this card
trello-cli --set-card-cover <card-id> --color blue --size full
trello-cli --set-card-cover <card-id> --attachment <attachment-id> --brightness dark
trello-cli --clear-card-cover <card-id>
```

`--set-card-cover` needs at least one option, or it returns `NO_PARAMS`.
`--clear-card-cover` removes the cover only; an attachment used as one stays on the card.

#### Card Activity

```bash
# Who changed this card, and when
trello-cli --get-card-activity <card-id> --limit 20
trello-cli --get-card-activity <card-id> --filter updateCard,commentCard
# Returns: {"ok":true,"data":[{"id":"...","type":"updateCard","date":"...","memberCreator":{...},"data":{...}}]}
```

Each entry's `data` differs by action type and is passed through exactly as Trello
sends it, so read the `type` before reaching into `data`.

### Comment Operations

#### Reading Comments

```bash
# Get all comments on a card
trello-cli --get-comments <card-id>
# Returns: {"ok":true,"data":[{"id":"...","date":"...","data":{"text":"..."},"memberCreator":{"id":"...","fullName":"...","username":"..."}}]}
```

#### Adding Comments

```bash
# Add a comment to a card
trello-cli --add-comment <card-id> "<comment-text>"
# Returns: {"ok":true,"data":{"id":"...","date":"...","data":{"text":"..."},"memberCreator":{"id":"...","fullName":"...","username":"..."}}}
```

#### Updating and Deleting Comments

The comment ID is the `id` of the `commentCard` action that `--get-comments` returns.
Trello only lets an account change its own comments.

```bash
# Rewrite a comment
trello-cli --update-comment <card-id> <comment-id> "Corrected note"
# Returns: {"ok":true,"data":{"id":"...","date":"...","data":{"text":"Corrected note"},"memberCreator":{...}}}

# Delete a comment (permanent)
trello-cli --delete-comment <card-id> <comment-id>
# Returns: {"ok":true,"data":true}
```

### Search Operations

Search is how you turn words into IDs. Every other command needs an ID you already
have; this is the one that finds them, so reach for it before `--get-all-cards`.

```bash
# Everything the token can see
trello-cli --search "login page"
# Returns: {"ok":true,"data":{"cards":[...],"boards":[...],"members":[...]}}

# Narrow to one board, cap the results, and ask for cards only
trello-cli --search "login page" --board <board-id> --limit 10 --cards-only
# Returns: {"ok":true,"data":{"cards":[...],"boards":[],"members":[]}}

# Trello's own search operators work inside the query
trello-cli --search "label:red due:week"

# Find people by name or username
trello-cli --search-members "alex" --limit 5
# Returns: {"ok":true,"data":[{"id":"...","username":"alexdoe","fullName":"Alex Doe"}]}
```

Matching is partial, so a fragment of a card title is enough. Each collection in the
response is empty rather than absent when nothing matched.

### Member Operations

```bash
# Which account does this token act as? Everything the CLI does happens as this member.
trello-cli --whoami
# Returns: {"ok":true,"data":{"id":"...","username":"alexdoe","fullName":"Alex Doe","initials":"AD"}}

# Look someone up by ID or username
trello-cli --get-member alexdoe

# Board members: these are the IDs --members and --add-card-member accept
trello-cli --get-members <board-id>

# Members assigned to one card
trello-cli --get-card-members <card-id>

# Everything assigned to me, across every board I can see
trello-cli --get-my-cards
trello-cli --get-my-cards --filter all
```

#### Assigning Members and Labels to Cards

`--labels` and `--members` on `--update-card` replace the entire set. These commands
change one entry and leave the rest alone, which is usually what you want.

```bash
# Members
trello-cli --add-card-member <card-id> <member-id>
trello-cli --remove-card-member <card-id> <member-id>
# Both return the card's resulting member list.

# Labels
trello-cli --add-card-label <card-id> <label-id>
trello-cli --remove-card-label <card-id> <label-id>
# Both return the card's resulting label IDs. Removing a label from a card
# leaves the label itself on the board.
```

### Label Operations

#### Listing Labels

```bash
# List all labels on a board
trello-cli --get-labels <board-id>
# Returns: {"ok":true,"data":[{"id":"...","idBoard":"...","name":"bug","color":"red","uses":5}]}
```

#### Creating Labels

```bash
# Create a label with a color
trello-cli --create-label <board-id> "<label-name>" --color <color>
# Valid colors: green, yellow, orange, red, purple, blue, sky, lime, pink, black
# Each accepts _light / _dark suffix (e.g. green_dark, red_light). Pass empty for no color.
# Returns: {"ok":true,"data":{"id":"...","idBoard":"...","name":"...","color":"..."}}

# Create a label with no color
trello-cli --create-label <board-id> "<label-name>"
```

#### Updating Labels

```bash
# Rename a label
trello-cli --update-label <label-id> --name "<new-name>"

# Recolor a label
trello-cli --update-label <label-id> --color <new-color>

# Both at once
trello-cli --update-label <label-id> --name "<new-name>" --color <new-color>
```

#### Deleting Labels

```bash
# Delete a label permanently (removes from all cards)
trello-cli --delete-label <label-id>
# Returns: {"ok":true,"data":true}
```

### Attachment Operations

**Note:** `--download-attachment` and `--download-all-attachments` fetch attachments Trello hosts, authenticating with the same API token as every other command. Attachments that are links to somewhere else are not fetched for you: they return `LINK_ATTACHMENT` with the URL, and the bulk command lists them under `data.skipped`.

#### Listing Attachments

```bash
# List all attachments on a card
trello-cli --list-attachments <card-id>
# Returns: {"ok":true,"data":[{"id":"...","name":"file.pdf","url":"...","bytes":12345,"mimeType":"application/pdf","date":"...","isUpload":true}]}
```

#### Uploading Attachments

```bash
# Upload a local file
trello-cli --upload-attachment <card-id> "/path/to/file.pdf"

# Upload with custom name
trello-cli --upload-attachment <card-id> "/path/to/file.pdf" --name "Project Spec"
# Returns: {"ok":true,"data":{"id":"...","name":"Project Spec","url":"...","bytes":12345,"mimeType":"application/pdf"}}
```

#### Attaching URLs

```bash
# Attach a URL to a card
trello-cli --attach-url <card-id> "https://example.com/document.pdf"

# Attach URL with custom name
trello-cli --attach-url <card-id> "https://example.com/document.pdf" --name "External Doc"
# Returns: {"ok":true,"data":{"id":"...","name":"External Doc","url":"https://example.com/document.pdf"}}
```

#### Deleting Attachments

```bash
# Read one attachment's metadata; isUpload tells you whether Trello hosts the file
trello-cli --get-attachment <card-id> <attachment-id>

# Download a Trello-hosted attachment into the current directory
trello-cli --download-attachment <card-id> <attachment-id>
# Returns: {"ok":true,"data":{"id":"...","name":"Spec","fileName":"spec.pdf","path":"/abs/path/spec.pdf","bytes":12345,"mimeType":"application/pdf"}}

# Choose the destination; --output may name a file or a directory
trello-cli --download-attachment <card-id> <attachment-id> --output ./spec.pdf --overwrite

# Download every Trello-hosted attachment on a card
trello-cli --download-all-attachments <card-id> --output-dir ./attachments
# Returns: {"ok":true,"data":{"directory":"...","downloaded":[...],"skipped":[{"id":"...","name":"...","url":"...","reason":"LINK_ATTACHMENT"}],"failed":[]}}

# Delete an attachment from a card
trello-cli --delete-attachment <card-id> <attachment-id>
# Returns: {"ok":true,"data":true}
```

### Checklist Operations

#### Getting Checklists

```bash
# Get all checklists on a card
trello-cli --get-checklists <card-id>
# Returns: {"ok":true,"data":[{"id":"...","name":"My Checklist","idCard":"...","checkItems":[{"id":"...","name":"Item 1","state":"incomplete"},{"id":"...","name":"Item 2","state":"complete"}]}]}
```

#### Creating Checklists

```bash
# Create a checklist on a card
trello-cli --create-checklist <card-id> "<checklist-name>"
# Returns: {"ok":true,"data":{"id":"...","name":"My Checklist","idCard":"...","checkItems":[]}}
```

#### Deleting Checklists

```bash
# Delete a checklist
trello-cli --delete-checklist <checklist-id>
# Returns: {"ok":true,"data":true}
```

#### Adding Checklist Items

```bash
# Add an item to a checklist
trello-cli --add-checklist-item <checklist-id> "<item-name>"
# Returns: {"ok":true,"data":{"id":"...","name":"Item name","state":"incomplete","idChecklist":"..."}}
```

#### Updating Checklist Items

```bash
# Mark item as complete
trello-cli --update-checklist-item <card-id> <item-id> complete
# Returns: {"ok":true,"data":{"id":"...","name":"Item name","state":"complete",...}}

# Mark item as incomplete
trello-cli --update-checklist-item <card-id> <item-id> incomplete
# Returns: {"ok":true,"data":{"id":"...","name":"Item name","state":"incomplete",...}}
```

#### Renaming and Reordering Checklists

```bash
# The checklist itself
trello-cli --update-checklist <checklist-id> --name "Release steps"
trello-cli --update-checklist <checklist-id> --pos top

# Its items; like --update-checklist-item these take the CARD id
trello-cli --rename-checklist-item <card-id> <item-id> --name "Ship it"
trello-cli --move-checklist-item <card-id> <item-id> --pos top
```

#### Deleting Checklist Items

```bash
# Delete an item from a checklist
trello-cli --delete-checklist-item <checklist-id> <item-id>
# Returns: {"ok":true,"data":true}
```

---

## Common Workflows

### 1. First Time Setup

```bash
# Get API key from: https://trello.com/app-key
# Get Token from the same page (click "Token" link)
trello-cli --set-auth <your-api-key>  # Enter the token at the hidden prompt
trello-cli --check-auth  # Verify it works
```

Headless alternative:

```bash
export TRELLO_API_KEY='<your-api-key>'
export TRELLO_TOKEN='<your-token>'
trello-cli --check-auth
```

### 2. Explore Board Structure

```bash
trello-cli --get-boards                    # Find your board
trello-cli --get-lists <board-id>          # See all lists
trello-cli --get-all-cards <board-id>      # See all cards
```

### 3. Create Task with Full Details

```bash
# Step 1: Find the target list
trello-cli --get-lists <board-id>

# Step 2: Create the card
trello-cli --create-card <list-id> "Implement login feature" \
  --desc "Add OAuth2 authentication with Google and GitHub providers" \
  --due "2025-01-20"
```

### 4. Move Card Through Workflow

```bash
# Get list IDs
trello-cli --get-lists <board-id>
# Example output shows: To Do (id1), In Progress (id2), Done (id3)

# Move from To Do to In Progress
trello-cli --move-card <card-id> <in-progress-list-id>

# Later, move to Done
trello-cli --move-card <card-id> <done-list-id>
```

### 5. Update Existing Card

```bash
# Get card details first
trello-cli --get-card <card-id>

# Update what you need
trello-cli --update-card <card-id> --desc "Updated requirements: ..."
```

### 6. Work with Attachments

```bash
# List attachments on a card
trello-cli --list-attachments <card-id>

# Upload a file to a card
trello-cli --upload-attachment <card-id> "./document.pdf" --name "Project Spec"

# Attach a URL (also used to link attachments between cards)
trello-cli --attach-url <card-id> "https://docs.google.com/..." --name "Design Doc"

# Delete an attachment
trello-cli --delete-attachment <card-id> <attachment-id>
```

Note: To copy an attachment between cards, use `--list-attachments` on the source card to get the URL, then `--attach-url` on the target card.
To get the bytes onto disk instead, use `--download-attachment`, or `--download-all-attachments --output-dir <dir>` for the whole card.

### 7. Work with Checklists

```bash
# Get existing checklists on a card
trello-cli --get-checklists <card-id>

# Create a new checklist
trello-cli --create-checklist <card-id> "Implementation Tasks"

# Add items to the checklist
trello-cli --add-checklist-item <checklist-id> "Write unit tests"
trello-cli --add-checklist-item <checklist-id> "Update documentation"
trello-cli --add-checklist-item <checklist-id> "Code review"

# Mark items as complete
trello-cli --update-checklist-item <card-id> <item-id> complete

# Delete a checklist when done
trello-cli --delete-checklist <checklist-id>
```

---

## Restrictions

Also printed by `trello-cli --help` and returned under `data.restrictions` by
`trello-cli --commands`.

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
- Lists cannot be deleted. Archiving one with `--archive-list` is the closest equivalent and is reversible.
- Only Trello-hosted attachments can be downloaded. A link attachment is not fetched for you; `--download-attachment` returns its URL so you can retrieve it yourself.
- `--move-card` cannot move a card to a different board; `--copy-card` can copy one across.
- No workspace or organization management. Members can be read and assigned to cards, but not invited, removed from a board, or given a different role.
- Only comments written by the token's own account can be edited or deleted.
- Custom fields, stickers, power-ups, webhooks, board backgrounds and notifications are out of scope.
- Except for `--bulk-move-lists` there is no batching: one command performs one operation.

**What the read commands return**

- `--get-boards`, `--get-lists` and `--get-all-cards` return open items unless you pass `--filter closed` or `--filter all`.
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

---

## Tips

1. **Use `--get-all-cards`** instead of multiple `--get-cards` calls
2. **Cache board and list IDs** - they don't change often
3. **ISO date format** for due dates: `YYYY-MM-DD`
4. **Quote strings with spaces** in card names and descriptions
5. **Check `ok` field** in response before processing data
