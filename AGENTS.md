# Rules for every coding agent (Claude, Codex, subagents)

This file is the single source of rules. `CLAUDE.md` imports it; Codex reads it directly.

## 1. Start and end of every session

1. **Start:** read `docs/STATUS.md` (where we are, what is next), then `docs/tasks/BOARD.md`
   (who does what). Read `docs/PLAN.md` / `docs/DECISIONS.md` only when the task needs them.
2. **End:** update `docs/STATUS.md` (the "Now", "Next" and "Session log" sections) and
   `docs/tasks/BOARD.md`. Record any new locked-in choice in `docs/DECISIONS.md`.
   A session that changed code but not STATUS is not finished.

## 2. Permissions (granted by the owner, 2026-09-29)

- Agents and subagents may create, edit, copy, move and delete files in this repo without
  asking. Just do the work and report it.
- Still stop and ask before: force-pushing, rewriting published history, deleting a branch
  other than your own, or anything that spends money or touches real-money payments.

## 3. Who does what

| Agent | Does | Never does |
|---|---|---|
| **Claude** | Plans, splits work into tasks, reviews, runs git (branch, commit, merge, push), runs Unity batchmode and real training, updates STATUS | — |
| **Codex** | Implements one task file from `docs/tasks/` at a time (C# Core, tests, Python trainer), runs tests, writes a report in the task file | **Any git command** (commit, branch, push, checkout, stash, reset) |
| **Owner (@ngotrongduong)** | Runs things only possible on the Windows PC (Unity Editor GUI, GPU training when Claude is not linked), decides game design questions | — |

Codex workflow: pick the first task with `Owner: Codex` and `Status: todo` on the board →
set it to `doing` → implement → `dotnet test CoreTests` → fill the task's "Report" section →
set it to `review`. Claude reviews, commits and sets `done`.

## 4. Git

- Branches: `feature/mN-<topic>`, `fix/<topic>`, `chore/<topic>`, merged into `main` with a
  merge commit (`--no-ff`). No `develop` branch.
- Commit message: `type(scope): summary` (feat, fix, chore, docs, test, refactor), then the
  `Co-Authored-By` trailer the harness gives you.
- Never commit trained models (`*.onnx`, `*.sentis`), demos (`*.demo`), `results/`, saves,
  logs or Unity `Library/`. **Do** commit every Unity `.meta` file next to its asset.

## 5. Product rules

- In-game "buying" uses in-game gold only. No real-money purchases, ads, analytics or network
  calls in the game.
- Assets must be CC0 or MIT (see `docs/RESOURCES.md`). Record the source of every imported pack.

## 6. Core invariants (`Unity/Assets/Arena/Core/`)

- Pure C# 9, no `UnityEngine`, no `System.Numerics` (use `Vec2`), no LINQ in hot paths, no
  static mutable state. Assembly `PersonalArena.Core` has `noEngineReferences: true`.
- Deterministic: all randomness comes from the seeded `Rng`. Same seed + same inputs = same result.
- Fixed step: `ArenaSim.Step` advances exactly one tick (`1/60 s`); the agent decides every
  5 ticks (12 Hz).
- Observation size depends only on hero class and sensor config, never on zombie count.
- Rewards come only from `SimEvent`s via `RewardCalculator`. Every reward term has a **sign
  test** (the video's agent never used its spear because of a sign bug).
- Units: metres, seconds, radians; +x right, +y "up" on the arena floor (= Unity +z).
- Names that do not exist yet (`Rng`, `ArenaSim`, `SimEvent`, `RewardCalculator`) are the
  planned names; create them with exactly these names.

## 7. Code style

- C#: namespace `PersonalArena.Core` (Core), `PersonalArena.<Area>` elsewhere; 4 spaces;
  `TreatWarningsAsErrors` is on in CoreTests. See `.editorconfig`.
- Python (`Trainer/`): 3.10, type hints, `pytest`.
- Tests sit next to their subject: `CoreTests/<Type>Tests.cs`, `Trainer/tests/test_*.py`.

## 8. Commands

| What | Command |
|---|---|
| Core unit tests | `dotnet test CoreTests` (must pass before any merge; CI runs it on every push) |
| Trainer tests | `.venv-ml\Scripts\python -m pytest Trainer` |
| Unity EditMode tests | `Unity.exe -batchmode -projectPath Unity -runTests -testPlatform EditMode -testResults results/editmode.xml` |
| Training | see `docs/TRAINING.md` |

## 9. Language

Docs for the owner (`STATUS`, `PLAN`, `DECISIONS`, task summaries) are in Vietnamese.
Agent instructions, code, comments and commit messages are in English.
