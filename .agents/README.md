# Antigravity Cloud Environment Setup

The configuration in [`environment.json`](./environment.json) is **specific to Antigravity Cloud** Ubuntu Linux VMs and is not intended for local developer machines (macOS/Windows/local Linux).

When a new Antigravity Cloud session boots for this repository, its `install` command runs from the repository root to prepare:

- Linux Secret Service packages (`dbus-x11`, `gnome-keyring`, `libsecret-tools`) required for `PlatformCredentialStoreSmokeTests`
- Locked NuGet restore, Release build, NuGet pack, and global .NET tool installation of `trello-cli` (`~/.dotnet/tools`)
- Persistent `PATH` export for `~/.dotnet/tools` in `~/.bashrc`
- Symlinks for the `trello-cli` skill in `~/.agents/skills/trello-cli` and `~/.claude/skills/trello-cli`

> **Credentials:** No API keys or tokens are stored here. In headless cloud sessions, provide `TRELLO_API_KEY` and `TRELLO_TOKEN` via environment variables.
