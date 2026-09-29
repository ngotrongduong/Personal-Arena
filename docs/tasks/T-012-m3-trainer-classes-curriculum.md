# T-012: M3 Trainer — rules v3, per-class training, zombie-mix curriculum

- **Owner:** Codex
- **Status:** review
- **Milestone:** M3
- **Parallel OK with:** T-011 (C# Core only, no shared files)
- **Depends on:** none

> Claude (the orchestrator) has full decision authority; never ask the user anything; work until
> the task is done. Follow `AGENTS.md`. Do not run git commands. Only touch the files listed
> below. When a detail is not specified, pick the simplest option and write it in the Report.

## Goal

The Python trainer side can do three things:
- train **Warrior, Mage and Archer** as separate behaviors from the same Unity training build;
- start fresh runs for rules v3;
- run a curriculum that first raises the zombie count and then mixes in the new zombie types
  (Runner, Brute, Spitter).

The watcher exports brains for every behavior, and the training service reports the zombie mix.

## Files

- Edit: `Trainer/arena_trainer.py`, `Trainer/train_service.py`, `Trainer/export_brain.py`,
  `Trainer/training_history.py`, `Trainer/watch_ai.ps1`, `Trainer/config/warrior_ppo.yaml`,
  `Trainer/config/warrior_ppo_randomized.yaml`.
- Create: `Trainer/config/mage_ppo.yaml`, `Trainer/config/archer_ppo.yaml`.
- Edit and create tests in `Trainer/tests/`.
- Do not touch anything else. Leave `Trainer/runs/` alone: it holds real training data and must
  not be modified or deleted.

## Spec

### 1. Rules version

`arena_trainer.RULES_VERSION = 3`. The existing logic in `plan_run` / `newest_behavior_dir(...,
rules_version=...)` must therefore start a new run (`warrior-003`) instead of resuming
`warrior-002`, which is v2. Add or adjust a test that proves a v2 run is not resumed under v3.

### 2. Hero class forwarding (`arena_trainer.py`)

- New optional arg `--hero-class` with choices `warrior`, `mage`, `archer`.
- `build_command` passes it to Unity through `--env-args`, together with `--arena-agents` when that
  is set. `--env-args` takes the rest of the command line, so it must appear once, last:
  `... --env-args --arena-agents 32 --hero-class mage`.
- If neither arg is set, `--env-args` is not added.
- Unity side, done by Claude later: `TrainingArenaHost` reads `--hero-class <id>`, where the ids
  are the ones `DefaultDefs.HeroClass` accepts.

### 3. Training service (`train_service.py`)

- `--behavior` accepts `Warrior`, `Mage`, `Archer` (case-insensitive input, normalised to that
  capitalisation).
- The config defaults to `Trainer/config/<behavior lower>_ppo.yaml` when `--config` is not given.
- The trainer command gets `--hero-class <behavior lower>`.
- Run ids already use the `behavior.lower()` prefix. Check that `mage-001` and `archer-001` come
  out right.
- `Progress`:
  - Keep tracking `zombie_count` as today.
  - Also track the latest values of `runner_weight`, `brute_weight` and `spitter_weight`, parsed
    from the same "Parameter '…' is in lesson …" lines.
  - Expose the weights in the status JSON the viewer reads, as `"zombie_mix": {"walker": 1.0,
    "runner": x, "brute": y, "spitter": z}`. Walker is always 1.0. The weights default to 0.0
    until a line is seen.
  - Keep every existing status key unchanged, because the Unity viewer parses them.
- The status JSON also includes `"behavior": "<Behavior>"`.

### 4. Curricula (`config/*.yaml`)

`warrior_ppo.yaml` keeps its hyperparameters. Replace `environment_parameters` with:
- `zombie_count` lessons 1 → 2 → 4 → 8 → 16. The thresholds (measure reward, smoothed,
  `min_lesson_length: 1000`) are 15, 25, 40, 70. The v2 thresholds were passed too early.
- `runner_weight`: lesson `NoRunners` value 0.0 (threshold 150), then `Runners` value 0.5.
- `brute_weight`: lesson `NoBrutes` value 0.0 (threshold 170), then `Brutes` value 0.35.
- `spitter_weight`: lesson `NoSpitters` value 0.0 (threshold 190), then `Spitters` value 0.35.
- `arena_size`, `hp_mult`, `damage_mult`, `speed_mult` are unchanged.
- Add a comment:
  - ML-Agents advances each parameter independently on the same reward.
  - The mix thresholds are above the last zombie_count threshold, so the new types arrive once
    16 zombies are handled.
  - All thresholds are first estimates, to be tuned from real runs (see docs/TRAINING.md).

`mage_ppo.yaml` and `archer_ppo.yaml`:
- A copy of the new warrior file, with the behavior name `Mage` / `Archer` in `behaviors:` and in
  every `completion_criteria.behavior`.
- Lower every threshold to 80% of the warrior value, because those classes have less HP. Keep one
  decimal.

`warrior_ppo_randomized.yaml`: apply the same zombie_count thresholds and add the same three
weight parameters. Keep its randomized `arena_size` last lesson as it is.

Add a test that loads all four YAML files and checks:
- each has exactly one behavior;
- every `completion_criteria.behavior` matches it;
- all four env params exist;
- each curriculum's thresholds are strictly increasing.

### 5. Brain export (`export_brain.py`, `watch_ai.ps1`)

- `--behavior` accepts a name or `all`, and the default becomes `all`.
  - With `all`, the exporter discovers every behavior directory under `runs_dir/*/<Behavior>`
    that contains checkpoints, skipping `run_logs` and anything that is not a behavior folder.
  - It exports and writes history for each behavior found, both in one-shot mode and in
    `--watch` mode.
- `export_newest` and `write_newest_history` pick the newest run **of the current
  RULES_VERSION** for each behavior (pass `rules_version=arena_trainer.RULES_VERSION`). A
  brain for old rules must not become the newest `latest.brain` the viewer would load. Check
  the import direction and avoid a circular import. If `export_brain` cannot import
  `arena_trainer`, move `RULES_VERSION` / `run_rules_version` into a tiny shared module and
  re-export them from `arena_trainer` so existing imports keep working.
- `watch_ai.ps1` starts the exporter watching all behaviors.
- Keep the change minimal and keep its existing behaviour otherwise.

### 6. Training history (`training_history.py`)

Remove the implicit Warrior assumption: `build_history`/`write_history` take the behavior from
the caller, and the CLI `--behavior` works the same way as in the exporter (`all` by default).

## Tests (must add or update, in Trainer/tests)

- `build_command` with `hero_class` only, with `arena_agents` only, with both, and with neither.
  `--env-args` appears at most once and is last.
- `Progress` parses the three weights and reports `zombie_mix`; the defaults are 0.0.
- The default config resolves per behavior.
- `plan_run` for `Mage` with an empty runs dir gives `mage-001`.
- A v2 warrior run is not resumed when `RULES_VERSION` is 3.
- The export discovers several behaviors and ignores old-rules runs.
- The YAML curriculum checks from §4.

## Done when

- [x] `C:\PersonalArena\.venv-ml\Scripts\python.exe -m pytest C:\PersonalArena\Trainer` passes.
  Run it as one plain command.
- [x] No existing status key or CLI flag was removed.
- [x] The Report below is filled in.

## Report (filled by the implementer)

- Changed files: `Trainer/arena_trainer.py`, `Trainer/train_service.py`,
  `Trainer/export_brain.py`, `Trainer/training_history.py`, `Trainer/watch_ai.ps1`;
  all four files in `Trainer/config/` (created Mage/Archer configs); and Trainer tests
  (`conftest.py`, trainer/service/export/history tests, plus new `test_curricula.py`).
- Test result: `C:\PersonalArena\.venv-ml\Scripts\python.exe -m pytest C:\PersonalArena\Trainer`
  â€” 61 passed in 4.53s.
- Notes / open questions: Export and history discovery are strict to rules v3, so v1/v2 runs
  cannot replace a current `latest.brain`. Pytest uses ignored `results/pytest-tmp` because the
  coding sandbox cannot access the Windows user temp folder. No open questions.
