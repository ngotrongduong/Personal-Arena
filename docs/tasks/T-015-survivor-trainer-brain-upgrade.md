# T-015: M4 Trainer — schema versions, brain upgrade, Survivor PPO config and curriculum

- **Owner:** Codex
- **Status:** doing
- **Milestone:** M4
- **Parallel OK with:** T-014 (C# Core only, no shared files)
- **Depends on:** none (read `docs/tasks/T-014-survivor-core-sim.md` §9 for the observation
  layout; do not edit it)

> Claude (the orchestrator) has full decision authority; never ask the user anything; work until
> the task is done. Follow `AGENTS.md`. Do not run git commands. Only touch the files listed
> below. When a detail is not specified, pick the simplest option and write it in the Report.

## Goal

The game switches to a Vampire Survivors–style mode (`docs/GDD.md`, decisions D-026..D-029). Its
brain sees a new observation (schema v4, 2152 floats) and has 3 action branches (move 9,
active skill 5, level-up pick 5). The owner also wants brains that **keep learning across game
updates** instead of restarting. After this task the Python side:

1. versions runs by **observation schema** (`schema_version.txt`) instead of rules version, so a
   rules change with the same schema resumes the same run;
2. has `Trainer/brain_upgrade.py`, which widens a checkpoint to a newer schema (new inputs get
   zero weights, new action outputs get small weights) so an upgraded brain gives **identical
   logits** on old observations and then keeps learning;
3. trains the Warrior Survivor behavior with a new PPO config and curriculum
   (run length 3 → 6 → 10 → 15 min, difficulty tier and random build level growing);
4. names new Survivor runs `warrior-s001`, `warrior-s002`, … .

## Files

- Edit: `Trainer/arena_trainer.py`, `Trainer/train_service.py`, `Trainer/export_brain.py`,
  `Trainer/training_history.py`, `Trainer/README.md`.
- Create: `Trainer/brain_upgrade.py`, `Trainer/schemas/obs_v4.json`,
  `Trainer/config/warrior_survivor_ppo.yaml`.
- Edit and create tests in `Trainer/tests/`.
- Leave the old `warrior_ppo*.yaml`, `mage_ppo.yaml`, `archer_ppo.yaml` in place (T-017 removes
  them). Do not touch `Trainer/runs/` (real training data, must never be modified or deleted),
  `Unity/`, `CoreTests/` or `docs/` (except this file's Report).

## Spec

### 1. Schema versions (replaces rules versions)

- `arena_trainer.SCHEMA_VERSION = 4`, `SCHEMA_FILE = "schema_version.txt"`.
- `run_schema_version(run_dir) -> int`: reads `schema_version.txt`; if it is missing but
  `rules_version.txt` exists, returns that rules version (1..3; these are the old arena
  observations, all incompatible with v4); if both are missing returns 1.
- `write_schema_version(run_dir)` writes `SCHEMA_VERSION`.
- Replace every use of `RULES_VERSION` / `run_rules_version` / `write_rules_version` in
  `train_service.py`, `export_brain.py` and `training_history.py` with the schema functions.
  Keep the public status/history keys working: where a dict exposes `"rules_version"`, also
  expose `"schema_version"` (keep the old key with the same value for the Unity HUD until T-016
  switches).
- Remove `RULES_VERSION` and the rules helpers once nothing uses them.

### 2. Run naming and planning (`train_service.plan_run`)

- New Survivor runs are named `<behavior lower>-s<NNN>` (e.g. `warrior-s001`), numbered after
  the highest existing `-sNNN` for that behavior. Old `warrior-NNN` runs are left alone.
- Planning order for a behavior:
  1. newest run with schema == `SCHEMA_VERSION` and a checkpoint → **resume** (as today);
  2. else newest run with `4 ≤ schema < SCHEMA_VERSION` whose schema JSON and the current schema
     JSON both exist in `Trainer/schemas/` → **upgrade**: call `brain_upgrade.upgrade_run(...)` to
     write the widened checkpoint into a new seed run `<new_id>-seed/<Behavior>/checkpoint.pt`,
     then start `<new_id>` with `--initialize-from <new_id>-seed`. Add mode `"upgrade"` to
     `RunPlan` and report it in the status like the other modes;
  3. else **new**.
- `_run_locked` writes `schema_version.txt` into the run dir (where it writes the rules file
  today).
- Extend the existing tests: a rules-v3 run (`rules_version.txt` = 3) is never resumed; a v4 run
  is resumed; a fake v4 run is upgraded when `SCHEMA_VERSION` is monkeypatched to 5 with a fake
  `obs_v5.json`.

### 3. Schema description (`Trainer/schemas/obs_v4.json`)

Machine-readable layout of observation v4, exactly as in T-014 §9, in this shape:

```json
{
  "schema_version": 4,
  "size": 2152,
  "blocks": [
    {"name": "self", "repeat": 1, "fields": ["hp_ratio", "max_hp", "energy", "..."]},
    {"name": "inventory", "repeat": 1, "fields": ["item_0", "...", "item_63"]},
    {"name": "offers", "repeat": 4, "fields": ["item_0", "...", "item_63", "next_level", "valid"]},
    {"name": "rays", "repeat": 72, "fields": ["solid_none", "solid_wall", "solid_enemy_0", "...",
      "solid_enemy_7", "solid_projectile", "solid_reserved_0", "solid_distance", "solid_elite",
      "solid_windup", "solid_stunned", "pickup_none", "pickup_gem", "pickup_gold", "pickup_meat",
      "pickup_chest", "pickup_magnet", "pickup_reserved_0", "pickup_distance"]},
    {"name": "density", "repeat": 1, "fields": ["r0_s0_enemies", "r0_s0_xp", "..."]}
  ],
  "actions": [9, 5, 5]
}
```

- Every field name is unique inside its block and **stable forever**: a future schema keeps a
  field's name when it keeps its meaning, and adds new names for new values. Reserved slots get
  names such as `stat_13`, `reserved_13`, `solid_enemy_7`.
- The offset of `(block, repeat index r, field f)` is the sum of the previous blocks' sizes
  plus `r × len(fields) + f`. Blocks are laid out in list order. The total must equal `size`.
- Write the self fields in T-014 §9 order with readable names (`cooldown_0..3`, `ready_0..3`,
  `wall_px`, `wall_nx`, `wall_py`, `wall_ny`, `stat_0..15`, …) and the density fields in index
  order `r{ring}_s{sector}_{enemies|xp}`.
- `brain_upgrade.load_schema(version) -> Schema` with `size`, `actions` and
  `offset(block, r, field)`.
- Test: `obs_v4.json` loads, has size 2152, unique names, and a few spot offsets match T-014 §9
  (`self.awaiting_pick` = 24, `inventory.item_0` = 48, `offers[1].valid` = 112 + 66 + 65,
  `rays[1].pickup_none` = 376 + 24 + 16, `density.r2_s7_xp` = 2151).

### 4. `Trainer/brain_upgrade.py`

First read how mlagents 1.1.0 stores checkpoints: the installed package is under
`C:\PersonalArena\.venv-ml\Lib\site-packages\mlagents\trainers\` (look at
`model_saver/torch_model_saver.py`, `torch_entities/networks.py`, `torch_entities/encoders.py`,
`torch_entities/action_model.py`, `torch_entities/distributions.py`). Also read how
`export_brain.py` walks the checkpoint today; reuse its knowledge of the tensor names.

- `upgrade_checkpoint(src: Path, dst: Path, old: Schema, new: Schema) -> UpgradeReport`:
  - loads `checkpoint.pt` (`torch.load(..., map_location="cpu")`);
  - for every **input layer** that consumes the vector observation (the policy's first encoder
    linear layer and the critic's): builds a new weight matrix `[out, new.size]` filled with 0 and
    copies each old column to the new offset of the same `(block, r, field)`; old fields that no
    longer exist are dropped (listed in the report); bias unchanged;
  - handles a normalizer if present (the configs use `normalize: false`, but if running
    mean/variance tensors exist, widen them with mean 0 / variance 1 for new inputs);
  - for every **discrete action head** whose branch grew (`new.actions[i] > old.actions[i]`):
    appends rows initialised from `normal(0, 0.01)` with a fixed torch seed and bias 0; shrinking
    branches is an error;
  - drops the optimizer state (so Adam restarts) — check what mlagents does when the optimizer
    entry is missing or mismatched with `--initialize-from`, and make sure training starts
    without crashing (keep a fresh-but-valid entry if mlagents requires one);
  - resets the step counter in the saved dict if one exists;
  - hidden sizes are never changed; raise a clear error if the checkpoint layout is not
    recognised.
- `upgrade_run(runs_dir, src_run_id, dst_seed_run_id, behavior, old_version, new_version)`:
  finds the source `checkpoint.pt`, writes `runs/<dst>/<behavior>/checkpoint.pt`, writes
  `schema_version.txt` = new version into the seed run, and writes `upgrade.json` (source run,
  versions, dropped fields, added inputs) next to it.
- CLI: `python Trainer/brain_upgrade.py --runs-dir … --from-run … --to-run … --behavior Warrior
  --from-schema 4 --to-schema 5`.
- **Tests** (the core promise):
  - build a tiny fake mlagents-shaped checkpoint (or a real `SimpleActor`/critic from the
    installed mlagents with a tiny obs size, whichever is simpler and robust) for a fake schema
    v4-mini, upgrade it to a fake v5-mini that inserts new fields in the middle of a block, adds
    a block and grows one action branch; run both networks on random old observations mapped into
    the new layout (new fields random too) and assert the old logits and the value estimate are
    **identical** (within 1e-6), and the new action rows exist;
  - dropping a field is reported; shrinking a branch raises.

### 5. Survivor PPO config (`Trainer/config/warrior_survivor_ppo.yaml`)

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

(`constant` learning rate because the brain keeps learning across updates; a linear schedule
would decay to 0 at `max_steps`.)

`environment_parameters` (names are read by the Unity agent in T-016; all curricula use
`measure: progress` so no reward threshold has to be guessed; tune later from real runs):
- `run_seconds`: 180 until progress 0.08, 360 until 0.2, 600 until 0.4, then 900;
- `tier_min`: 1.0 (constant);
- `tier_max`: 1 until progress 0.3, 3 until 0.5, 6 until 0.7, then 10;
- `build_level_max`: 10 until progress 0.15, 30 until 0.35, then 50;
- `own_build_share`: 0.0 (constant; M5 raises it for per-character brains).

Check that `progress` curricula are valid in mlagents 1.1.0 (read
`mlagents/trainers/settings.py`) and add a test that loads the YAML with mlagents'
`RunOptions`/settings parser, like the existing curriculum tests do.

### 6. `arena_trainer.py`

- `--hero-class warrior` now defaults to `Trainer/config/warrior_survivor_ppo.yaml`. For
  `mage` / `archer`, if `<class>_survivor_ppo.yaml` does not exist, exit with a clear message
  ("Mage/Archer Survivor brains arrive in M6") instead of falling back to the old config.
- Keep all existing flags. `--env-args` stays last.

### 7. `export_brain.py`

- Must export the new network (3 × 512 hidden, 3 discrete branches 9/5/5, 2152 inputs) to the
  `.brain` format that `PolicyBrain.cs` reads. Check that nothing assumes 2 layers, 3 branches
  of the old sizes, or 736/811/883 inputs. Add a test exporting a fake 3-layer, 3-branch
  checkpoint and reading back the header (inputs, layers, branch sizes).
- Discovery uses schema versions (§1): only current-schema runs replace `latest.brain`.

### 8. README

Update `Trainer/README.md`: schema versions, run naming, the upgrade flow, and the Survivor
config/curriculum in a few short sections.

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
