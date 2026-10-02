# T-036: M9 wave 1a — Warrior content, 6 + 6 slots (no schema change)

- **Milestone:** M9 (content wave 1; plan in `docs/CONTENT-PROPOSAL.md`)
- **Owner:** Claude subagent (`core-sim-engineer`), Claude reviews and does git
- **Branch:** `claude/serene-sagan-3a70q5` (do NOT run any git command; Claude commits)
- **Depends on:** M8. Observation schema v4 (2264) and actions 9/5/5 MUST NOT change.

## Goal (owner view)

The game plays more like Vampire Survivors: the hero carries **6 weapons and 6 passives** (was 4 + 4), and the
Warrior gets more weapons, passives and one more active skill, so builds differ a lot between runs. The owner
accepts that rule changes make brains retrain; they only want a complete, faithful game.

## Scope

Core only (`Unity/Assets/Arena/Core`, `CoreTests`). Do not edit `Unity/Assets/Arena/View` or `ML`; list in the
Report anything the viewer needs (icons, models, effects, HUD layout for 6 + 6 slots).
Dotnet is at `/root/.dotnet` (`export PATH=$PATH:/root/.dotnet`); run `dotnet test CoreTests -c Release`.

### 1. Slots 4 + 4 → 6 + 6
`SurvivorCatalog.MaxWeapons` and `MaxPassives` become 6. Inventory and offer observations are indexed by catalog
slot, so the schema is unchanged; verify that (`SurvivorObservation`, `Trainer/schemas/survivor_v4.json`).
Fix every test and place that assumed 4. Evolutions, chest logic and offers must keep working with 6 slots.

### 2. New Warrior weapons (catalog 26–30, new Vietnamese names; numbers are starting points)

| Index | Id | Name | Behaviour | Level 1 → per level |
|---|---|---|---|---|
| 26 | `flame-cone` | Phun lửa | Sweep with a narrow cone (60°), short range, very fast cooldown, no knockback | 6 dmg (+2), range 3.2 m, cooldown 0.35 s |
| 27 | `combo-blade` | Kiếm liên hoàn | A sweep that strikes 3 times in a row (0.25 s apart, 90° arc); the third hit does ×2 | 14 dmg (+5), range 2.6 m, cooldown 1.6 s |
| 28 | `heavy-hammer` | Búa nặng | Thrown, slow, huge, high damage, pierces 3 | 60 dmg (+15), range 8 m, radius 0.8, speed 8, cooldown 2.6 s, counts 1,1,1,2,2 |
| 29 | `bomb` | Bom | Thrown at the nearest enemy; on hit or at max range it explodes in an area (2 m) for the full damage | 30 dmg (+9), cooldown 2 s |
| 30 | `retaliate` | Phản đòn | Passive weapon: whenever the hero takes damage, it deals damage in a 2.5 m ring around the hero (internal cooldown 1 s) | 20 dmg (+8) |

Use existing `WeaponPattern`s where possible (flame-cone = Sweep config, heavy-hammer = Thrown config) and add a
pattern only when needed (combo, bomb explosion, retaliate). Add them to Warrior `WeaponPool`
(`{0,1,2,3,4,5,26,27,28,29,30}`). Each weapon: max level 5, deterministic, **allocation-free `Step`**.
Note the existing rule: a class holds at most one Orbit, one Aura and one Shockwave (sim keeps one state each); the
new weapons must not break it. No evolutions for these yet (slots 40–57 stay as they are).

### 3. New passives (catalog 58–61, usable by all three classes, added to every class `PassivePool`)

| Index | Id | Name | Effect per level |
|---|---|---|---|
| 58 | `recovery` | Hồi phục | +0.2 HP/s (`StatId.Regen`) |
| 59 | `clover` | Cỏ may mắn | +Luck (use the existing Luck stat scale) |
| 60 | `greed` | Tham lam | +10% gold (`StatId.Greed`) |
| 61 | `crown` | Vương miện | +8% EXP (`StatId.Growth`) |

The stats already exist (`StatId.Regen/Luck/Greed/Growth`, fed from owner stat points); wire the passive levels
into the same stat totals. Check `SurvivorCatalog.PassivePerLevel` and how the 8 existing passives are applied.
The two fillers stay at 62 and 63.

### 4. New Warrior skill (4th active slot, currently "none")
`war-cry` / Tiếng thét: `SkillKind.AreaBurst` (as the Mage `frost-burst`): radius 3.5 m, 15 damage, 0.8 s stun,
knockback 2, cooldown 10 s, energy cost 20. Warrior only. The action layout (5 choices on the skill branch)
does not change; check the action mask and observation for the Warrior's 4th skill.

## Hard rules
- Pure C# 9, no UnityEngine, no LINQ in hot paths, deterministic RNG only, no new allocations in `Step`,
  `SurvivorObservation.Write`, `WriteMask`. `TreatWarningsAsErrors` is on.
- **Goldens.** `SurvivorM5GoldenTests` (and `SurvivorM7Tests`) pin old behaviour bit for bit and are recorded on
  Windows (two of them are skipped on Linux). Do **not** re-record them. Extend the `PreM8` helper (or add a
  sibling) so those tests build the OLD rules: old pools (no new weapons/passives, no 4th Warrior skill) and old
  slot limits 4 + 4 (make the limits a config or tuning value instead of a const if needed, default 6). The
  Linux-running goldens (`Tier1_ScriptedRun…`, `Tier1_EvaluatorRun…`) must pass unchanged: that proves the restore.
- Every new weapon, passive and the skill needs tests: it fires/does what the table says, scaling per level,
  determinism with the new content, 0 bytes allocated, passives change the stat, 6 slots fill and a 7th item is
  never offered, offers still never exceed the pool.
- Do not touch any file outside `Unity/Assets/Arena/Core`, `CoreTests`, and this task file. Add a `.meta` file next to
  every new Unity asset (none expected inside Core: check `.meta` for new `.cs` files).

## Report (fill in when done)
