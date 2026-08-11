# Fork Repository Identity Design

## Goal

Make every repository reference in this fork point to `bazf/trello-cli` instead of the original upstream repository.

## Scope

- Update both README clone commands to use `https://github.com/bazf/trello-cli.git`.
- Update the NuGet `RepositoryUrl` metadata to use `https://github.com/bazf/trello-cli`.
- Retain no attribution or link to the original repository owner.
- Do not change package IDs, command names, source behavior, or unrelated documentation.

## Verification

- Search the entire tracked and untracked repository, excluding `.git`, for the original owner name and repository URL; no matches may remain.
- Confirm all fork repository references use the expected `bazf/trello-cli` URL.
- Run the full Release test suite, Release build, and package creation so the metadata change is validated in the normal release path.
- Inspect the generated package metadata to confirm its repository URL points to the fork.

## Delivery

Commit the identity replacements on `codex/secure-credential-storage`, push them to PR #1, and perform one immediate thread-aware PR and CI-state check. Recurring monitoring remains disabled at the user's request.
