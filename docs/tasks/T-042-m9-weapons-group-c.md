# T-042: M9 weapons group C — five new weapons on schema v5 (catalog 64–68)

- **Milestone:** M9 (D-041; plan in `docs/CONTENT-PROPOSAL.md`, group C)
- **Owner:** Claude subagent (`core-sim-engineer`), Claude reviews and does git
- **Branch:** `claude/serene-sagan-3a70q5` (do NOT run any git command)
- **Depends on:** T-036, T-038 (6 + 6 slots, `OldRules`/`OldConfig`, pooled patterns), T-039 (schema v5, catalog 128).
  Read their Reports. The observation/action schema is already v5 (2592, 9/7/5): do not change it.

## Goal (owner view)

Five more Vampire Survivors-style weapons that each force a different way of playing: bouncing shots, a weapon that
likes moving, a time freeze, a screen wipe and a rotating bombardment. They go into the new catalog range (64–68; ids
64–95 are reserved for new weapons) and into class pools. The numbers are starting points.

## Weapons (max level 5)

| Index | Id | Name | Behaviour | Numbers (level 1 → per level) | Class pools |
|---|---|---|---|---|---|
| 64 | `bounce-shot` | Đạn nảy | Projectile at the nearest enemy that **bounces off map walls and obstacles** (walls and obstacles use up a bounce; hitting an enemy does not, it flies on; each enemy at most once per 0.5 s per projectile). Dies when bounces or lifetime are used up | 12 dmg (+4), speed 10, radius 0.35, lifetime 4 s, bounces 3,3,4,4,5, count 1,1,2,2,3, cooldown 2 s | all three |
| 65 | `momentum-spirit` | Bóng tốc | A short volley of projectiles in the direction the hero is moving (facing if standing still). Damage is scaled by a **movement factor**: an exponential moving average of "the hero is moving" (> 0.5 m/s), time constant 1.5 s, factor in [0, 1], damage × (0.4 + 1.2 × factor) | 10 dmg (+4), speed 12, count 1,2,2,3,3, cooldown 1 s | Archer |
| 66 | `time-clock` | Đồng hồ băng | Every cooldown, with a chance, **stuns every non-boss enemy within 14 m** (use the existing stun mechanism; the boss is immune, elites get half the time) | chance 30% (+10%), stun 1.2 s (+0.2), cooldown 6 s (−0.5) | Mage |
| 67 | `purge` | Thanh tẩy | Very long cooldown. **Kills every normal enemy within 14 m** (not elites, not the boss; elites and the boss lose a flat 5% of max HP). Kills count like any kill (events, drops, XP) | cooldown 30 s (−2); radius grows 14 m × area | Mage |
| 68 | `bomb-ring` | Mưa bom vòng | A salvo of N explosions placed on a ring of radius 4 m × area around the hero, one every 0.15 s, the ring rotates by a fixed step each salvo | N 4,4,5,5,6; blast radius 1.3 m; 22 dmg (+7); cooldown 3.5 s | Mage |

Notes:
- Add each to the `WeaponPool` of the classes in the last column (existing pools stay; append). Use `Get(index)` rows with
  Vietnamese names as above. Add new `WeaponPattern`s only where the table needs them. A class holds at most one of each
  persistent-state pattern; keep the existing rules for Orbit/Aura/Shockwave/Combo/Retaliate/Barrier.
- `time-clock` and `purge` use no projectiles. `purge` and `bomb-ring` must not allocate: preallocate fixed arrays
  (pending blasts 8) and reuse them; if full, skip and do not spend the cooldown.
- Determinism: only `rng` from the sim, and only when the weapon is owned (so old-rule runs stay bit-identical).
- Events: `WeaponFired` when a weapon fires (put the useful number in `Extra`/`Value`), `StrikeLanded` (id = catalog index,
  value = radius) for every blast, as in the existing blast weapons, so the viewer can draw them.
- Duration/Amount stats from T-038: `duplicator` adds projectiles to count-based weapons (bounce-shot, momentum-spirit);
  bomb-ring salvo size also gets +Amount. `duration-charm` lengthens the stun of `time-clock`.

## Hard rules
- Pure C# 9, allocation-free `Step`/`Write`/`WriteMask`, `TreatWarningsAsErrors`, deterministic.
- Old behaviour stays bit-identical: extend the `OldRules` helper so the old-rule configs exclude 64–68 from every pool.
  The Linux goldens (`Tier1_ScriptedRun…`, `Tier1_EvaluatorRun…`) must pass unchanged; re-record nothing.
- Only edit `Unity/Assets/Arena/Core`, `CoreTests` and this task file (new `.cs` in Core need a `.meta`).
  No git commands. Run `export PATH=$PATH:/root/.dotnet; dotnet test CoreTests -c Release`.
- Tests in a new file `CoreTests/Survivor/SurvivorM9GroupCTests.cs`: each weapon does what the table says (bounces counted,
  movement factor changes the damage, stun applies and boss is immune, purge kills normal enemies only and respects the
  radius and the boss/elite rule, ring positions and rotation), scaling per level, determinism with all five owned,
  zero allocation with the full kit, pools in the right classes, 6 + 6 slots still hold, pool-full is safe.

## Report (fill in when done)
