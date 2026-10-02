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

## Report (fill in when done). Include the FINAL list of weapons per class pool, for T-045.
