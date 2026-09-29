# T-005: `ArenaSim` minimal

- **Owner:** Claude writes the final spec → Codex implements
- **Status:** todo (spec draft)
- **Milestone:** M1
- **Parallel OK with:** —
- **Depends on:** T-001, T-002, T-003, T-004

## Goal

A headless, deterministic simulation of one Warrior against N Walkers that both the Unity view
and the ML agent drive. Enough to play by hand in M1.

## Draft spec (to finalise)

- `ArenaSim(ArenaConfig cfg)`; `Reset(ulong seed)`; `Step(in HeroInput input)` advances one tick
  and appends `SimEvent`s to a reusable buffer exposed as `ReadOnlySpan<SimEvent> LastEvents`.
- `HeroInput { int Move (0–8); int Turn (0–2); int Skill (0–4); }` — matches the 3 action branches.
- Hero: position, facing, `Health`, `Energy`, 4 `Cooldown`s, `StunTimer`. Walker: position,
  `Health`, chase + melee with cooldown.
- Circle-vs-circle collision, arena bounds clamp. Spawns at edges using `Rng`.
- Test: same seed + same inputs for 600 ticks → identical state hash.
