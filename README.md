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

```bash
# Authentication
trello-cli --set-auth <api-key>  # token is entered at the hidden prompt
trello-cli --clear-auth
trello-cli --check-auth

# Board operations
trello-cli --get-boards
trello-cli --get-board <board-id>

# List operations
trello-cli --get-lists <board-id>
trello-cli --create-list <board-id> "<name>"

# Card operations
trello-cli --get-cards <list-id>
trello-cli --get-all-cards <board-id>
trello-cli --get-card <card-id>
trello-cli --create-card <list-id> "<name>" [--desc "<desc>"] [--due "YYYY-MM-DD"] [--labels "<ids>"] [--members "<ids>"]
trello-cli --update-card <card-id> [--name "<name>"] [--desc "<desc>"] [--due "<date>"] [--labels "<ids>"] [--members "<ids>"]
trello-cli --move-card <card-id> <target-list-id>
trello-cli --archive-card <card-id>
trello-cli --unarchive-card <card-id>
trello-cli --delete-card <card-id>

# Label operations
trello-cli --get-labels <board-id>
trello-cli --create-label <board-id> "<name>" [--color <color>]
trello-cli --update-label <label-id> [--name "<name>"] [--color <color>]
trello-cli --delete-label <label-id>

# Comment operations
trello-cli --get-comments <card-id>
trello-cli --add-comment <card-id> "<text>"

# Attachment operations
trello-cli --list-attachments <card-id>
trello-cli --upload-attachment <card-id> <file-path> [--name "<name>"]
trello-cli --attach-url <card-id> <url> [--name "<name>"]
trello-cli --delete-attachment <card-id> <attachment-id>

# Note: Downloading attachments is not supported - Trello's download API
# requires browser authentication. Use --attach-url to link attachments.

# Checklist operations
trello-cli --get-checklists <card-id>
trello-cli --create-checklist <card-id> "<name>"
trello-cli --delete-checklist <checklist-id>
trello-cli --add-checklist-item <checklist-id> "<name>"
trello-cli --update-checklist-item <card-id> <item-id> <complete|incomplete>
trello-cli --delete-checklist-item <checklist-id> <item-id>
```

## Requirements

- .NET 10.0 or later
- Trello account with API access

## License

This project is licensed under the [MIT License](LICENSE).
