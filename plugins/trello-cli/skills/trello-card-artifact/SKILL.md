---
name: trello-card-artifact
description: Creates and updates an interactive side-pane Markdown artifact for a specific Trello card so the user can view all card details, select any field or line, and leave comments to request edits. Activate when the user is working on, viewing, reviewing, or editing a particular Trello card.
---
# Trello Card Side Artifact

Display any Trello card as a clean, editable-by-comment side-pane Markdown artifact, and apply the user's review comments back to Trello via `trello-cli`.

## When to Activate

- The user asks to view, inspect, review, or edit a specific Trello card.
- The conversation focuses on a particular Trello card and a visual side-pane representation helps the user review fields and request changes.

## Language Adaptation Rule

- **UI Chrome in the User's Language:** Write all template headings, table headers, field names (`Properties`, `Field`, `Current`, `Available / Notes`, `Title`, `List`, `Labels`, `Members`, `Start Date`, `Due Date`, `Cover`, `Description`, `Checklists`, `Comments`, `Attachments`, `Card Metadata`), status/empty placeholders (`Not set`, `empty`, `No checklists.`, etc.), and the `> [!TIP]` instruction in **the language the user is speaking in the conversation**.
- **Card Content Untouched:** Keep all actual Trello data (card title, list names, label names, description text, checklist items, comment text) in its **original language** from Trello unless the user explicitly asks to translate it.

## Workflow

### 1. Gather Card & Board Context

Use `trello-cli` (see the `trello-cli` skill for auth and command details) to fetch:
1. Card details: `trello-cli --get-card <card-id>`
2. Comments: `trello-cli --get-comments <card-id>`
3. Checklists: `trello-cli --get-checklists <card-id>`
4. Attachments: `trello-cli --list-attachments <card-id>`
5. Board context (for available options):
   - `trello-cli --get-board <board-id>`
   - `trello-cli --get-lists <board-id>`
   - `trello-cli --get-labels <board-id>`
   - `trello-cli --get-members <board-id>`

### 2. Create the Side Artifact

- **In Antigravity** (when `write_to_file` with `ArtifactMetadata` is available): write a Markdown file in the conversation's artifact directory (e.g., `trello_card_edit.md`) with `ArtifactMetadata.UserFacing: true`, `ArtifactMetadata.RequestFeedback: true`, and `Overwrite: true`.
- **In other runtimes (e.g., Claude Code)** where `write_to_file` / `ArtifactMetadata` is unavailable: write the same Markdown template to a temporary/local file using the runtime's native file-write tool (such as `Write`) or output it in chat, and invite the user to reply with the edits they want applied.

Follow this layout (localized to the user's language for all UI chrome):

```markdown
# <Card Title>

**Board:** <Board Name> · **List:** `<List Name>` · **Label:** `<Label Names>` · **Assignee:** `<Member Names>` · [Open in Trello ↗](<short-card-url>)

> [!TIP]
> Highlight any field, row, or line below and add a comment with your requested change. Submit your review when ready, and I will update the card in Trello.

## Properties

| Field | Current | Available / Notes |
| :--- | :--- | :--- |
| **Title** | <Card Title> | — |
| **List** | `<Current List>` | `<List 1>` · `<List 2>` · ... |
| **Labels** | `<Label>` *(<color>)* | `<Label 1>` · `<Label 2>` · ... |
| **Members** | `<Full Name>` (`@<username>`) | `<Member 1>` (`@<u1>`) · `<Member 2>` (`@<u2>`) |
| **Start Date** | <Date or _Not set_> | `YYYY-MM-DD` |
| **Due Date** | <Date or _Not set_> *(<complete/incomplete>)* | `YYYY-MM-DD` · mark complete |
| **Cover** | `<color>` *(<brightness>, <size>)* | Change color/size or clear |

## Description

<Card description formatted cleanly with blank lines between sections; if empty, show _(empty)_>

## Checklists

<Render each checklist as a subheading with `- [x]` / `- [ ]` items, or an empty-state prompt to comment here to add one.>

## Comments

<Render each comment with author, date, and body, or an empty-state prompt to comment here to post a new comment.>

## Attachments

<Render attachment names and links, or an empty-state prompt.>

<details>
<summary>Card Metadata (IDs)</summary>

- **Card ID:** `<card-id>`
- **Board ID:** `<board-id>`
- **List ID:** `<list-id>`
- **Full URL:** <full-card-url>

</details>
```

#### Formatting Rules
- Use a short Trello URL (`https://trello.com/c/<shortLink>`) in the top header; keep the long percent-encoded URL inside `<details>`.
- Avoid unnecessary `---` horizontal rules between sections.
- Ensure blank lines (`\n\n`) separate sub-labels and paragraphs in **Description** so Markdown does not collapse them onto one line.
- After creating or updating the artifact, point the user to it without re-summarizing its contents.

### 3. Apply User Comments & Refresh

When the user submits comments on the artifact:
1. Map each comment to the corresponding `trello-cli` command (`--update-card`, `--move-card`, `--add-card-label`, `--remove-card-label`, `--add-card-member`, `--remove-card-member`, `--set-start-date`, `--set-due-complete`, `--set-card-cover`, `--clear-card-cover`, `--add-comment`, `--create-checklist`, `--add-checklist-item`, `--update-checklist-item`, etc.).
2. Verify `ok: true` in the JSON output for every command.
3. Re-fetch the card state and overwrite the artifact with the updated values so the side pane stays in sync with Trello.