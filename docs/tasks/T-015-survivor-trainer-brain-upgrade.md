# T-015: M4A Trainer — schema versions, Survivor PPO config and reward curriculum

- **Owner:** Codex
- **Status:** doing
- **Milestone:** M4A
- **Parallel OK with:** T-014 (C# Core only, no shared files)
- **Depends on:** none (read `docs/tasks/T-014-survivor-core-sim.md` §9–§11 for the observation
  and rewards; do not edit it)

> Claude (the orchestrator) has full decision authority; never ask the user anything; work until
> the task is done. Follow `AGENTS.md`. Do not run git commands. Only touch the files listed
> below. When a detail is not specified, pick the simplest option and write it in the Report.

## Goal

The game switches to a Vampire Survivors–style mode (`docs/GDD.md`, decisions D-026..D-030).
M4A is a vertical slice for the Warrior. Its brain sees observation schema v4 (**2264 floats**,
already normalized to [−1, 1] by the game) and has 3 action branches (move 9, active skill 5,
level-up pick 5). After this task the Python side:

1. versions runs by **observation schema** (`schema_version.txt`) instead of rules version, so a
   rules or balance change with the same schema **resumes** the same run instead of restarting;
2. trains the Warrior Survivor behavior with a new PPO config and a **reward-measured**
   curriculum on run length (3 → 6 → 10 → 15 min);
3. names new Survivor runs `warrior-s001`, `warrior-s002`, …;
4. exports the new network to the `.brain` format.

Out of scope (M4B task T-019): `brain_upgrade.py` (widening a checkpoint to a bigger schema), the
schema JSON description, build-conditioned training, the regression curriculum, champion /
challenger. Do not start them here.

## Files

- Edit: `Trainer/arena_trainer.py`, `Trainer/train_service.py`, `Trainer/export_brain.py`,
  `Trainer/training_history.py`, `Trainer/README.md`.
- Create: `Trainer/config/warrior_survivor_ppo.yaml`.
- Edit and create tests in `Trainer/tests/`.
- Leave the old `warrior_ppo*.yaml`, `mage_ppo.yaml`, `archer_ppo.yaml` in place (T-017 removes
  them). Do not touch `Trainer/runs/` (real training data, must never be modified or deleted),
  `Unity/`, `CoreTests/`, `Tools/` or `docs/` (except this file's Report).

## Spec

### 1. Schema versions (replace rules versions)

- `arena_trainer.SCHEMA_VERSION = 4`, `SCHEMA_FILE = "schema_version.txt"`.
- `run_schema_version(run_dir) -> int`: reads `schema_version.txt`; if it is missing but
  `rules_version.txt` exists, returns that rules version (1..3; these are the old arena
  observations, all incompatible with v4); if both are missing returns 1.
- `write_schema_version(run_dir)` writes `SCHEMA_VERSION`.
- Replace every use of `RULES_VERSION` / `run_rules_version` / `write_rules_version` in
  `train_service.py`, `export_brain.py` and `training_history.py` (and the tests) with the schema
  functions. Keep the public status/history keys working: where a dict exposes
  `"rules_version"`, also expose `"schema_version"` (keep the old key with the same value for the
  Unity HUD until T-016 switches).
- Remove `RULES_VERSION` and the rules helpers once nothing uses them.

### 2. Run naming and planning (`train_service.plan_run`)

- New Survivor runs are named `<behavior lower>-s<NNN>` (e.g. `warrior-s001`), numbered after
  the highest existing `-sNNN` for that behavior. Old `warrior-NNN` runs are left alone and are
  never resumed (their schema is < 4).
- Planning order for a behavior:
  1. newest run with schema == `SCHEMA_VERSION` and a checkpoint → **resume** (as today);
  2. else **new**.
- `_run_locked` writes `schema_version.txt` into the run dir (where it writes the rules file
  today).
- Extend the existing tests: a rules-v3 run (`rules_version.txt` = 3) is never resumed; a v4 run
  is resumed; the next new run after `warrior-s002` is `warrior-s003`; old `warrior-011` does not
  affect the `-s` numbering.

### 3. Survivor PPO config (`Trainer/config/warrior_survivor_ppo.yaml`)

```yaml
behaviors:
  Warrior:
    trainer_type: ppo
    hyperparameters:
      batch_size: 4096
      buffer_size: 81920
      learning_rate: 3.0e-4
      learning_rate_schedule: constant
      beta: 5.0e-3
      epsilon: 0.2
      lambd: 0.95
      num_epoch: 3
    network_settings:
      normalize: false
      hidden_units: 512
      num_layers: 3
    reward_signals:
      extrinsic:
        gamma: 0.995
        strength: 1.0
    time_horizon: 256
    max_steps: 1.0e8
    summary_freq: 50000
    checkpoint_interval: 1000000
    keep_checkpoints: 10
```

- `constant` learning rate because the brain keeps learning across updates; a linear schedule
  would decay to 0 at `max_steps`.
- `normalize: false` because the game normalizes every value inside the schema (T-014 §9); this
  also keeps later schema upgrades simple (no running statistics to widen).
- mlagents already builds one shared body with a separate head per discrete branch; nothing to
  configure for that.

`environment_parameters` (names are read by the Unity agent in T-016):
- `run_seconds`: a curriculum with `measure: reward`, `signal_smoothing: true`,
  `min_lesson_length: 200`:

  | Lesson | Value | Advance when the smoothed mean episode reward ≥ |
  |---|---|---|
  | `survive-3` | 180 | 5.0 |
  | `survive-6` | 360 | 6.5 |
  | `survive-10` | 600 | 8.5 |
  | `full-run` | 900 | (last lesson) |

  Why these numbers (rewards from T-014 §11: TimeUp +5, Death −5, 0.01 per second survived,
  0.05 per level, −1 per MaxHp lost): an episode that survives 180 s earns about
  5 + 1.8 + ~0.4 − HP lost ≈ 6.5, a death earns about −3, so a mean of 5.0 needs most runs to
  survive. The same reasoning gives ~9 for 360 s and ~12 for 600 s. Write this reasoning as a
  YAML comment above the curriculum.
- `tier_min`: 1.0 and `tier_max`: 1.0 (constants; M4B randomizes the tier);
- `build_level_max`: 0.0 (constant; M4B turns on random builds);
- `own_build_share`: 0.0 (constant; M5 raises it for per-character brains).

Check that `measure: reward` curricula with these keys are valid in mlagents 1.1.0 (read
`C:\PersonalArena\.venv-ml\Lib\site-packages\mlagents\trainers\settings.py`) and add a test that
loads the YAML with mlagents' `RunOptions`/settings parser, like the existing curriculum tests
do, and checks the lesson values and thresholds.

### 4. `arena_trainer.py`

- `--hero-class warrior` now defaults to `Trainer/config/warrior_survivor_ppo.yaml`. For
  `mage` / `archer`, if `<class>_survivor_ppo.yaml` does not exist, exit with a clear message
  ("Mage/Archer Survivor brains arrive in M7") instead of falling back to the old config.
- Keep all existing flags. `--env-args` stays last.

### 5. `export_brain.py`

- Must export the new network (3 × 512 hidden, 3 discrete branches 9/5/5, 2264 inputs) to the
  `.brain` format that `PolicyBrain.cs` reads. Check that nothing assumes 2 layers, the old branch
  sizes, or 736/811/883 inputs. Add a test exporting a fake 3-layer, 3-branch checkpoint with
  2264 inputs and reading back the header (inputs, layers, branch sizes).
- Discovery uses schema versions (§1): only current-schema runs replace `latest.brain`.

### 6. Training history

- `training_history.py` keeps reading tags that start with `Environment/` or `Arena/`. The Unity
  side (T-016) will record the new Survivor stats under the `Arena/` prefix (e.g.
  `Arena/SurvivedSeconds`, `Arena/Level`, `Arena/Gold`, `Arena/DeathCause/Surrounded`), so no
  filter change is needed; add a test that such tags are kept.
- Where history points carry a rules version, carry the schema version too (§1).

### 7. README

Update `Trainer/README.md`: schema versions (same schema → resume, new schema → new run until
M4B's upgrade tool), run naming, the Survivor config and its reward curriculum, in a few short
sections.

## Done when

- [ ] `C:\PersonalArena\.venv-ml\Scripts\python.exe -m pytest <this checkout>\Trainer` passes
  (run it as one plain command; the venv lives in the main checkout even if you work in another
  folder).
- [ ] No existing status key or CLI flag was removed (only added).
- [ ] The Report below is filled in.

## Report (filled by the implementer)

- Changed files:
- Test result:
- Notes / open questions:
