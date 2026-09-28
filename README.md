# Personal Arena

A 3D top-down arena game (Unity 6) where every hero — Warrior, Mage, Archer —
learns to fight zombies **by itself** with reinforcement learning (ML-Agents PPO),
inspired by Pezzza's Work "AI Gladiator learns to fight Zombies".

Player loop (target): buy a hero → train it in your own arena (size, difficulty,
zombie count and types) → watch it fight. Each owned hero has its own brain.

Status: **M0 — setup**. See [docs/PLAN.md](docs/PLAN.md).

## Layout

| Path | What |
|---|---|
| `Unity/` | Unity 6000.3 project |
| `Unity/Assets/Arena/Core/` | Pure C# simulation (no `UnityEngine`), deterministic, fast, unit-tested |
| `CoreTests/` | dotnet NUnit tests that compile the Core sources directly |
| `Trainer/` | Python side: ML-Agents trainer wrapper and configs |
| `docs/` | Plan and guides |

## Dev environment

- Unity 6000.3.2f1 with `com.unity.ml-agents` 4.x
- Python 3.10.12 venv at `.venv-ml` (created with `uv`), `torch 2.2.x+cu121`, `mlagents==1.1.0`
  (`setuptools<70` is required — mlagents imports `pkg_resources`)
- .NET SDK 10 for `dotnet test CoreTests`
