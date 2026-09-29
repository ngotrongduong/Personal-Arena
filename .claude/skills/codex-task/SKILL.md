---
name: codex-task
description: Split work for Codex and bring it back - write precise task files in docs/tasks for Codex to implement, then review and commit what Codex produced. Use when planning work that can run in parallel with Claude, or when a task is in review.
---

# Codex task

Codex (on the owner's PC) implements; it never runs git (AGENTS.md §3). Claude writes specs,
reviews, commits.

## Write tasks

1. Pick work that is self-contained: Core types + tests, trainer Python + pytest, pure
   refactors. Keep Unity scene/prefab work and git for Claude.
2. Copy `docs/tasks/TEMPLATE.md` → `docs/tasks/T-<next>-<slug>.md`. Fill:
   - exact files to create/edit (disjoint from other open tasks if marked parallel);
   - exact public API (C# signatures) and behaviour, including edge cases and exceptions;
   - named tests with the values they assert;
   - reference data when correctness is subtle (e.g. published test vectors — verify them first).
3. Add a row to `docs/tasks/BOARD.md` with `Owner: Codex`, `todo`, parallel info.
4. Commit and push so Codex sees the task after `git pull` on the PC.
5. Tell the owner the one line to give Codex: "Làm task tiếp theo của Codex trong docs/tasks/BOARD.md."

## Review

1. Codex's work arrives as uncommitted changes on the PC (or pushed by the owner). Read the
   task's Report.
2. Spawn the `reviewer` subagent on the diff + task file.
3. Fix small issues yourself; for large ones set the task back to `todo` with notes under Report.
4. Branch `feature/mN-<slug>` off `develop`, commit `feat(<area>): <task title> (T-xxx)`,
   merge `--no-ff` into `develop`, push. Set the task and board to `done`; update STATUS.
