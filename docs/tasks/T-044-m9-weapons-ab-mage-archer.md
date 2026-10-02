# T-044: M9 weapons groups A/B for Mage and Archer (catalog 69–72 + pool additions)

- **Milestone:** M9 (D-041; ideas in `docs/CONTENT-PROPOSAL.md` sections 2A/2B)
- **Owner:** Claude subagent (`core-sim-engineer`), Claude reviews and does git
- **Branch:** `claude/serene-sagan-3a70q5` (do NOT run any git command)
- **Depends on:** T-036..T-043 (read their Reports). The schema is v5 and must not change.
- **Next:** T-045 (evolutions for the new weapons) needs the final weapon list from this task.

## Goal (owner view)

Mage and Archer get the more varied weapon sets the Warrior already has: they can now find several of the weapons
that were Warrior-only, and four brand-new ones. Numbers are starting points.

## 1. Pool additions (existing weapons, no new behaviour code expected)

| Class | Gets (catalog index) |
|---|---|
| Mage | 26 `flame-cone`, 31 `barrier`, 33 `poison-pool` |
| Archer | 29 `bomb`, 32 `boomerang`, 33 `poison-pool` |

Append to the class `WeaponPool` (existing entries unchanged). Check each weapon works for a class with different
stats and facing (e.g. a bomb or boomerang thrown by the Archer, barrier for the Mage: the barrier is the one
`Barrier` pattern per hero). Do not change those weapons' numbers.

## 2. New weapons (max level 5)

| Index | Id | Name | Pattern / behaviour | Numbers (level 1 → per level) | Class pools |
|---|---|---|---|---|---|
| 69 | `fireball-nova` | Hỏa cầu nổ | `Strike`-style: a big blast on a random enemy within range (reuse the Strike pattern config, larger blast, long cooldown, knockback) | 45 dmg (+14), range 9 m, blast radius 1.8 m, cooldown 3 s, count 1,1,1,2,2 | Mage |
| 70 | `bracelet-trio` | Vòng tay ba mũi | `Fan`-style: 3 projectiles in a very tight spread (8°) at ONE random enemy within range | 12 dmg each (+4), speed 14, range 11 m, cooldown 1.4 s | Mage, Archer |
| 71 | `quad-shot` | Bắn bốn hướng | New pattern `Quad`: fires one projectile in each of 4 fixed directions relative to the hero's body (facing, +90°, +180°, +270°), each pierces 1 extra enemy; count per direction 1,1,2,2,3 (staggered 8° apart) | 10 dmg (+4), speed 13, range 9 m, cooldown 1.2 s | Archer |
| 72 | `magi-stone` | Đá tụ lực | New pattern `Stone`: instant hit on the nearest enemy within range with **fixed damage that ignores Might and crit** (so it suits a build with no damage passives); count by level 1,1,2,2,3 on different enemies | 20 dmg per level (level 5 = 100), range 10 m, cooldown 0.9 s | Mage |

- Add 69–72 to the pools in the last column. Names, ids as in the table; Vietnamese names must be exactly these.
- Prefer reusing the existing Strike/Fan/Thrown code paths; add a pattern (`Quad`, `Stone`) only if needed and keep each
  dispatch in the same style as the other patterns. Fixed pools for any state; zero allocation in `Step`.
- Duplicator (+Amount) and duration/area stats behave as for the comparable existing weapons.
- Events: `WeaponFired` / `StrikeLanded` as comparable weapons do (id = catalog index) so the viewer can draw them.

## Hard rules
- Pure C# 9, deterministic (rng only from the sim, never when the weapon is unowned), allocation-free
  `Step`/`Write`/`WriteMask`, `TreatWarningsAsErrors`.
- Old behaviour bit-identical: extend `OldRules` so old-rule configs exclude 69–72 and the added pool entries from every pool.
  Linux goldens (`Tier1_ScriptedRun…`, `Tier1_EvaluatorRun…`) pass unchanged; re-record nothing.
- Only edit `Unity/Assets/Arena/Core`, `CoreTests` and this task file (new `.cs` need `.meta`); no Unity ML/View, no
  `Trainer/`. No git commands. `export PATH=$PATH:/root/.dotnet; dotnet test CoreTests -c Release`.
- Tests in a new file `CoreTests/Survivor/SurvivorM9MageArcherTests.cs`: pool contents per class (and that nothing
  else changed), each new weapon behaves per the table (blast radius, 3-shot spread, four directions rotating with
  the facing, stone ignores Might/crit and hits different enemies), scaling per level, the pool-added weapons work for
  the new class, 6 + 6 slots hold, determinism, zero allocation with a full Mage kit and a full Archer kit, offers per class.

## Report

**Status: review.** `dotnet test CoreTests -c Release`: 495 passed, 0 failed (446 before; 49 new test cases in `CoreTests/Survivor/SurvivorM9MageArcherTests.cs`). The two Windows-only goldens show as skipped on Linux; the Linux goldens pass unchanged, nothing re-recorded. Schema stays v5.

### FINAL weapon pools (for T-045)
- **Warrior** (unchanged): 0, 1, 2, 3, 4, 5, 26, 27, 28, 29, 30, 31, 32, 33, 64
- **Mage**: 14, 15, 16, 17, 18, 19, 64, 66, 67, 68, **26, 31, 33, 69, 70, 72** (16 weapons)
- **Archer**: 20, 21, 22, 23, 24, 25, 64, 65, **29, 32, 33, 70, 71** (13 weapons)
- Passive pools unchanged for all classes. New patterns: 69 `Strike`, 70 `Trio`, 71 `Quad`, 72 `Stone`. Evolutions of 69-72 do not exist yet (`EvolutionOf` = -1).

### Changes per file
- `Unity/Assets/Arena/Core/Survivor/SurvivorDefs.cs`: `WeaponPattern` + `Trio, Quad, Stone`; consts `FireballNovaIndex..MagiStoneIndex` (69-72), `QuadStaggerDegrees` (8); rows 69-72 (also in `Get`); Mage and Archer `WeaponPool` appended as above.
- `Unity/Assets/Arena/Core/Survivor/SurvivorSim.Content.cs`: `FireTrio`, `FireQuad`, `StrikeStones`. In `StrikeTargets` the blast centre is captured before the blast so `StrikeLanded.Point` stays at the blast centre (before, it was the target position after knockback; matters for the nova's knockback 1.5, and shifts the old strike weapons' event Point by their tiny knockback only, no state change).
- `Unity/Assets/Arena/Core/Survivor/SurvivorSim.Weapons.cs`: `FireWeapons` dispatch for Trio, Quad, Stone.
- Tests: new `SurvivorM9MageArcherTests.cs`; updated `SurvivorM9WaveBTests` and `SurvivorM9WarriorTests` (they asserted Mage/Archer had none of the Warrior weapons; now only the added ones are allowed), `SurvivorM9GroupCTests` and `SurvivorSchemaV5Tests` (first empty row is now 73). `OldRules` needed no change: it already overwrites the Mage and Archer pools with the old lists (the added entries are excluded; a test proves it).

### Decisions on unspecified details
- Nova (69): `Strike` pattern, Width (blast radius) 1.8, knockback 1.5, no stun, count 1,1,1,2,2, `StrikeLanded` id 69 Value = radius, plus `WeaponFired` Extra = blasts.
- Trio (70): needed a new pattern value `Trio` because `Fan` aims at the NEAREST enemy and the table says RANDOM. Target = `RandomTarget(0, 11)` (one rng draw only when the weapon is ready and an enemy is within 11 m); 3 shots over `ArcDegrees` = 8 total (so 4 degrees each side; with a duplicator, 4 shots over the same 8 degrees); count list {3,3,3,3,3}; radius 0.25, knockback 0.2, no pierce. `WeaponFired` Extra = shots, Point = aim.
- Quad (71): fires whether or not an enemy exists (the table says fixed directions, no target), in facing, +90, +180, +270 (exact vector rotations, no trig error), per-direction stagger 8 degrees centred on the direction, pierce 1 (= hits 2 enemies, existing meaning), radius 0.25, knockback 0.2, range 9 m. Duplicator adds 1 per direction (cap `MaxVolleyCount` 8; pool of 128 never overflows: 4 x 8 = 32). Pool full: remaining shots skipped, cooldown kept only if nothing fired. `WeaponFired` Extra = per-direction count, Point = facing.
- Stone (72): nearest enemy within 10 m (+ enemy radius), then the next nearest not yet hit, up to `VolleyCount`; damage via `DamageEnemy(..., flat: true)` so no Might, no crit, no rng, and the boss takes it too. Knockback 0. `StrikeLanded` id 72 (Value 0, Point = enemy) per hit, emitted before the damage; `WeaponFired` Extra = hits. Cooldown kept when no enemy is in range.
- Pool-added weapons use their existing code unchanged; barrier for the Mage is the single Barrier of that pool. Names are exactly as in the table.

### What the Unity viewer needs (View/ML not edited)
- Icons for 69-72 (HUD, level-up cards; Vietnamese names are in the catalog). `SoundCueMap.WeaponCue` has no case for `Trio`, `Quad`, `Stone`.
- 69: explosion at each `StrikeLanded` id 69 (Value = radius 1.8, Point = centre), fireball falling optional. 70: three small bolts from projectiles with `SourceIndex` 70. 71: four-way bolts, `SourceIndex` 71 (projectile sprite lookup by index). 72: instant stone/spark on each `StrikeLanded` id 72 at Point (draw a line from the hero or a hit flash), number popups already come from `DamageDealt`.
- Per-index sprite tables or ML item lists (`SurvivorEpisodeStats`) should be checked for 69-72 (not looked at).

### Not verified / weak tests
- Windows goldens and Unity EditMode not run; Release `Step` time (0.05 ms) not measured (new code is O(enemies) per cast; the stone is O(count x enemies); `Performance_LateGame` passes).
- Weak: "rng is never drawn when the weapon is unowned" is proved by the bit-identical run against `OldRules` (1500 ticks, Mage and Archer), not by counting draws; "stone consumes no rng" is only implied by `flat` (the crit test shows no `Crit` event at 100% crit chance). The allocation test is a 1500-tick full-kit run asserting 0 bytes (it proves no leak; the "every weapon acts" test, which excludes the barrier because it only emits events on break, shows each weapon fires). The pool-full test for the quad only checks safety (pool <= 128, no crash), not exact counts. The nova knockback test only checks the target moved outward. Pool-added weapon tests (flame cone, barrier, poison pool, bomb, boomerang for the new class) check the main behaviour, not every number again. Balance numbers are starting points, untuned.
