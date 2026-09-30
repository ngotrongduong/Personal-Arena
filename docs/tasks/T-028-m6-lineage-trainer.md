# T-028: M6 brain lineage — trainer side (versions, branches, fork)

- **Owner:** Claude (orchestrator)
- **Status:** done
- **Milestone:** M6 (D-037)
- **Depends on:** champion system (T-020), `brain_upgrade.py` run layout

## Goal

Keep old brains so the owner can watch them again, compare them, and continue training from any
of them on a new branch, without ever changing or deleting an existing run.

## Store (contract with the viewer, T-029)

Everything lives in `Trainer/runs/champions/<Behavior>/lineage/` (every run scanner already skips
`champions`).

- `versions/<run_id>-<step>/`
  - `brain.brain` — the exported policy (always present).
  - `checkpoint.pt` — the ML-Agents checkpoint (optional; needed to fork).
  - `training_status.json` — the run's curriculum state at snapshot time, checkpoint lists
    stripped (optional).
  - `version.json` — written last, so a folder without it is ignored:

    ```json
    {"id": "warrior-s001-96999889", "run_id": "warrior-s001", "step": 96999889,
     "schema_version": 4, "source": "eval|history|manual", "created_at": "<iso>",
     "evaluated": true, "score": 1530.0, "champion": true, "passes_m4a": true,
     "summary": {...SurvivorEval summary...}, "behavior": {...}, "evaluated_at": "<iso>",
     "brain_file": "brain.brain", "has_checkpoint": true}
    ```

    `champion` = this brain became the champion when it was evaluated. Unevaluated versions have
    `evaluated: false`, `score: 0` and no `summary`/`behavior`.
- `branches.json` (written only by Python):
  `{"branches": [{"run_id": "warrior-s002", "parent_run": "warrior-s001", "parent_step": 96999889,
  "parent_version": "warrior-s001-96999889", "created_at": "<iso>"}]}`. A run that is not listed
  is a root branch.
- `labels.json` (written only by the viewer; Python only reads it):
  `{"versions": [{"id": "...", "name": "...", "pinned": true}], "branches": [{"run_id": "...",
  "name": "..."}]}`.

## `Trainer/brain_lineage.py`

No torch import at module level (only `snapshot` needs it, through `export_brain`).

- `sync(results_dir, behavior)`: import every `champions/<Behavior>/history/<id>.brain + .json`
  that is not a version yet (`source: "history"`, `champion: true`); copy the run's numbered
  checkpoint for that step when it still exists. Idempotent.
- `record_evaluation(results_dir, behavior, run_id, step, brain, checkpoint, line)`: called by
  `champion.evaluate_latest` after every evaluation (before a losing candidate is deleted). Copies
  the brain, the `.pt` and the run's `training_status.json`, writes `version.json` from the
  evaluation line (`champion` = `line["won"]`), then `prune`. Never raises into the evaluator.
- `snapshot(results_dir, behavior, run_id, evaluate)`: the run's newest numbered checkpoint that
  is at least 20 s old (a newer one may still be written); exports the brain from it (falls back
  to the previous checkpoint when a file cannot be read), `source: "manual"`. With `evaluate`, runs
  the SurvivorEval once (no champion promotion, no `evaluations.jsonl` line).
- `fork(results_dir, behavior, version_id=None, run_id=None)`: `run_id` means "clone the newest
  state of that branch" (snapshot without evaluation, then fork it). Requires a version with
  `checkpoint.pt` and the current schema. Writes `.<new>.partial/` (numbered `.pt`,
  `checkpoint.pt`, `schema_version.txt`, `forked_from.json`, `run_logs/training_status.json`),
  `os.replace` into `runs/<new>` (`next_run_id`), then appends `branches.json`.
- `prune(results_dir, behavior)`: protected = champion, pinned, manual, or a parent of any
  branch. Per run: `.pt` kept for protected versions and the 3 newest others; other `.pt` files
  are removed and `has_checkpoint` rewritten. Unprotected versions older than the 10 newest of the
  run keep only the best-scored one per 10M-step bucket; the rest are removed.
- CLI: `python Trainer/brain_lineage.py [--results-dir R] [--behavior B] sync | snapshot --run-id R
  [--no-evaluate] | fork (--version ID | --run-id R)`. The last stdout line is JSON:
  `{"ok": true, ...}` or `{"ok": false, "error": "<Vietnamese text for the owner>"}` (exit 1).

## Integration

- `champion.evaluate_latest` calls `record_evaluation` (lazy import, wrapped).
- `train_service` calls `sync` once at start (wrapped, logged).

## Tests (`Trainer/tests/test_brain_lineage.py`)

sync imports history once; record keeps the loser's brain; fork creates a resumable run (numbered
`.pt`, `checkpoint.pt`, schema, stripped status) and a `branches.json` entry, never touches the
source run, refuses a version without `.pt` or with another schema, and leaves no partial folder
on failure; clone by run id; prune keeps protected `.pt`, removes old ones and thins old versions;
the CLI prints JSON on success and on error.

## Report

- `Trainer/brain_lineage.py` as specified, plus a `prune` CLI command. `fork` retries the final
  folder rename for a moment when Windows (a scanner or the indexer) still holds the new folder.
- `champion.evaluate_latest` records every finished evaluation (winner or loser) before the losing
  candidate is deleted; `train_service` runs `sync` once after planning the run. Both are wrapped
  so a lineage failure is only logged.
- `Trainer/tests/test_brain_lineage.py`: 13 tests; the whole trainer suite passes (131).
- Smoke test on a scratch copy of the real run: `sync` imported the 8 champion history brains (view
  only: their numbered `.pt` files were already pruned by ML-Agents), `snapshot --no-evaluate`
  exported step 106,093,272 with its checkpoint, `fork --run-id warrior-s001` created a resumable
  `warrior-s002`, and a bad `fork` printed the Vietnamese error with exit 1.
- The running training imports the old `champion.py`, so evaluated versions with checkpoints start
  appearing after the next TRAIN. Snapshot and clone work at once.
- **Reviewer fixes (2026-10-01):**
  - `prune` removes nothing while `labels.json` or `branches.json` exists but cannot be read.
  - Run, version and behavior names are checked (`check_id`: letters, digits, `_.-`, at most 64, not `champions`, no leading `.`).
  - A version is built in `.<id>.partial` and moved into place whole, so a failed copy leaves no half folder.
  - JSON temp names are unique per writer.
  - When scoring fails, a snapshot returns `ok` with a `warning`. So does a fork whose `branches.json` cannot be written.
  - Forked `.pt` files keep their times (`copy2`), so a fork never looks like the newest training.
  - The folder-rename retry moved to `arena_trainer.replace_directory`. `brain_upgrade` now uses it too, which fixes a rare Windows `PermissionError` in its test.
  - Trainer suite: 145 passed.
