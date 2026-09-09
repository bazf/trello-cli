# Trello CLI

A CLI tool that provides Trello integration with Claude Code. With this tool, you can manage your Trello boards, lists, and cards using natural language through Claude Code.

## What is it?

`trello-cli` is a command-line tool that communicates with the Trello API. Claude Code uses this tool to:

- List your boards
- View, create, and update your cards
- Move cards between lists
- Track your tasks

## Installation

`trello-cli` 2.0.0 requires the .NET 10 SDK to build. Credential storage is
provided by the operating system:

- Windows: Windows Credential Manager.
- macOS: Keychain.
- Linux: Secret Service over D-Bus. Install `secret-tool` (the
  `libsecret-tools` package) and run a Secret Service provider such as GNOME
  Keyring. A package by itself is not sufficient in a headless session; the
  session must also have a D-Bus address and an unlocked Secret Service.

### Quick Install (Recommended)

Install directly from GitHub using the install script. Requires [Homebrew](https://brew.sh).

```bash
# Clone and install
git clone https://github.com/bazf/trello-cli.git
cd trello-cli
./install.sh
```

The install script will:
- Install .NET SDK via Homebrew if needed
- Build and install trello-cli as a global tool
- Install Claude Code skills to `~/.claude/skills/`

### Manual Build from Source

If you prefer to install manually or don't use Homebrew:

```bash
# Clone the repository
git clone https://github.com/bazf/trello-cli.git
cd trello-cli

# Install as global tool (requires .NET SDK 10.0+)
dotnet pack src/TrelloCli.csproj -c Release
dotnet tool install --global --add-source src/nupkg TrelloCli

# Verify installation
trello-cli --help
```

### Uninstalling

```bash
# The script asks whether to remove saved credentials before removing the tool.
# The default answer preserves credentials.
./uninstall.sh

# Or remove credentials and the tool manually
trello-cli --clear-auth
dotnet tool uninstall --global TrelloCli
```

Uninstalling the .NET tool alone does not remove credentials. `--clear-auth`
removes the saved API key configuration and the token in the OS credential
store, but does not revoke the Trello token or unset `TRELLO_API_KEY` /
`TRELLO_TOKEN`. Environment overrides therefore remain active until they are
unset separately.

### Setting Up Trello API Credentials

1. Get your API key and token from https://trello.com/app-key
2. Click the "Token" link on that page to generate a token
3. Configure the CLI:

```bash
# Save the API key and enter the token at the hidden terminal prompt
trello-cli --set-auth <api-key>

# Verify authentication
trello-cli --check-auth
```

The token is never accepted as a positional argument. On Windows it is saved
under target `trello-cli` / username `trello-token` in Credential Manager; on
macOS under service `trello-cli` / account `trello-token` in Keychain; and on
Linux under Secret Service attributes `service=trello-cli` and
`account=trello-token`. The API key remains in
`~/.trello-cli/config.json`; newly saved configuration never serializes the
token.

For CI, containers, SSH sessions, and other headless environments, provide both
values through the environment instead of using the interactive command:

```bash
export TRELLO_API_KEY='<api-key>'
export TRELLO_TOKEN='<token>'
trello-cli --check-auth
```

Environment values have precedence: `TRELLO_API_KEY` overrides the saved API
key and a nonblank `TRELLO_TOKEN` bypasses credential-store access and legacy
migration for that run.

### Migration from releases before 2.0.0

If an existing `~/.trello-cli/config.json` contains a plaintext token, 2.0.0
attempts a one-time migration. It writes the token to the OS credential store,
reads it back, compares the exact value, and only then atomically rewrites the
file with the API key alone. If storage, verification, or the rewrite fails,
the original file and token remain available for the current run, a sanitized
warning is emitted, and migration can be retried on a later run. A failed new
setup restores the previous secure token (or removes the newly created entry)
if the API-key file cannot be persisted.

`--clear-auth` attempts secure-token and configuration deletion independently.
It reports partial failure rather than claiming full cleanup and includes
`environmentOverridesRemainActive` in its success data.

## Usage

Simply mention "Trello" when talking to Claude Code:

```
"Show my Trello tasks"
"Add a new card to Trello: Login page design"
"Move this card to Done on Trello"
"List my Trello boards"
```

### Discovering what the CLI can do

The CLI documents itself, so neither you nor an agent has to guess:

```bash
trello-cli --help                 # every command, grouped, plus the global limits
trello-cli --help --create-card   # usage, options and limits for a single command
trello-cli --commands             # the same catalog as JSON, for programmatic use
trello-cli --commands --create-card
```

`--commands` returns the standard `{"ok":true,"data":...}` envelope and carries the
command list, each command's arguments, options and per-command limits, the error
codes, and the restrictions listed below. It is the recommended entry point for an
agent: one call is enough to learn the whole surface. An unrecognized command is
reported as `UNKNOWN_COMMAND` with the closest match and a pointer back to these
two commands.

## Documentation

| File | Description |
|------|-------------|
| [docs/instruction.md](docs/instruction.md) | Detailed command reference and usage examples for AI |
| [docs/system-prompt.md](docs/system-prompt.md) | System prompt for AI integration |
| [plugins/trello-cli/skills/trello-cli/SKILL.md](plugins/trello-cli/skills/trello-cli/SKILL.md) | Claude Code skill definition and quick reference |
| [plugins/trello-cli/skills/trello-cli/REFERENCE.md](plugins/trello-cli/skills/trello-cli/REFERENCE.md) | Complete documentation of all commands |

## Claude Code Skill System

This repo uses Claude Code's **skill** system. Skills are configuration files that give Claude Code specialized capabilities.

### What is a Skill?

A skill is a markdown file that defines how Claude Code should use specific tools or APIs. They are located in the `.claude/skills/` directory.

### Adding the Skill to Your Personal Directory

To use this skill everywhere on your system, copy it to your personal `.claude` directory:

```bash
# Copy the skill folder to your personal directory
cp -r plugins/trello-cli/skills/trello-cli ~/.claude/skills/
```

After this, Claude Code will automatically activate this skill whenever you mention "Trello" in any directory.

### Skill Structure

```
~/.claude/
└── skills/
    └── trello-cli/
        ├── SKILL.md       # Main skill definition (trigger rules, quick reference)
        └── REFERENCE.md   # Detailed command documentation
```

### SKILL.md Anatomy

```markdown
---
name: trello-cli
description: Trello board, list and card management via CLI...
---

# Skill Content
...
```

- **name**: Unique name of the skill
- **description**: Description that determines when it activates (contains trigger words)

## Command Summary

Generated from the same catalog the CLI serves through `--help` and `--commands`;
run those for the authoritative, always-current version.

| Command | What it does |
|---------|--------------|
| `--help [<command>]` | Show this help, or the details of a single command. |
| `--commands [<command>]` | Print the command catalog, limits and error codes as JSON. |
| `--version` | Print the tool version. |
| `--set-auth <api-key>` | Save the API key and read the token from a hidden prompt. |
| `--check-auth` | Verify the credentials against Trello and return the member. |
| `--clear-auth` | Remove persisted credentials. |
| `--get-boards` | List the open boards of the authenticated member. |
| `--get-board <board-id>` | Get one board. |
| `--get-lists <board-id> [--filter <open\|closed\|all>]` | Get the open lists of a board. |
| `--create-list <board-id> <name>` | Create a list on a board. |
| `--move-list <list-id> <pos>` | Reposition a list on its board. |
| `--bulk-move-lists <list-id:pos>...` | Reposition several lists in one call. |
| `--update-list <list-id> [--name <text>] [--pos <top\|bottom\|number>]` | Rename or reposition a list. |
| `--archive-list <list-id>` | Archive a list and its cards. |
| `--unarchive-list <list-id>` | Restore an archived list. |
| `--archive-all-cards <list-id>` | Archive every card in a list, keeping the list. |
| `--move-all-cards <source-list-id> <target-list-id>` | Move every card from one list to another. |
| `--get-cards <list-id>` | Get the cards of a list. |
| `--get-all-cards <board-id> [--filter <open\|closed\|all>]` | Get every open card on a board. |
| `--get-card <card-id>` | Get one card. |
| `--create-card <list-id> <name> [--desc <text>] [--due <date>] [--labels <ids>] [--members <ids>]` | Create a card in a list. |
| `--update-card <card-id> [--name <text>] [--desc <text>] [--due <date>] [--labels <ids>] [--members <ids>] [--closed <true|false>]` | Change fields of a card. |
| `--move-card <card-id> <target-list-id>` | Move a card to another list. |
| `--archive-card <card-id>` | Archive a card. |
| `--unarchive-card <card-id>` | Restore an archived card. |
| `--delete-card <card-id>` | Delete a card. **Irreversible.** |
| `--copy-card <card-id> <target-list-id> [--name <text>] [--position <pos>] [--keep <what>]` | Copy a card into a list. |
| `--set-card-position <card-id> <top\|bottom\|number>` | Move a card within its list. |
| `--set-due-complete <card-id> <true\|false>` | Tick or untick a card's due date. |
| `--set-start-date <card-id> <date>` | Set or clear a card's start date. |
| `--set-card-cover <card-id> [--color <c>] [--attachment <id>] [--size <normal\|full>] [--brightness <light\|dark>]` | Set a card's cover. |
| `--clear-card-cover <card-id>` | Remove a card's cover. |
| `--get-card-activity <card-id> [--limit <n>] [--filter <types>]` | Read a card's activity feed. |
| `--get-comments <card-id>` | Get the comments on a card. |
| `--add-comment <card-id> <text>` | Add a comment to a card. |
| `--update-comment <card-id> <comment-id> <text>` | Rewrite an existing comment. |
| `--delete-comment <card-id> <comment-id>` | Delete a comment from a card. **Irreversible.** |
| `--add-card-label <card-id> <label-id>` | Add one label to a card, keeping the others. |
| `--remove-card-label <card-id> <label-id>` | Remove one label from a card. |
| `--whoami` | Show the account the current token belongs to. |
| `--get-member <member>` | Look up a member by id or username. |
| `--get-members <board-id>` | List the members of a board. |
| `--get-card-members <card-id>` | List the members assigned to a card. |
| `--get-my-cards [--filter <open\|closed\|all>]` | List the cards assigned to the current member. |
| `--add-card-member <card-id> <member-id>` | Assign a member to a card. |
| `--remove-card-member <card-id> <member-id>` | Unassign a member from a card. |
| `--search <query> [--board <board-id>] [--limit <n>] [--cards-only]` | Search Trello for cards and boards. |
| `--search-members <query> [--limit <n>]` | Search for members by name or username. |
| `--get-labels <board-id>` | List the labels defined on a board. |
| `--create-label <board-id> <name> [--color <color>]` | Create a label on a board. |
| `--update-label <label-id> [--name <text>] [--color <color>]` | Rename or recolor a label. |
| `--delete-label <label-id>` | Delete a label. **Irreversible.** |
| `--list-attachments <card-id>` | List the attachments on a card. |
| `--upload-attachment <card-id> <file-path> [--name <text>]` | Upload a local file to a card. |
| `--get-attachment <card-id> <attachment-id>` | Read one attachment's metadata. |
| `--download-attachment <card-id> <attachment-id> [--output <path>] [--overwrite]` | Download a Trello-hosted attachment to a local file. |
| `--download-all-attachments <card-id> [--output-dir <path>] [--overwrite]` | Download every Trello-hosted attachment on a card. |
| `--attach-url <card-id> <url> [--name <text>]` | Attach a URL to a card. |
| `--delete-attachment <card-id> <attachment-id>` | Delete an attachment from a card. **Irreversible.** |
| `--get-checklists <card-id>` | Get the checklists of a card, including their items. |
| `--create-checklist <card-id> <name>` | Create a checklist on a card. |
| `--delete-checklist <checklist-id>` | Delete a checklist and its items. **Irreversible.** |
| `--add-checklist-item <checklist-id> <name>` | Add an item to a checklist. |
| `--update-checklist-item <card-id> <item-id> <state>` | Mark a checklist item complete or incomplete. |
| `--update-checklist <checklist-id> [--name <text>] [--pos <pos>]` | Rename or reposition a checklist. |
| `--rename-checklist-item <card-id> <item-id> --name <text>` | Rename an item in a checklist. |
| `--move-checklist-item <card-id> <item-id> --pos <pos>` | Reorder an item within its checklist. |
| `--delete-checklist-item <checklist-id> <item-id>` | Delete an item from a checklist. **Irreversible.** |

Notes worth knowing up front:

- `--update-checklist-item` takes a **card** ID, while `--add-checklist-item` and
  `--delete-checklist-item` take a **checklist** ID.
- `--download-attachment` fetches files Trello hosts. A link attachment is not fetched
  for you: it returns `LINK_ATTACHMENT` with the URL so you can retrieve it yourself.
- `--labels` and `--members` replace the whole set on a card; pass `""` to clear it.
  Use `--add-card-label` / `--add-card-member` to change one without touching the rest.
- `--search` is how you turn words into IDs; the other commands all need an ID already.

## Limits and restrictions

The same list is printed by `trello-cli --help` and returned by
`trello-cli --commands` under `data.restrictions`.

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

## Requirements

- .NET 10.0 or later
- Trello account with API access

## License

This project is licensed under the [MIT License](LICENSE).
