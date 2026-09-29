---
name: train-and-report
description: Run an ML-Agents training run for Personal Arena on this PC and log the result. Use when asked to train a hero class or check a training run.
---

# Train and report (Codex, on the PC)

1. Read `docs/TRAINING.md` (commands, metrics table, run log) and the config in
   `Trainer/config/`.
2. Use the venv: `.venv-ml\Scripts\mlagents-learn <config> --run-id=<class>-<NNN> ...`
   (next free number from the run log). Never pip-install into another environment.
3. When it ends (or is stopped), read `results/<run-id>/run_logs/timers.json` and the last
   summary lines: final mean reward, steps, curriculum lesson reached.
4. Add one row to the run log table in `docs/TRAINING.md`.
5. Never commit or move files under `results/`. No git commands.
