# Rules for coding agents (Codex, Claude)

- **Codex never runs git** (no commit, branch, push, checkout). Claude handles git and PRs.
- Commits end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- Never commit trained models (`*.onnx`, `*.sentis`), demos (`*.demo`), `results/`,
  saves, logs or Unity `Library/`.
- In-game "buying" uses in-game gold only. There are no real-money purchases, ads or
  network calls in the game.

## Core invariants (`Unity/Assets/Arena/Core/`)

- Pure C# 9, no `UnityEngine`, no `System.Numerics` (use `Vec2`), no LINQ in hot paths, no
  static mutable state. Assembly `PersonalArena.Core` has `noEngineReferences: true`.
- Deterministic: all randomness comes from the seeded `Rng`. The same seed and inputs give the same result.
- Fixed step. `ArenaSim.Step` advances exactly one tick (`1/60 s`); the agent decides every 5 ticks (12 Hz).
- The observation size depends only on the hero class and sensor config, never on the zombie count.
- Rewards are computed only from `SimEvent`s by `RewardCalculator`. Every reward term
  has a sign test, because the video's agent never used its spear due to a sign bug.
- Units: metres, seconds, radians; +x right, +y "up" on the arena floor (maps to Unity +z).

## Tests

- `dotnet test CoreTests` must pass before any PR.
- Unity EditMode tests are added from M1 on, once the Editor licence works.
