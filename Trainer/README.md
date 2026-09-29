# Trainer

Python side of Personal Arena.

| Path | What |
|---|---|
| `requirements-ml.lock.txt` | Exact ML environment (Windows, Python 3.10.12, torch cu121). Install: `docs/SETUP.md` |
| `config/<class>_ppo.yaml` | ML-Agents PPO + curriculum per hero class |
| `arena_trainer.py` *(M4)* | Wrapper the game calls to train one owned hero and write `progress.json` |
| `tests/` *(M4)* | pytest |

How to train and read results: `docs/TRAINING.md`.
