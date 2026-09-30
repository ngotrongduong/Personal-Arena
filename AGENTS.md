# Rules for every coding agent (Claude, Codex, subagents)

This file is the single source of rules. `CLAUDE.md` imports it; Codex reads it directly.

## Authority (standing rule from the user, 2026-09-29)

- The user is not a programmer and only decides the game's direction (features, classes,
  priorities, milestones). Claude (the orchestrator) has full authority over every technical
  decision and commands Codex and subagents. Agents never ask the user to approve or choose
  anything technical. They work until the task is done and report to Claude.
- Avoid commands that trigger permission prompts: one simple command per call, with no
  `cd &&`, pipes, heredocs or chained `$VAR`s. Put multi-step logic in a script file.
- Hard limits remain: no credentials or sign-ins, no real money, no permanent deletion of
  user data, and no touching other apps or games.

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

- Branches: `feature/mN-<topic>`, `fix/<topic>`, `chore/<topic>` off `develop`, merged back
  into `develop` (PR or `--no-ff`). `main` is fast-forwarded to `develop` when a chunk of work
  is stable. Keep `docs/STATUS.md` correct on `main` too: cloud sessions open `main` by default.
- Cloud clones are shallow and may only contain `main`: run `git fetch origin develop` and
  work from `develop`.
- Commit message: `type(scope): summary` (feat, fix, chore, docs, test, refactor), then the
  `Co-Authored-By` trailer the harness gives you.
- Never commit trained models (`*.onnx`, `*.sentis`), demos (`*.demo`), `results/`, saves,
  logs or Unity `Library/`. **Do** commit every Unity `.meta` file next to its asset.
- The repo is **public** (D-015, keeps GitHub Actions free). Never commit secrets, tokens,
  credentials, personal data or machine-private files. Do not make it private: that costs money.

## 5. Product rules

- In-game "buying" uses in-game gold only. No real-money purchases, ads, analytics or network
  calls in the game.
- Assets must be CC0 or MIT (see `docs/RESOURCES.md`). Record the source of every imported pack.

## 6. Core invariants (`Unity/Assets/Arena/Core/`)

- Pure C# 9, no `UnityEngine`, no `System.Numerics` (use `Vec2`), no LINQ in hot paths, no
  static mutable state. Assembly `PersonalArena.Core` has `noEngineReferences: true`.
- Deterministic: all randomness comes from the seeded `Rng`. Same seed + same inputs = same result.
- The only game mode is Survivor (D-026): `PersonalArena.Core.Survivor` (`SurvivorSim`,
  `SurvivorConfig`, balance numbers in `SurvivorTuning` with `Validate()`). The old round arena
  (`ArenaSim`, abyss, obs 883) was removed in T-017.
- Fixed step: `SurvivorSim.Step` advances exactly one tick (`SurvivorSim.FixedDeltaTime` = 1/60 s);
  the agent decides every 5 ticks (`SurvivorPilot.DecisionPeriod`), and again on every tick while
  a level-up offer is waiting. Unity code must not assume Unity's fixed timestep equals the sim tick.
- `Step`, `SurvivorObservation.Write` and `WriteMask` allocate nothing (pooled entities,
  `SpatialHash`). Keep the Release `Step` under about 0.05 ms at the late-game horde.
- Observations: `SurvivorObservation`, schema v4 = 2264 values in [-1, 1] (self 64, inventory 64,
  offers 4 × 66, 72 rays × 25, density 3 × 8 × 3). Slots are reserved (64-item catalog, 8 enemy
  kinds) so new content fills slots instead of changing the size. Actions: 3 discrete branches
  9 / 5 / 5 (move, active skill, level-up pick).
- Adaptive brains (D-027): a run records `schema_version.txt`; the trainer resumes any run whose
  schema matches, even after rule changes. Only a change to the observation or action layout
  bumps `SurvivorObservation.SchemaVersion` (then `brain_upgrade.py` grows the old brain,
  never train from scratch). The viewer loads only brains with the current schema.
- Rewards come only from `SurvivorEvent`s (`SurvivorSim.Events`) via `SurvivorRewardCalculator` (weights in
  `SurvivorRewardConfig`, hierarchy in D-030). Every reward term has a **sign test** (the video's
  agent never used its spear because of a sign bug).
- Evaluation: `SurvivorEvaluator` / `Tools/SurvivorEval` plays fixed seeds with an exported
  `.brain`. M4A acceptance is 100 seeds, median ≥ 600 s, P10 ≥ 420 s, no death before 180 s.
- Units: metres, seconds, radians; +x right, +y "up" on the arena floor (= Unity +z); facing
  0 = +x, counter-clockwise positive.
- Game rules live only in Core. Unity MonoBehaviours read Core state and feed `SurvivorInput`.

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
| Unity EditMode tests | `"C:\Program Files\Unity\Hub\Editor\6000.3.2f1\Editor\Unity.exe" -batchmode -projectPath Unity -runTests -testPlatform EditMode -testResults results/editmode.xml` (default Hub path; adjust if Unity is elsewhere) |
| Training | see `docs/TRAINING.md` |

## 9. Language

Docs for the owner (`STATUS`, `PLAN`, `DECISIONS`, task summaries) are in Vietnamese.
Agent instructions, code, comments and commit messages are in English.
