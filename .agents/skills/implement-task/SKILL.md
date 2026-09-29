---
name: implement-task
description: Implement the next Codex task for Personal Arena from docs/tasks/BOARD.md (or a named task file) - code, tests, report, no git. Use whenever asked to do a task, "làm task", or continue Codex work.
---

# Implement a task (Codex)

1. Read `AGENTS.md` (rules; §6 Core invariants are hard rules). **Never run git commands.**
2. Open `docs/tasks/BOARD.md`. Take the named task, or the first row with `Owner: Codex` and
   status `todo` whose "Depends on" tasks are `done`.
3. In the task file set `Status: doing`; update the board row.
4. Implement exactly the spec: only the listed files, exact names and signatures. If the spec
   is wrong or unclear, implement the most reasonable reading and write the question in Report.
5. Work on whatever branch the owner has checked out (normally `develop`). Add every listed
   test. Run `dotnet test CoreTests` (and `.venv-ml\Scripts\python -m pytest
   Trainer` for Python tasks). Fix until green with no warnings.
6. Fill the Report: changed files, test summary line (passed/failed counts), notes.
7. Set `Status: review` in the file and on the board. Stop. Claude reviews and commits.
8. If asked to continue, go to step 2 for the next task.
