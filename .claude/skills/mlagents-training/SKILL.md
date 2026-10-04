---
name: mlagents-training
description: Configure, run and diagnose ML-Agents training for Personal Arena heroes - PPO YAML, curriculum, environment parameters, headless builds, TensorBoard metrics, fine-tuning per owned hero. Use for any training or trainer-config work.
---

# ML-Agents training

Single source of commands and metric meanings: `docs/TRAINING.md`. Configs: `Trainer/config/`.
Versions: `com.unity.ml-agents` 4.x, `mlagents==1.1.0`, venv `.venv-ml` (see `docs/SETUP.md`).

## Configure

- One YAML per class: `Trainer/config/<class>_survivor_ppo.yaml`, behavior name = class name
  (`Warrior`, `Mage`, `Archer`), must equal `BehaviorParameters.BehaviorName` in Unity.
- `environment_parameters` names must match what the ML glue reads via
  `Academy.Instance.EnvironmentParameters.GetWithDefault(...)`; check the current list in the YAML
  (`run_seconds`, `build_level_max`, `tier_max`, `hard_share`, …) rather than this file.
- Change one hyperparameter at a time; record why in the run log.

## Run

- Only possible on the owner's PC (GPU). If the computer is not linked, write an Owner task
  with the exact command from `docs/TRAINING.md`.
- Prefer headless builds with `--num-envs` and `--time-scale=20` over Editor training.

## Diagnose

For a run: read `Trainer/runs/<run-id>/` (run logs, `Trainer/runs/<run-id>.log`) and TensorBoard scalars (or ask the owner for a
screenshot). Use the table in `docs/TRAINING.md`. For deeper design questions spawn the
`rl-trainer` subagent.

## Finish

Add a row to the run log in `docs/TRAINING.md`. Good brains are kept by the champion and lineage
tools (`Trainer/champion.py`, `Trainer/brain_lineage.py`) under `Trainer/runs/champions/`; never in git.
