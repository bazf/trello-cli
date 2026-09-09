# Trello CLI Tool

You can manage Trello via `trello-cli`. All outputs are JSON: `{"ok":true,"data":...}` or `{"ok":false,"error":"...","code":"..."}`.

## Commands

| Command | Usage |
|---------|-------|
| `--get-boards` | List all boards |
| `--get-board <id>` | Get board details |
| `--get-lists <board-id>` | Get lists in board |
| `--create-list <board-id> "<name>"` | Create list |
| `--get-cards <list-id>` | Get cards in list |
| `--get-all-cards <board-id>` | Get all cards in board |
| `--get-card <card-id>` | Get card details |
| `--create-card <list-id> "<name>" [--desc "..."] [--due "YYYY-MM-DD"]` | Create card |
| `--update-card <card-id> [--name "..."] [--desc "..."] [--due "..."]` | Update card |
| `--move-card <card-id> <list-id>` | Move card to list |
| `--delete-card <card-id>` | Delete card |
| `--get-comments <card-id>` | Get comments on card |
| `--add-comment <card-id> "<text>"` | Add comment to card |
| `--set-auth <api-key>` | Save API key and securely prompt for token |
| `--clear-auth` | Remove persisted auth; environment values remain active |
| `--check-auth` | Verify auth |

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
