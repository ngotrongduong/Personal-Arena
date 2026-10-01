# T-031: M7 trainer — Mage and Archer Survivor brains

- **Owner:** Claude (orchestrator, subagent `rl-trainer`)
- **Status:** done
- **Milestone:** M7
- **Depends on:** T-030 (Mage/Archer kits in Core), T-028 (lineage)

## Goal

Train a Mage or an Archer exactly like the Warrior: same PPO, network, curricula, champion
evaluation and brain history, each class in its own runs (`<class>-sNNN`).

## Work

- `Trainer/config/mage_survivor_ppo.yaml`, `archer_survivor_ppo.yaml`: copies of the Warrior config
  keyed by `Mage` / `Archer` (curriculum behavior fields included).
- `train_service`: the M7 gate is gone. Config, default run `<class>-s001`, run numbering, resume,
  schema upgrade, export, champions, lineage and history all follow `--behavior`.
  - A requested run of another class is never forced.
  - An older-schema run (pre-Survivor `mage-001`) is never an upgrade source and never overwritten:
    it gets the class's next run id, even without a checkpoint.
  - A config/behavior mismatch fails before any run folder is created.
- `arena_trainer`: `HERO_BEHAVIORS`, `behavior_name` / `run_class`; the class comes from an explicit
  single-behavior config, and a mismatch is an argparse error.
- `champion`, `brain_lineage`, `brain_upgrade` CLIs accept `--behavior` in any case;
  `brain_upgrade` infers it from the source run name.
- `Trainer/README.md` documents per-class runs.

## Report

- Tests: `Trainer/tests/test_m7_classes.py` plus updated curricula, service and trainer tests.
- Reviewer fix: a legacy run without `checkpoint.pt` is no longer forced over (a folder holding
  only a stale schema marker is still reused).
- Trainer suite: 185 passed.
