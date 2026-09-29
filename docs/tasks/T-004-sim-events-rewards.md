# T-004: `SimEvent` + `RewardCalculator` + sign tests

- **Owner:** Claude writes the final spec after T-003 merges → then Codex implements
- **Status:** todo (spec draft)
- **Milestone:** M1
- **Parallel OK with:** —
- **Depends on:** T-003

## Goal

Every reward the agent receives comes from simulation events through one calculator, and every
term has a test that proves its sign (AGENTS.md §6).

## Draft spec (to finalise)

- `enum SimEventType { Tick, DamageDealt, DamageTaken, ZombieKilled, Backstab, Parry, SkillWasted, HeroDied, WaveCleared }`
- `readonly struct SimEvent { SimEventType Type; int ActorId; int TargetId; float Amount; }`
- `sealed class RewardWeights` (values live in `HeroClassDef` later; defaults below).
- `static float RewardCalculator.Compute(ReadOnlySpan<SimEvent> events, RewardWeights w)` —
  no allocation.

| Term | Default | Sign |
|---|---|---|
| Survive per decision | +0.001 | + |
| Damage dealt (per HP) | +0.01 | + |
| Zombie killed | +0.5 | + |
| Backstab / Parry bonus | +0.2 / +0.2 | + |
| Damage taken (per HP) | −0.01 | − |
| Skill wasted (no target in range) | −0.002 | − (small) |
| Hero died | −1.0 | − |
| Wave cleared | +1.0 | + |

## Tests (must add)

- One test per row: a single event of that type gives a reward with the expected sign.
- Using the melee attack (DamageDealt + ZombieKilled) always nets positive, even with SkillWasted
  in the same step — this is the "spear bug" regression test.
- Empty span → 0.
