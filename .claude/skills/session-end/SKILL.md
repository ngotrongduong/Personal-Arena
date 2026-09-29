---
name: session-end
description: Close a Personal Arena session so the next one starts exactly where this one stopped - update STATUS, BOARD and DECISIONS, commit, merge and push. Use before stopping work or when the owner says they are done.
---

# Session end

1. `docs/STATUS.md` (Vietnamese):
   - "Đang ở đâu": milestone and what now exists.
   - Milestone checklist: update states.
   - "Việc tiếp theo": ordered, each line names an owner (Claude / Codex / Owner) and a task id.
   - "Vướng mắc": blockers and open questions.
   - "Nhật ký phiên": add an entry on top — date, who, where (cloud/PC), 3–6 bullets of what
     changed. Keep only the last ~10 entries; move older ones to `docs/archive/SESSIONS.md`.
   - Set "Cập nhật lần cuối".
2. `docs/tasks/BOARD.md`: every task's status matches its file. New work discovered → new task
   file from `TEMPLATE.md` + board row.
3. `docs/DECISIONS.md`: add any choice made this session that later work depends on.
4. `docs/TRAINING.md`: log any real training run.
5. Git: commit on the current branch (`docs(status): session YYYY-MM-DD` if only docs), merge
   `--no-ff` into `main` if the branch is done and CI-safe, `git push origin main <branch>`.
6. Tell the owner in 2–4 Vietnamese lines what was done and what is next.
