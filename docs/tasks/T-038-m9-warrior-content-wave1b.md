# T-038: M9 wave 1b — more Warrior weapons and four stat passives (no schema change)

- **Milestone:** M9 (content wave 1; plan in `docs/CONTENT-PROPOSAL.md`)
- **Owner:** Claude subagent (`core-sim-engineer`), Claude reviews and does git
- **Branch:** `claude/serene-sagan-3a70q5` (do NOT run any git command)
- **Depends on:** T-036 (6 + 6 slots, `OldRules`/`OldConfig` test helpers, weapons 26–30, passives 58–61).
  Read `docs/tasks/T-036-m9-warrior-content-wave1a.md` (its Report) and follow the same patterns.
  Observation schema v4 (2264) and actions 9/5/5 MUST NOT change.

## Goal (owner view)

More Vampire Survivors-style variety for the Warrior: a shield, a boomerang, a damaging pool, and the passives that
make projectile and orbit builds work (more projectiles, longer durations).

## Scope

Core only (`Unity/Assets/Arena/Core`, `CoreTests`). No View/ML edits; list what the viewer needs in the Report.
Dotnet: `export PATH=$PATH:/root/.dotnet`; `dotnet test CoreTests -c Release`.

### 1. New Warrior weapons (catalog 31–33; starting numbers, max level 5)

| Index | Id | Name | Behaviour | Level 1 → per level |
|---|---|---|---|---|
| 31 | `barrier` | Vòng bảo hộ | Passive weapon: a shield that absorbs the next N hits (N = 1, 1, 2, 2, 3); after it breaks it returns after a recharge delay. An absorbed hit deals no damage to the hero (and counts as `Retaliate`-neutral: it does not set retaliate). Emits a `WeaponFired` when it breaks and when it returns | recharge 12 s (−1 s per level) |
| 32 | `boomerang` | Boomerang | Flies out to the nearest enemy within 10 m, turns around at max range (or the first obstacle) and flies back to the hero; hits enemies on the way out and back (each enemy at most once per direction); count 1,1,2,2,3 | 18 dmg (+6), speed 11, radius 0.45, range 8 m, cooldown 1.8 s |
| 33 | `poison-pool` | Bình độc | Drops a damage zone (radius 1.8 m × area) at the position of the densest group of enemies within 8 m (fallback: a random point 3 m from the hero); the zone lasts 3.5 s (× duration) and hurts every enemy inside every 0.5 s; up to 3 zones alive at once | 5 dmg per tick (+2), cooldown 4 s |

Add them to Warrior `WeaponPool`. New `WeaponPattern`s: `Barrier`, `Boomerang`, `Zone` (limit: at most one `Barrier`
per hero; boomerang and zone volleys keep their own pooled state; zero allocation in `Step`; deterministic).
Pooling: preallocate fixed arrays (zones 3, boomerangs 6). If a pool is full, the new volley is skipped, never an
allocation.

### 2. New passives (catalog 34–37, all three classes' `PassivePool`)

| Index | Id | Name | Effect per level (max level 5, Amount 2) |
|---|---|---|---|
| 34 | `duration-charm` | Bùa thời gian | +10% duration of weapon volleys (orbit, aura-based lifetimes, boomerang flight stays, poison zones, barrier recharge is NOT affected) → new stat `StatId.Duration` (use `Reserved13`; rename it) |
| 35 | `duplicator` | Bộ nhân đôi | +1 projectile per volley for count-based weapons (Thrown, Thrust, Orbit, Fan, Strike, Boomerang), max level 2 → new stat `StatId.Amount` (use `Reserved14`; rename it). Mind the buffers sized by `CountByLevel` and `orbitAxePositions` (8): cap, never overflow |
| 36 | `spiked-armor` | Giáp phản | +1 armor and +10% of the damage taken is reflected to every enemy touching the hero (contact attackers only) | 
| 37 | `omni-box` | Hộp tổng hợp | +4% damage, +4% move speed, +4% duration, +4% area | 

Wire each into the same stat totals as the T-036 passives. `MaxLevel` for `duplicator` is 2.

## Hard rules
- Same as T-036: pure C# 9, deterministic RNG, allocation-free `Step`/`Write`/`WriteMask`, `TreatWarningsAsErrors`,
  no files outside `Core`, `CoreTests` and this task file; no `.meta` needed for `.cs` inside Core? check how
  existing files do it and add the `.meta` for any NEW `.cs` file.
- **Goldens:** the old-rule helpers must keep every old test bit-identical. Extend `OldRules` so old pools exclude
  everything new; new stat terms must add exactly 0 when no new passive is owned. The Linux goldens
  (`Tier1_ScriptedRun…`, `Tier1_EvaluatorRun…`) must pass with their old constants unchanged. Do not re-record any.
- Tests for each new weapon and passive, in a new file `CoreTests/Survivor/SurvivorM9WaveBTests.cs`: behaviour per
  the table, scaling per level, determinism, zero allocation, pool-full is safe, 6 + 6 slots still hold, the
  barrier absorbing then returning, duplicator never overflows any buffer with all 6 weapons at level 5.

## Report (fill in when done)
