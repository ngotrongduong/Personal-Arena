---
name: mlagents-training
description: Configure, run and diagnose ML-Agents training for Personal Arena heroes - PPO YAML, curriculum, environment parameters, headless builds, TensorBoard metrics, fine-tuning per owned hero. Use for any training or trainer-config work.
---

# ML-Agents training

Single source of commands and metric meanings: `docs/TRAINING.md`. Configs: `Trainer/config/`.
Versions: `com.unity.ml-agents` 4.x, `mlagents==1.1.0`, venv `.venv-ml` (see `docs/SETUP.md`).

## Configure

- One YAML per class: `Trainer/config/<class>_ppo.yaml`, behavior name = class name
  (`Warrior`, `Mage`, `Archer`), must equal `BehaviorParameters.BehaviorName` in Unity.
- `environment_parameters` names must match what `HeroAgent` reads via
  `Academy.Instance.EnvironmentParameters.GetWithDefault(...)` and map onto `ArenaConfig`:
  `zombie_count`, `arena_size`, `hp_mult`, `damage_mult`, `speed_mult`,
  `kind_mix_runner`, `kind_mix_brute`, `kind_mix_spitter` (M3).
- Change one hyperparameter at a time; record why in the run log.

## Run

- Only possible on the owner's PC (GPU). If the computer is not linked, write an Owner task
  with the exact command from `docs/TRAINING.md`.
- Prefer headless builds with `--num-envs` and `--time-scale=20` over Editor training.

## Diagnose

For a run: read `results/<run-id>/run_logs/` and TensorBoard scalars (or ask the owner for a
screenshot). Use the table in `docs/TRAINING.md`. For deeper design questions spawn the
`rl-trainer` subagent.

## Finish

Add a row to the run log in `docs/TRAINING.md`; copy a good model into the save folder only via
the game's Training Center flow (M4), never into git.
