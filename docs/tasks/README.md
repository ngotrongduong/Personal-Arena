# Tasks

One file per task: `T-<3 digits>-<slug>.md`, made from `TEMPLATE.md`. The board
(`BOARD.md`) is the index; the task file is the spec and the report.

## Status flow

`todo` → `doing` → `review` → `done` (or `blocked`, with the reason in the file).

## Rules

- A task fits in one sitting: one clear outcome, ≤ ~300 lines changed, its own tests.
- A task lists the exact files it may touch. Two tasks marked "parallel OK" must not share files.
- **Codex:** implement, run `dotnet test CoreTests` (and `pytest` for Trainer), fill "Report",
  set `review`. No git commands.
- **Claude:** reviews (spawn `reviewer` for anything touching Core rules), commits on a
  branch, merges to `main`, sets `done`, updates `docs/STATUS.md`.
- **Owner tasks** give exact click-by-click or command steps, because the owner may be doing
  them alone.

## Asking Codex to work

On the PC, in the repo folder, run Codex and say for example:

> Làm task tiếp theo của Codex trong docs/tasks/BOARD.md.

or name it: "Implement docs/tasks/T-001-rng.md". Codex has the `implement-task` skill in
`.agents/skills/` that walks it through the flow above.
