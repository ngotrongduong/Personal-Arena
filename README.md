# Personal Arena

A 3D top-down arena game (Unity 6) where every hero — Warrior, Mage, Archer —
learns to fight zombies **by itself** with reinforcement learning (ML-Agents PPO),
inspired by Pezzza's Work "AI Gladiator learns to fight Zombies".

Player loop (target): buy a hero → train it in your own arena (size, difficulty,
zombie count and types) → watch it fight. Each owned hero has its own brain.

## Where to look

| File | What |
|---|---|
| [docs/STATUS.md](docs/STATUS.md) | **Start here.** Current milestone, next steps, session log |
| [docs/tasks/BOARD.md](docs/tasks/BOARD.md) | Task board (Claude / Codex / owner) |
| [docs/PLAN.md](docs/PLAN.md) | Vision, architecture, milestones M0–M5 |
| [docs/DECISIONS.md](docs/DECISIONS.md) | Locked-in decisions |
| [docs/SETUP.md](docs/SETUP.md) | Machine, Unity, Python venv, Unity MCP |
| [docs/TRAINING.md](docs/TRAINING.md) | How to train and read results |
| [docs/RESOURCES.md](docs/RESOURCES.md) | External tools, templates and CC0 assets |
| [AGENTS.md](AGENTS.md) | Rules for all AI agents |

## Layout

| Path | What |
|---|---|
| `Unity/` | Unity 6000.3 project (`Assets/Arena/Core` = pure C# simulation) |
| `CoreTests/` | dotnet NUnit tests that compile the Core sources directly |
| `Trainer/` | Python side: ML-Agents configs, trainer wrapper, lock file |
| `docs/` | Status, plan, decisions, guides, tasks |
| `.claude/` | Claude subagents, skills and settings |
| `.agents/skills/` | Codex skills |
