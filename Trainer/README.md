# Trainer

Python side of Personal Arena.

| Path | What |
|---|---|
| `requirements-ml.lock.txt` | Exact ML environment (Windows, Python 3.10.12, torch cu121). Install: `docs/SETUP.md` |
| `config/warrior_survivor_ppo.yaml` | Survivor PPO and its reward-measured run-length curriculum |
| `arena_trainer.py` *(M4)* | Wrapper the game calls to train one owned hero and write `progress.json` |
| `tests/` *(M4)* | pytest |

## Survivor runs

New runs use names such as `warrior-s001`, `warrior-s002`, and so on. Legacy names such as
`warrior-011` do not affect this sequence and are never resumed as Survivor runs.

Only the Warrior trains in Survivor mode for now. Mage and Archer get their own Survivor configs in
M7; until then the training service reports that clearly instead of starting a run.

Each run records `schema_version.txt`; a run without it counts as schema 1. Training resumes a
checkpoint when its observation schema matches the current schema; a new schema starts a new run. Balance or rules changes that leave the
schema unchanged continue the same run. M4B adds a checkpoint upgrade path between schemas.

The Warrior Survivor config uses a 3-by-512 PPO network with game-normalized observations. Its
reward curriculum advances the episode limit from 3 to 6 to 10 to 15 minutes only after the
smoothed mean reward shows that most agents survive the current lesson.

How to train and read results: `docs/TRAINING.md`.
