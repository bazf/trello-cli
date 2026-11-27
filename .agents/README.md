# Antigravity Cloud Environment Setup

The configuration in [`environment.json`](./environment.json) is **specific to Antigravity Cloud** Ubuntu Linux VMs and is not intended for local developer machines (macOS/Windows/local Linux).

To keep cloud VM startup fast, `environment.json` installs only what is needed to run `trello-cli`:

- Locked NuGet restore and Release pack of `src/TrelloCli.csproj` only, plus global .NET tool installation (`~/.dotnet/tools`)
- Persistent `PATH` export for `~/.dotnet/tools` in `~/.bashrc`
- Symlinks for the `trello-cli` skill in `~/.agents/skills/trello-cli` and `~/.claude/skills/trello-cli`

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
