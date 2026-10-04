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

**Status: review.** `dotnet test CoreTests -c Release`: 395 passed, 0 failed, 0 skipped (361 before; 34 new tests in `CoreTests/Survivor/SurvivorM9GroupCTests.cs`). Linux goldens pass unchanged, nothing re-recorded.

### Changes per file (`Unity/Assets/Arena/Core/Survivor/` unless noted)
- `SurvivorDefs.cs`: `WeaponPattern` + `Bounce, Momentum, Freeze, Purge, BombRing`; `ItemDef` + `BouncesByLevel`, `Chance`, `ChancePerLevel`, `StunPerLevel`; index consts 64-68; rows 64-68 (also in `Get`); consts `BounceRehitSeconds`, `Momentum*`, `FreezeEliteShare`, `PurgeBossFraction`, `BombRingRotationStep`, `MaxPendingBlasts`; pools: Warrior +64, Mage +64,66,67,68, Archer +64,65.
- `SurvivorEntities.cs`: `SurvivorProjectile` + `Bouncing`, `BouncesLeft`, `BounceCount` (public `Bounces`), 8-slot `BounceIds`/`BounceUntil` ring.
- `SurvivorSim.GroupC.cs` (new, + `.meta`): bounce shot (`FireBounce`, `BounceProjectile`, `FindObstacle`), momentum (`UpdateMovementFactor`, `FireMomentum`, public `MovementFactor`), `UpdateFreeze`, `Purge`, bomb ring (`StartBombRing`, `UpdatePendingBlasts`, public `PendingBlastCount`; fixed 8-slot arrays).
- `SurvivorSim.Weapons.cs`: dispatch for the 5 patterns; `UpdateProjectiles` bounce branch (no map-edge expiry, no pierce, per-enemy 0.5 s rehit via `AlreadyHit`); `NewProjectile` resets `Bouncing`.
- `SurvivorSim.Content.cs`: `LaunchProjectile(..., bounces = -1)`; `ResetContentState` clears the new state. `SurvivorSim.cs`: `UpdateMovementFactor()` after `MoveHero`, `UpdatePendingBlasts()` after `UpdateZones`, test hook `SetMovementFactorForTests`.
- Tests: `SurvivorM9GroupCTests.cs` (new); `SurvivorTestHelpers.OldRules` now also resets the Mage and Archer weapon pools; pool/`Get(64)` assertions updated in `SurvivorDefsTests`, `SurvivorM4CContentTests`, `SurvivorM7Tests`, `SurvivorSchemaV5Tests` (first empty row is now 69).

### Decisions on unspecified details
- Bounce count semantics: "bounces 3" = 3 reflections happen; the 4th wall/obstacle contact kills the shot. One bounce per tick at most (a corner counts once). Obstacle bounce reflects velocity about the contact normal only if moving into it, and pushes the shot out. Multi-shot volleys fan 20 degrees apart (as spears); target = nearest enemy within 12 m (`BaseRange`), no target = no fire, cooldown kept. Knockback 0.2. Range = 40 m (4 s at 10 m/s). Rehit memory is 8 slots (a ring), so in an extreme crowd an enemy might be re-hit early.
- Momentum: always fires when ready (no target needed), direction = hero velocity if > 0.5 m/s else facing. Fan of count shots over 20 degrees total, radius 0.3, range 14 m, knockback 0.2, no pierce. Factor is updated every tick (with or without the weapon, no rng), starts at 0 each run, EMA step = dt/1.5. `WeaponFired`: Value = factor, Extra = count.
- Time clock: needs an enemy (any, boss included) within 14 m to roll; when ready the cooldown is spent whether or not the roll succeeds; one `rng.NextFloat()` per attempt. Stun = (1.2 + 0.2 per level) x `DurationMul`; elites half; range fixed 14 m (area does not scale it). Cooldown (6 - 0.5 per level) x cooldown mul. `WeaponFired`: Value = stun seconds, Extra = enemies stunned (only on success). No `StrikeLanded` for it.
- Purge: needs an enemy within 14 m x area (+ enemy radius); then kills normal enemies via flat `DamageEnemy(hp)` (no crit, no rng; events DamageDealt + EnemyKilled, drops, XP as any kill); elites and the boss take a flat 5% of max HP (flat `DamageEnemy`, so `BossDamaged` fires and an elite below 5% HP would die). `StrikeLanded` (id 67, Value = radius) first, then `WeaponFired` (Value = radius, Extra = kills). Cooldown (30 - 2 per level) x cooldown mul.
- Bomb ring: blasts are placed on the ring around the hero at fire time (not following the hero), first lands on the fire tick, then one per 9 ticks. Needs an enemy within ring + blast radius. Blast radius = 1.3 x area (the table gave 1.3 flat; area applied like the other strike weapons), knockback 0.5, positions clamped to the map. Ring angle starts at 0 and turns by 30 degrees per salvo (reset per run). Needs N free slots of 8, else skipped with cooldown kept. Salvo size = `VolleyCount` (duplicator, cap 8). `StrikeLanded` is emitted when each blast lands (id 68, Value = radius, Point = blast); `WeaponFired` at salvo start (Extra = N).

### What the Unity viewer needs (nothing edited in View/ML)
- Icons: 5 weapons. `SoundCueMap.WeaponCue` has no case for `Bounce`, `Momentum`, `Freeze`, `Purge`, `BombRing`.
- Bounce shot: projectiles with `SourceIndex` 64 (read `Projectiles`, `Bounces` for a spark on each bounce); a glowing ball, bounce sound. Momentum: projectiles with `SourceIndex` 65; tint by `sim.MovementFactor` or by `WeaponFired.Value`; whoosh sound.
- Time clock: draw an icy ring/flash of 14 m at the hero on `WeaponFired` id 66 (Value = stun s, Extra = count); frozen tint on stunned enemies (they already have `StunRemaining`); chime/freeze sound.
- Purge: big expanding white ring from `StrikeLanded` id 67 (Value = radius), screen flash, deep boom; enemies vanish via the normal `EnemyKilled` events.
- Bomb ring: bomb explosion per `StrikeLanded` id 68 (Value = radius 1.3 x area, Point); optionally a telegraph ring (radius 4 x area) at the hero on `WeaponFired` id 68; explosion sounds.
- HUD/level-up cards need names/icons for catalog 64-68 (Vietnamese names in the catalog). Per-index tables (sprites, ML episode item lists) were not checked.

### Not verified / weak tests
- Windows-only goldens and Unity EditMode tests not run. Release `Step` time (0.05 ms) not measured; `Performance_LateGame` passes. New work is O(enemies) per cast plus O(projectiles) per tick.
- Weak: the obstacle-bounce test depends on a seed placing a usable obstacle (loops seeds; Inconclusive otherwise; it ran green). The time-clock chance is checked statistically (800 trials, +-0.06 windows), not exactly. The allocation test is a full-kit run asserting 0 bytes over 1500 ticks; it proves no per-tick leak, not that every path was hit (the "every weapon acts" test covers that for events). The 8-slot rehit overflow and bomb-ring map clamp are untested. Bounce wall test shortens the map (half size 11) and overrides projectile lifetime through an internal setter to isolate the bounce limit.
- Balance numbers are starting points, untuned.
