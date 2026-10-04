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

**Status: review.** `dotnet test CoreTests -c Release`: 342 passed, 0 failed (286 before; 56 new test cases in
`CoreTests/Survivor/SurvivorM9WaveBTests.cs`). The two Windows-only goldens still show as skipped on Linux; the Linux goldens
(`Tier1_ScriptedRun…`, `Tier1_EvaluatorRun…`) pass with their old constants, nothing was re-recorded.

### What changed, per file (all under `Unity/Assets/Arena/Core/Survivor/` unless noted)
- `SurvivorDefs.cs`: `WeaponPattern` + `Barrier`, `Boomerang`, `Zone`. `StatId.Reserved13/14` renamed `Duration`/`Amount`. Index constants 31-37,
  catalog rows 31-33 and 34-37 (also in `Get`), `PassivePerLevel` for 34-37, `SpikedReflectPerLevel` (0.1), `MaxVolleyCount` (8).
  `Passive(...)` takes a max level (duplicator = 2). Warrior `WeaponPool` +31,32,33; every class `PassivePool` +34..37.
- `SurvivorEntities.cs`: `SurvivorDerivedStats.DurationMul / Amount / ReflectFraction`; new pooled classes `SurvivorBoomerang` and `SurvivorZone`.
- `SurvivorSim.cs`: pools `boomerangs` (6) and `zones` (3) (`BoomerangCapacity`, `ZoneCapacity`, public read-only `Boomerangs`, `Zones`);
  `Step` calls `UpdateBoomerangs`, `UpdateZones` after `UpdateProjectiles` and `ResolveReflect` after `ResolveRetaliate`;
  `RecomputeStats` adds the new terms after the old ones (each adds exactly 0 when the passive is absent); pools cleared in `ClearPools`.
- `SurvivorSim.Wave1b.cs` (new, with `.meta`): barrier (`SyncBarrier`, `UpdateBarrier`, `TryAbsorbHit`, public `BarrierCharges`), boomerang
  (`ThrowBoomerangs`, `UpdateBoomerangs`, `HitWithBoomerang`), poison pool (`DropZone`, `UpdateZones`), spiked-armor `ResolveReflect`.
- `SurvivorSim.Weapons.cs`: `FireWeapons` dispatches Barrier/Boomerang/Zone; `ThrowHammers` uses `VolleyCount`; `DamageEnemy` gets an optional `flat` flag
  (no Might, no crit, no rng) used only by the reflect.
- `SurvivorSim.Content.cs`: `VolleyCount` (count + duplicator, capped at 8) used by Thrown, Thrust, Orbit, Fan, Strike, Boomerang; orbit duration x `DurationMul`;
  new state reset in `ResetContentState`.
- `SurvivorSim.Enemies.cs`: `DamageHero` calls `TryAbsorbHit` after the parry check (before block/armor) and sums reflect damage for contact hits;
  `ApplyHeroDamage` records `lastHeroDamage`. `SurvivorSim.EnemyProjectiles.cs`: projectile hits call `TryAbsorbHit`.
- Tests: `SurvivorM9WaveBTests.cs` (new). Updated for the new pools / renamed stat: `SurvivorDefsTests`, `SurvivorM4CContentTests`, `SurvivorM7Tests`
  (pool lists, `Get(38)` as the first empty row), `SurvivorM9WarriorTests` (reserved inventory slot 38 instead of 31), `Meta/ProfileRulesTests`
  (uses `Reserved15` as the "unused stat" because 13 and 14 now have names). `OldRules` needed no change (it already overwrites both pools).

### Decisions on unspecified details
- Barrier: charges are full on the first tick the weapon is owned (or at the first hit). Absorb happens after a parry (a parry keeps the charge) and
  before block/armor, for contact, melee swings, explosions and enemy projectiles. An absorbed hit emits no `HeroDamaged`, no `Blocked`, no retaliate, no
  reflect, no stagger. Break: `WeaponFired` (id 31, `Extra` = 0). Return: `WeaponFired` (id 31, `Extra` = charges). Recharge = (12 − level + 1) x cooldown multiplier
  (the cooldown stat/hourglass does shorten it; duration does not), kept in the weapon's cooldown slot. A level-up adds the extra charges only while the shield is up.
- Boomerang: targets the nearest enemy within 10 m (`BaseRange`), flight range 8 m (`ProjectileRange`); several boomerangs fan 20 degrees apart (as spears).
  Out leg ends at max range, map edge or the first obstacle; the return leg homes on the hero's current position and ends within hero radius + boomerang radius
  (+ one step). Hits are tracked per boomerang and direction by enemy id in 48-slot lists; an enemy beyond slot 48 is not hit in that leg (extreme crowds only).
  Full pool: only as many as there are free slots fly; none free = volley skipped and cooldown not spent. 12 s age cap as a safety net. Knockback 0.3, radius scales with area, range does not.
- Poison pool: densest group = the enemy within 8 m with the most enemies inside one zone radius around it (zone lands on that enemy; first in pool order on ties; at most 64 candidate
  centres are tested, with a stride, to bound the O(n^2) work). Fallback (nobody within 8 m) = random angle, 3 m from the hero, clamped to the map. First tick comes 0.5 s after the drop
  (7 ticks over 3.5 s), then every 0.5 s; no knockback, no stun. Full pool (3 alive) = drop skipped, cooldown not spent. Duration x `DurationMul`, radius x area.
- Duration applies to orbit lifetime and poison zones only (aura has no lifetime; barrier recharge and boomerang flight are unaffected, as the table says).
- Duplicator: +1 per level to `VolleyCount` (Thrown incl. heavy hammer, Thrust, Orbit, Fan, Strike, Boomerang), capped at 8 = size of `orbitAxePositions` / `hammerTargetIds`. Not bomb, combo, shockwave.
  `stats.Amount` = min(level, 8) so a debug-granted level cannot exceed the cap; real max level is 2.
- Spiked armor: +1 armor per level; reflect = 10% x level of the damage actually taken (after armor), only for contact-attacker hits (`DamageHero(..., contact: true)`; not brute swings,
  explosions or projectiles, not the killing blow). Summed during the enemy phase and applied after `ResolveRetaliate` to every enemy within hero radius + enemy radius + contact margin,
  as flat damage (no Might, no crit, no rng) so no enemy dies inside the enemy loop. Omni box: +4% Might, move speed, duration and area per level, additive with the other sources.
- Owner stat points for Duration/Amount do not exist (`StatInfo.Cap` = 0, `UsedCount` stays 13).

### What the Unity viewer needs (nothing was edited in View/ML)
- Icons: 3 weapons (barrier, boomerang, poison pool) and 4 passives (duration charm, duplicator, spiked armor, omni box); Vietnamese names are in the catalog.
- Barrier: draw a shield ring while `sim.BarrierCharges > 0` (show the count); flash on break from `WeaponFired` id 31 with `Extra` = 0 and on return with `Extra` = charges.
  There is no event for an absorbed hit that leaves charges (the task asked for break/return only); the view can detect it from `BarrierCharges` dropping.
- Boomerang: read `sim.Boomerangs` (`Active`, `Position`, `Direction`, `Radius`, `Returning`) and render a spinning blade; `WeaponFired` id 32 has `Extra` = count, `Point` = aim.
- Poison pool: read `sim.Zones` (`Active`, `Position`, `Radius`, `Remaining`, `Duration`) and render a green puddle that fades with `Remaining/Duration`; `WeaponFired` id 33 has `Value` = duration, `Point` = centre.
- `SoundCueMap.WeaponCue` has no case for `Barrier`, `Boomerang`, `Zone`; sounds: barrier break/return, boomerang whoosh, poison bubbling. Spiked armor has no event (reflect shows only as `DamageDealt` on the touching enemies; optional thorn effect).
- Per-index tables in the view (sprites by catalog index, ML episode item lists) should be checked for 31-37. Not verified, I did not open the View code.

### Not verified / weak spots
- Windows-only goldens were not run for real (same reasoning as T-036: new terms add `+ 0f`, no new rng calls without the new items, `DamageEnemy` keeps the old path when `flat` is false).
- Release `Step` budget (0.05 ms) was not measured; `Performance_LateGame` passes. Boomerang loops are O(6 x enemies) per tick, the poison pool search is capped at 64 x enemies in range once per cast.
- Balance numbers are the task's starting points, untuned. Unity EditMode tests were not run.
- Weak tests: the obstacle-turn test depends on a seed that places an obstacle within 30 m (it loops seeds and would report Inconclusive otherwise; it ran green here); the "densest group" test checks
  a clear cluster, not tie-breaking or the 64-candidate stride; the 48-slot hit-list overflow is not tested; the full-kit/allocation tests assert "does something" counts, not exact numbers.
- Reflect applies when the hit lands in a tick where the same enemy can also be killed by the hero's weapons before `ResolveReflect`; the reflect then simply skips dead enemies.
