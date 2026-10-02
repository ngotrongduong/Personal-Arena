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
