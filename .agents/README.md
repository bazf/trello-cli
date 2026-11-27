# Antigravity Cloud Environment Setup

The configuration in [`environment.json`](./environment.json) is **specific to Antigravity Cloud** Ubuntu Linux VMs and is not intended for local developer machines (macOS/Windows/local Linux).

To keep cloud VM startup fast (~1–2 seconds), `environment.json`:

- Downloads the prebuilt `trello-cli-linux-x64` single-file binary and `trello-cli-linux-x64.sha256` from the latest GitHub Release, verifies the SHA-256 checksum, and installs it to `~/.local/bin/trello-cli` (reusing the VM's preinstalled .NET 10 runtime)
- Falls back automatically to building/packing `src/TrelloCli.csproj` and installing via `dotnet tool update --global` if no release asset is reachable
- Persists `~/.local/bin` and `~/.dotnet/tools` on `PATH` in `~/.bashrc`
- Symlinks the `trello-cli` skill into `~/.agents/skills/trello-cli` and `~/.claude/skills/trello-cli`

> **Credentials:** No API keys or tokens are stored here. In headless cloud sessions, provide `TRELLO_API_KEY` and `TRELLO_TOKEN` via environment variables.

## On-demand test dependencies

When running the test suite or Linux Secret Service smoke tests in a cloud session:

```bash
# Unit tests
dotnet restore tests/TrelloCli.Tests/TrelloCli.Tests.csproj --locked-mode --nologo
dotnet test tests/TrelloCli.Tests/TrelloCli.Tests.csproj --configuration Release --no-restore --nologo

# Linux Secret Service smoke tests (PlatformCredentialStoreSmokeTests)
sudo apt-get update -qq && sudo DEBIAN_FRONTEND=noninteractive apt-get install -y -qq --no-install-recommends dbus-x11 gnome-keyring libsecret-tools
```
