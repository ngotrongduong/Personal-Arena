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

**Status: review.** `dotnet test CoreTests -c Release`: 286 passed, 0 failed (237 before; 49 new tests in
`CoreTests/Survivor/SurvivorM9WarriorTests.cs`). NUnit reports 0 skipped on Linux; the two Windows-only goldens
use `Assume`, so I could not confirm from the summary whether they ran or were counted inconclusive. The Linux
goldens (`Tier1_ScriptedRun…`, `Tier1_EvaluatorRun…`) pass with their old constants, unchanged.

### What changed, per file (all under `Unity/Assets/Arena/Core/Survivor/` unless noted)
- `SurvivorDefs.cs`: `WeaponPattern` gains `Combo`, `Bomb`, `Retaliate`. `MaxWeapons`/`MaxPassives` = 6. Constants
  for the new indices, `ComboHits` (3), `ComboHitInterval` (0.25), `ComboFinisherMul` (2). Catalog rows 26-30 and 58-61
  (also in `Get`), `PassivePerLevel` for 58-61. Warrior `WeaponPool` {0..5,26..30}; every class `PassivePool` +58..61.
  Warrior skill 4 = `war-cry` (AreaBurst, radius 3.5, 15 dmg, stun 0.8, knockback 2, cooldown 10, energy 20).
- `SurvivorConfig.cs` (`SurvivorTuning`): new `MaxWeaponSlots` / `MaxPassiveSlots` (default 6, `Validate` requires 1..6).
- `SurvivorSim.Progression.cs`: `OpenOffer` uses the tuning slot caps instead of the old consts.
- `SurvivorSim.cs`: `RecomputeStats` adds the passive levels to Regen, Luck, GreedMul, GrowthMul (added after the
  stat-point term, so a hero without them is bit-identical); `Step` calls `ResolveRetaliate()` after body collisions;
  test hook `RequestRetaliateForTests`.
- `SurvivorSim.Enemies.cs`: `ApplyHeroDamage` sets `retaliatePending` (covers contact, melee, explosion and enemy projectiles).
- `SurvivorSim.Content.cs`: `UpdateCombo`, `ThrowBomb`, `ResolveRetaliate`; combo/retaliate state reset in `ResetContentState`;
  `LaunchProjectile` gets an optional `explodeOnExpire` argument.
- `SurvivorSim.Weapons.cs`: `FireWeapons` dispatches Combo/Bomb and skips Retaliate; `Sweep` takes a `damageMul`
  (default 1, so old sweeps are bit-identical); a projectile flagged `ExplodeOnExpire` blasts when its range or the map edge ends it;
  `NewProjectile` resets the flag.
- `SurvivorEntities.cs`: projectile `HitIds` 3 -> 4 (room for pierce 3) and `ExplodeOnExpire`; comment fix.
- Tests: `SurvivorM9WarriorTests.cs` (new); `SurvivorTestHelpers.OldRules/OldConfig` (4+4 slots, old pools, no war-cry);
  `SurvivorM5GoldenTests.PreM8` now also calls `OldRules`; the old-behaviour tests that assumed 4+4, the old pools or an
  empty 4th skill now use `OldConfig` (`SurvivorM4CContentTests`, `SurvivorProgressionTests`, `SurvivorM5GoldTests`,
  `SurvivorObservationTests`, `SurvivorSimulationTests`); pool assertions in `SurvivorDefsTests`, `SurvivorM4CContentTests`,
  `SurvivorM7Tests` updated to the new pools. No golden constant was re-recorded.

### Decisions on unspecified details
- Slot caps live in `SurvivorTuning` (like the M8 numbers); catalog consts stay as the hard maximum (6).
- Flame cone is a plain `Sweep` row (60 deg, 3.2 m, 0.35 s, no knockback, no back arc, no range growth).
- Combo blade: first strike on cooldown ready, then 2 more at 0.25 s steps along the facing at that moment; cooldown (1.6 s x
  cooldown multiplier) starts with the first strike; the strikes use `Sweep` (90 deg, 2.6 m, knockback 0.3, no range growth).
  `WeaponFired` carries `Extra` = strike number 1..3. A swing in progress finishes.
- Heavy hammer: `Pierce = 3` means 3 extra enemies, so 4 enemies per throw (existing meaning of `Pierce`). Knockback 1.2.
- Bomb: aims at the nearest enemy within 9 m, speed 9, projectile radius 0.3, flight range 9 m, blast radius 2 m x area, knockback 1,
  no stun, one bomb per volley at every level. Blast emits `StrikeLanded` (Value = radius, Id = 29) and also at max range / map edge.
- Retaliate: radius 2.5 m x area, knockback 1, internal cooldown 1 s x cooldown multiplier (stored in the weapon cooldown slot).
  It triggers on any hero HP loss, including blocked hits, not on a parried hit and not on the killing blow. It is resolved after
  the enemy phase of the same tick (so no enemy dies inside the enemy loop). Events: `WeaponFired` (id 30) + `StrikeLanded`
  (Value = radius, Id = 30, Point = hero).
- Clover = +5 Luck per level (one stat point per level, `StatInfo.PerPoint(Luck)`); Luck feeds the 4th-offer chance and gold chance as before.
- Recovery +0.2 HP/s, Greed +0.1 GreedMul, Crown +0.08 GrowthMul per level, additive with owner stat points.
- No change to the observation, `SurvivorActionMask` or `survivor_v4.json`: the mask and the observation already read the Warrior's 4th skill
  generically (verified by tests: `skill_allowed_3`, `skill_cooldown_3`, mask false on cooldown/low energy). New item ids use reserved inventory/offer slots.

### What the Unity viewer needs (nothing was edited in View/ML)
- HUD: 6 weapon + 6 passive slots (inventory arrays hold more; `Inventory.WeaponCount/PassiveCount` now go up to 6). Level-up cards must name/icon items 26-30 and 58-61 (Vietnamese names are in the catalog).
- Icons: 5 weapons (flame cone, combo blade, heavy hammer, bomb, retaliate), 4 passives (recovery, clover, greed, crown), 1 skill (war-cry) in the 4th skill button.
- Effects/models: flame-cone fan/cone (60 deg, 3.2 m); combo blade 3 slashes (use `WeaponFired.Extra` 1..3, third bigger); heavy hammer projectile (radius 0.8, slow, pierces 4);
  bomb projectile + explosion (`StrikeLanded` with Id 29, Value = radius 2); retaliate ring (`StrikeLanded` with Id 30, Value = radius); war-cry shout ring (`StrikeLanded` Id -1, Value 3.5; same event as the Mage frost burst).
- `View/Audio/SoundCueMap.cs` switches on `WeaponPattern` and has no cue for `Combo`, `Bomb`, `Retaliate`; `ProjectileLookOf` for ids 28/29 and any per-index tables
  in the view (sprites by catalog index, `SurvivorEpisodeStats` item lists in ML) should be checked. Not verified, I did not open the View code.
- Brains: rule changes only (no schema bump); the Warrior's skill branch value 4 is now usable, so old Warrior brains will start using it only after retraining.

### Not verified / open points
- Windows-only goldens (`Tier1_InvulnerableRun_First300Seconds…`, `Tier1_InvulnerableFullRun…`) were not run for real here; they rely on `OldRules` giving the old sim bit for bit.
  The reasoning: same RNG calls under the old pools, the added stat terms are `+ 0f`, `Sweep` multiplies by 1f. The Linux goldens passing supports this.
- Release `Step` time budget (0.05 ms) was not measured; the new weapons add only O(enemies) loops; `Performance_LateGame` passes.
- Balance numbers are the task's starting points; none were tuned. Unity EditMode tests and the View were not run.
