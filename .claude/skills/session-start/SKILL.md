---
name: session-start
description: Rebuild context at the start of a Personal Arena session - read STATUS, board and recent git history, check for finished Codex work, then pick and state the next task. Use as the first step of every session.
---

# Session start

1. Sync: `git fetch origin && git status -sb`. If `main` is behind, `git pull --ff-only`.
   If there are uncommitted changes, they are probably Codex work: note them, do not discard.
2. Read `docs/STATUS.md` fully, then `docs/tasks/BOARD.md`.
3. `git log --oneline -15` — anything newer than the last STATUS session log entry means
   someone worked without updating STATUS; reconcile it now.
4. Look for tasks in `review` (Codex finished): for each, read the task Report and the
   uncommitted diff, spawn the `reviewer` subagent, then commit it on a branch and merge (see
   `/codex-task`, "Review" part).
5. Check the environment: is the owner's computer linked (remote-device tools present)?
   Is `dotnet` available? This decides which tasks you can do yourself.
6. Tell the owner in Vietnamese, in 3–6 lines: where the project is, what you will do now,
   and anything they must do (Owner tasks). Then start the first task you can do.
