# Fork Repository Identity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make all repository links and package metadata identify `bazf/trello-cli` as the repository.

**Architecture:** Apply a literal identity replacement only at the two README clone commands and the NuGet `RepositoryUrl`. Validate both source text and the generated package metadata without changing application behavior.

**Tech Stack:** Markdown, MSBuild project metadata, .NET 10 SDK, xUnit, NuGet package tooling

## Global Constraints

- Both README clone commands must use `https://github.com/bazf/trello-cli.git`.
- NuGet `RepositoryUrl` must use `https://github.com/bazf/trello-cli`.
- Retain no original-owner name or repository URL anywhere outside `.git`.
- Do not change package IDs, command names, source behavior, or unrelated documentation.
- Recurring PR monitoring remains disabled.

---

### Task 1: Replace and verify repository identity

**Files:**
- Modify: `README.md:32`
- Modify: `README.md:48`
- Modify: `src/TrelloCli.csproj:21`
- Create: `docs/superpowers/plans/2026-08-11-fork-repository-identity.md`

**Interfaces:**
- Consumes: Existing README installation commands and MSBuild `RepositoryUrl` package metadata.
- Produces: README clone commands and packed NuGet metadata pointing to `https://github.com/bazf/trello-cli`.

- [ ] **Step 1: Prove the acceptance scan fails before replacement**

Run:

```bash
old_owner="$(printf '%s%s' Zenox ZX)"
! rg -n -i "$old_owner|github\.com/$old_owner/trello-cli" --hidden --glob '!.git/**' .
```

Expected: FAIL because `README.md` and `src/TrelloCli.csproj` still contain the original repository identity.

- [ ] **Step 2: Apply the literal replacements**

Replace both README clone commands with:

```text
git clone https://github.com/bazf/trello-cli.git
```

Replace the project metadata with:

```xml
<RepositoryUrl>https://github.com/bazf/trello-cli</RepositoryUrl>
```

- [ ] **Step 3: Verify the repository-wide identity scan**

Run:

```bash
old_owner="$(printf '%s%s' Zenox ZX)"
! rg -n -i "$old_owner|github\.com/$old_owner/trello-cli" --hidden --glob '!.git/**' .
rg -n 'github\.com/bazf/trello-cli' README.md src/TrelloCli.csproj
git diff --check
```

Expected: no original-owner matches, exactly three fork URL matches in the target files, and a clean diff check.

- [ ] **Step 4: Verify Release behavior and package output**

Run:

```bash
dotnet test tests/TrelloCli.Tests/TrelloCli.Tests.csproj --configuration Release --no-restore --nologo --verbosity minimal
dotnet build src/TrelloCli.csproj --configuration Release --no-restore --nologo
dotnet pack src/TrelloCli.csproj --configuration Release --no-restore --nologo --output /private/tmp/trello-cli-fork-identity-pack
```

Expected: all tests pass, build reports zero warnings and errors, and `TrelloCli.2.0.0.nupkg` is created.

Inspect `TrelloCli.nuspec` inside the package and confirm:

```xml
<repository type="git" url="https://github.com/bazf/trello-cli" />
```

- [ ] **Step 5: Commit and push**

```bash
git add README.md src/TrelloCli.csproj docs/superpowers/plans/2026-08-11-fork-repository-identity.md
git commit -m "docs: point repository links to fork"
git push origin codex/secure-credential-storage
```

- [ ] **Step 6: Recheck PR state once**

Run the thread-aware review fetch and `gh pr view` for PR #1. Confirm the remote head matches the pushed commit, report any immediately available checks or actionable feedback, and do not recreate recurring monitoring.
