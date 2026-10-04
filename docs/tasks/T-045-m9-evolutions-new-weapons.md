# T-045: M9 evolutions for the new weapons (catalog 112–127)

- **Milestone:** M9 (D-041; evolution rule from D-038)
- **Owner:** Claude subagent (`core-sim-engineer`), Claude reviews and does git
- **Branch:** `claude/serene-sagan-3a70q5` (do NOT run any git command)
- **Depends on:** T-036..T-044 (read their Reports). Schema v5 stays as it is (catalog 128 already has room: 112–127).

## Goal (owner view)

Every new weapon that has a partner passive can evolve like in Vampire Survivors: a max-level weapon plus its
passive, then the next chest turns it into a stronger weapon (and a "TIẾN HÓA" notice). Today only the 18 original
weapons (catalog 40–57) can evolve.

## Rule (existing, D-038)
Weapon at max level + the paired passive at level ≥ 1 → the next elite chest evolves the weapon into the evolution
(×1.5 damage of level 5, ×1.2 range, ×0.8 cooldown) and the base weapon is not offered again. Look at
`SurvivorCatalog.Evolve(...)`, `EvolutionOf`, `Get`, and `SurvivorSim.Progression.cs` (chest logic, `EvolvedAway`).

## Work

1. Generalise the evolution table: the original 18 stay at 40–57, the new ones go to **112–127** (16 rows).
   `Get(index)`, `EvolutionOf`, `EvolvedAway`, the chest and offer logic, `ItemDef.EvolvesFrom/EvolutionPassive`,
   and anything iterating `FirstEvolutionIndex`/`EvolutionCount` must cover both ranges. Add new constants rather than
   renaming the existing ones (`FirstEvolutionIndex` 40 and `EvolutionCount` 18 keep their meaning for the first range).
   Existing catalog ids never move.
2. Add 16 evolutions (suggested pairing; you may change a pairing if it is clearly wrong for the weapon, but keep one
   evolution per weapon and note any change in the Report). Catalog 67 `purge` gets no evolution.

| Evolution index | Base weapon | Partner passive | Suggested id / Vietnamese name |
|---|---|---|---|
| 112 | 26 flame-cone (Phun lửa) | 58 recovery (Hồi phục) | `inferno` Hỏa ngục |
| 113 | 27 combo-blade (Kiếm liên hoàn) | 12 wind-boots (Ủng gió) | `phantom-blade` Kiếm vô ảnh |
| 114 | 28 heavy-hammer (Búa nặng) | 7 bone-armor (Giáp xương) | `mountain-hammer` Búa núi |
| 115 | 29 bomb (Bom) | 35 duplicator (Bộ nhân đôi) | `carpet-bomb` Mưa bom |
| 116 | 30 retaliate (Phản đòn) | 36 spiked-armor (Giáp phản) | `fury` Cơn thịnh nộ |
| 117 | 31 barrier (Vòng bảo hộ) | 6 iron-heart (Tim sắt) | `aegis` Kết giới bất diệt |
| 118 | 32 boomerang (Boomerang) | 10 hourglass (Đồng hồ cát) | `storm-boomerang` Boomerang bão |
| 119 | 33 poison-pool (Bình độc) | 34 duration-charm (Bùa thời gian) | `miasma` Đầm độc |
| 120 | 64 bounce-shot (Đạn nảy) | 59 clover (Cỏ may mắn) | `chaos-shot` Đạn hỗn loạn |
| 121 | 65 momentum-spirit (Bóng tốc) | 37 omni-box (Hộp tổng hợp) | `wraith` Bóng ma tốc độ |
| 122 | 66 time-clock (Đồng hồ băng) | 10 hourglass (Đồng hồ cát) | `eternal-corridor` Hành lang vĩnh cửu |
| 123 | 68 bomb-ring (Mưa bom vòng) | 11 area-charm (Bùa vùng) | `nebula` Tinh vân |
| 124 | 69 fireball-nova (Hỏa cầu nổ) | 8 might-gauntlet (Găng sức mạnh) | `meteor` Thiên thạch |
| 125 | 70 bracelet-trio (Vòng tay ba mũi) | 9 crit-eye (Mắt chí mạng) | `twin-bracelet` Vòng tay song |
| 126 | 71 quad-shot (Bắn bốn hướng) | 13 magnet-charm (Nam châm) | `four-winds` Tứ phương |
| 127 | 72 magi-stone (Đá tụ lực) | 60 greed (Tham lam) | `sage-stone` Đá hiền triết |

3. Behaviour of an evolution = the base weapon's behaviour with scaled numbers (damage, range, cooldown) by the generic
   rule. Where a pattern has a defining parameter that the generic scale does not cover, scale it too and document it:
   barrier +1 charge and ×0.8 recharge, retaliate ×1.5 radius, boomerang +1 count, poison pool ×1.3 duration and radius,
   bounce +2 bounces, bomb-ring +2 bombs, quad +1 projectile per direction, stone fixed damage ×1.5, trio +1 projectile,
   time-clock +0.5 s stun and +10% chance, momentum factor floor 0.4 (the damage multiplier starts at 0.8 when idle),
   flame-cone/combo/nova/heavy-hammer/bomb/boomerang just the generic scale. Keep `MaxLevel` semantics: an evolution is a
   level-1 item (as the existing ones: check how `InventoryValue` shows it).
4. The class pools do not list evolutions; the evolution replaces the base in the inventory (existing mechanism). Make sure a
   class that cannot own the base weapon cannot get its evolution, the chest picks evolutions of owned weapons only, and
   the 6 + 6 slot logic still holds.
5. Events: use the existing "evolved" event path so the viewer shows the notice (find how T-030 emits it).

## Hard rules
- Pure C# 9, deterministic, allocation-free `Step`/`Write`/`WriteMask`, `TreatWarningsAsErrors`.
- Old behaviour bit-identical: the evolution table only matters when a new weapon is owned. Old-rule configs keep
  the old pools (they exclude every new weapon), so nothing changes. The Linux goldens (`Tier1_ScriptedRun…`,
  `Tier1_EvaluatorRun…`) pass unchanged; re-record nothing.
- Only edit `Unity/Assets/Arena/Core`, `CoreTests` and this task file (new `.cs` need `.meta`); no Unity ML/View, no
  `Trainer/`. No git commands. `export PATH=$PATH:/root/.dotnet; dotnet test CoreTests -c Release`.
- Tests in a new file `CoreTests/Survivor/SurvivorM9EvolutionTests.cs`: the 16 rows (ids, names, pairings, base/passive
  links, catalog range, no overlap with the old range), `EvolutionOf` and `Get` for both ranges, a chest evolves each new weapon
  when the conditions hold and not otherwise (not max level, missing passive, class lacks the weapon), the base weapon is
  not offered after evolving, the evolution fires and has the documented parameter changes, the evolution notice event,
  determinism, zero allocation with several evolutions owned, and the observation inventory shows the evolution.

## Report (fill in when done)

**Status: review.** `dotnet test CoreTests -c Release`: 614 passed, 0 failed, 2 Windows-only goldens skipped (495 before; 119 new cases in `CoreTests/Survivor/SurvivorM9EvolutionTests.cs`). Linux goldens pass unchanged, nothing re-recorded. Schema stays v5. (Slip: I ran one read-only `git diff --stat` by mistake; no other git command.)

### Changes per file
- `Unity/Assets/Arena/Core/Survivor/SurvivorDefs.cs`: consts `FirstNewEvolutionIndex = 112`, `NewEvolutionCount = 16` (old `FirstEvolutionIndex` 40 / `EvolutionCount` 18 untouched). New table `NewEvolutions` (16 rows). `Get` and `EvolutionOf` cover both tables. `Evolve(...)` got optional parameters (defaults reproduce the old 18 exactly): `rangeMul`, `widthMul`, `durationMul`, `extraCount`, `extraBounces`, `stunBonus`, `chanceBonus`, `momentumFloor`; it now also folds `BouncesByLevel` (level-5 + bonus), `Chance` (level 5 + bonus) and, for a Freeze weapon, the stun (level 5 + bonus). New `ItemDef.MomentumFloor`.
- `SurvivorSim.GroupC.cs`: momentum damage uses `Max(movementFactor, def.MomentumFloor)` (floor 0 for every old weapon = unchanged). Bomb-ring pending blasts now remember their own knockback and source index (new `pendingBlastKnockback`, `pendingBlastSource`), so the evolution's `StrikeLanded` carries id 123 (not 68) and its ×1.2 knockback; for the base weapon the values are identical to before.
- `SurvivorEntities.cs`: `SurvivorProjectile.HitIds` 4 -> 8. With the old 4, mountain-hammer's pierce of 4 (5 hits) was silently capped at 4 hits. No old weapon can exceed 4 hits (heavy-hammer base: pierce 3), so old behaviour is unchanged.
- `SurvivorSim.Progression.cs` / chest / offer code needed NO change: `TryEvolve`, `EvolvedAway`, `OpenOffer`, `Inventory.Replace` and the `WeaponEvolved` event all go through `EvolutionOf`/`Get`, which now cover both ranges.
- Tests: new `SurvivorM9EvolutionTests.cs` (+ `.meta`). Edited four older tests that asserted `EvolutionOf(newWeapon) == -1` (`SurvivorM9WarriorTests`, `SurvivorM9WaveBTests`, `SurvivorM9MageArcherTests` dropped that clause; `SurvivorM9GroupCTests` now expects -1 only for purge 67 and >= 112 otherwise) and `SurvivorSchemaV5Tests` (asserted `Get(127)` is null; now asserts `Get(111)` and `Get(128)` are null).

### Pairings
All 16 suggested pairings kept unchanged. Note carpet-bomb (bomb + duplicator) is thematically weak because the bomb ignores the duplicator's +projectile; it still works as a requirement. Every partner passive is in all three classes' passive pools; every evolution is reachable by at least one class (Warrior 112-120, Mage 122-125/127 and 112/117/119/120, Archer 115/118/119/120/121/125/126).

### How each parameter scales (evolution = level-1 item, numbers folded from level 5)
- Generic: damage (base + perLevel x 4) x 1.5, range x 1.2, cooldown (base + cdPerLevel x 4) x 0.8, knockback x1.2, projectile radius/range x1.2, pierce +1 for projectile weapons, volley count = level-5 count + 1, width x1.2, duration x1.25, hit interval x0.8.
- 117 aegis: 3+1 = 4 charges, recharge 8 s x 0.8 = 6.4 s (generic). 116 fury: radius 2.5 x 1.5 = 3.75 (REPLACES the generic x1.2, it is not 1.2 x 1.5), cooldown 0.8 s, damage 78. 118 boomerang: 4 (generic +1), range 12 x 1.2 = 9.6 flight. 119 miasma: width x1.3 = 2.34, duration x1.3 = 4.55 s (replace the generic 1.2/1.25); hit interval 0.4 s, damage 19.5. 120 chaos-shot: bounces 5+2 = 7, count 4, flight 48 m. 121 wraith: damage x (0.4 + 1.2 x max(factor, 0.4)), see below. 122 corridor: stun 1.2+0.8+0.5 = 2.5 s, chance 0.3+0.4+0.1 = 0.8, cooldown (6-2) x 0.8 = 3.2 s, range 16.8. 123 nebula: 6+2 = 8 blasts (= `MaxPendingBlasts`, so with a duplicator it stays 8), ring 4.8, blast width 1.56, interval 0.12 s. 124 meteor, 112, 113, 114, 115: generic only (113 still swings 3 times, interval 0.2 s; 112 sweep 60 degrees; 114 pierce 4, count 3; 115 blast width 2.4). 125 twin-bracelet: 4 shots over 8 degrees. 126 four-winds: 4 per direction (16 shots), pierce 2. 127 sage-stone: fixed damage (20+80) x 1.5 = 150, 4 stones, range 12.

### Decisions on unspecified details
- Momentum: the task says "factor floor 0.4 (the damage multiplier starts at 0.8 when idle)"; those two do not agree with the existing formula (0.4 + 1.2 x factor gives 0.88 at factor 0.4, not 0.8). I implemented the literal "factor floor 0.4": idle multiplier = 0.88 (x damage 39 = 34.3). If 0.8 is wanted, change `MomentumFloor` to 0.333 (one number in `NewEvolutions`). The `WeaponFired.Value` still carries the raw movement factor, not the floored one.
- "x1.5 radius / x1.3 duration and radius" read as the final multiplier versus the base level-5 value, replacing the generic range/width/duration multiplier.
- Time-clock stun/chance bonuses are added to the level-5 values (2.0 s and 0.7), not to level 1.
- Evolutions are not in any class pool (existing rule); a class that cannot own the base can never own the evolution (chest only looks at owned weapons, offers never list level-1/max-1 items).

### What the Unity viewer needs (View/ML not edited)
- Icons/names for 112-127: names come from the catalog; `SkillIconFactory`/`SurvivorViewLogic` already treat any `EvolvesFrom >= 0` as an evolution (gold frame, x1.3 scale, "TIẾN HÓA: <name>" toast through `WeaponEvolved`), but the existing View tests only loop the old range. Check `SurvivorViewLogic.EvolutionDescription`, `SoundCueMap.WeaponCue` (already maps by base via `EvolvesFrom`, but base patterns Trio/Quad/Stone/Bounce etc. may lack cases) and per-index sprite tables for 112-127.
- Effects: events use the evolution's own id (`WeaponFired`, `StrikeLanded` id 123 for nebula, projectile `SourceIndex`, zones `SourceIndex` 119), so the viewer draws them like the base with the existing evolved look. Fury radius is the `StrikeLanded` Value (3.75), nebula blasts Value 1.56, miasma radius 2.34 via `SurvivorZone.Radius`.

### Not verified / weak tests
- Windows goldens, Unity EditMode and Release `Step` time (0.05 ms) not run. The new code adds only an array lookup to `Get`/`EvolutionOf` (a 34-row scan per `EvolutionOf` call, called per chest and per pool weapon in offers, not per tick).
- Weak: "evolution fires and deals damage" (16 cases) only checks that a `WeaponFired` event appears and total damage > 0 in an arena with four stunned brutes; it does not check each weapon's exact hit counts. The time-clock rate test is statistical (about 80%, asserted > 60% over 40 seeds x about 4 casts, deterministic seeds). The mountain-hammer pierce test asserts exactly 15 hits (3 hammers x 5) in a straight line of tough walkers, which depends on knockback not reordering them (it passes; fragile if hammer knockback changes). Zero-allocation: a 1500-tick run with all 16 evolutions owned plus a single chest-evolve tick measured after a JIT warm-up (the first chest evolution in a fresh process allocated 24 bytes, JIT only). Balance numbers are untuned starting points. Old behaviour is proved by the unchanged goldens and the existing "not owned = bit-identical to old rules" tests, not by a new test.
