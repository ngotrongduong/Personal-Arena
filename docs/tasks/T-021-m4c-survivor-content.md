# T-021: M4C Core — full Survivor content (4 weapons, 4 passives, Spitter, magnet, chest)

- **Owner:** Codex
- **Status:** doing
- **Milestone:** M4C
- **Parallel OK with:** none (T-022 Unity view starts after this merges)
- **Depends on:** T-014, T-018, T-019 (all merged)

> Claude (the orchestrator) has full decision authority; never ask the user anything; work until
> the task is done. Follow `AGENTS.md`. Do not run git commands. Do not touch files outside the
> list below. When a detail is not specified, pick the simplest deterministic option, and write
> it in the Report.

## Goal

M4A built a vertical slice of the Survivor mode with room left in the data layout for the rest of
the content (design: `docs/GDD.md` §2.5–2.11, the "M4C" rows). This task fills that room in the
**pure C# simulation**:

- 4 new auto weapons: spear thrust, orbiting axes, aura, shockwave;
- 4 new passives: might gauntlet, hourglass, area charm, magnet charm;
- the Warrior pools grow to 6 weapons and 8 passives;
- the **Spitter** enemy (type 3) with **enemy projectiles** that the shield blocks;
- the **magnet** pickup (pulls every gem on the map) and the **chest** (elites drop it).

**The hard rule of this task: observation schema v4 does not change.** Size stays 2264, every
value stays in [−1, 1], every offset keeps its meaning, and `Trainer/schemas/survivor_v4.json`
still matches (`SurvivorObservationSchemaTests` stays green without edits to the JSON). The new
content only uses slots that v4 reserved for it: catalog rows 1, 2, 4, 5, 8, 10, 11, 13; enemy
type slot 3; the ray solid kind "enemy projectile" (offset 10); pickup kinds chest (4) and magnet
(5). This is what lets the running brain `warrior-s001` keep learning instead of starting over.
The action space (9/5/5) and the reward config do not change either.

## Files

- Edit under `Unity/Assets/Arena/Core/Survivor/` (namespace `PersonalArena.Core.Survivor`):
  `SurvivorDefs.cs`, `SurvivorEntities.cs`, `SurvivorSim.cs`, `SurvivorSim.Weapons.cs`,
  `SurvivorSim.Enemies.cs`, `SurvivorSim.Progression.cs`, `SurvivorObservation.cs`, and
  `SurvivorBehaviorTracker.cs` / `SurvivorEvaluator.cs` only if something there must follow.
  You may add new partial files (for example `SurvivorSim.Content.cs`,
  `SurvivorSim.EnemyProjectiles.cs`) to keep each file under ~600 lines. Every new `.cs` needs a
  `.meta` (copy a neighbour's format, fresh random 32-hex guid).
- Tests: edit/add `CoreTests/Survivor/*.cs`.
- This file's Report section.
- Nothing under `Unity/Assets/Arena/ML`, `View`, `Editor`, `Trainer/`, `Tools/` or other `docs/`
  changes. (The Unity view of the new content is T-022, done by Claude after this merges.)

## Spec

All numbers are starting balance; keep every tunable number in the defs / `SurvivorTuning`, not
as literals in the logic. "L" is the item level (1..5). `AreaMul`, `CooldownMul`, `Might` are the
existing derived stats; `Might` and crit are already applied inside `DamageEnemy`.

### 1. Catalog rows (`SurvivorCatalog.Get`)

Add the 8 rows with the ids/names below. `ItemDef` may get new init-only properties where the
existing ones do not fit (suggested: `Duration`, `HitInterval`, `AngularSpeedDegrees`,
`CooldownPerLevel`, `Width`). Keep the existing rows 0, 3, 6, 7, 9, 12, 62, 63 unchanged.

| Index | Id | Name | Kind |
|---|---|---|---|
| 1 | `spear-thrust` | Giáo đâm | Weapon, `Thrust` |
| 2 | `orbit-axe` | Rìu xoay | Weapon, `Orbit` |
| 4 | `aura` | Hào quang | Weapon, `Aura` |
| 5 | `shockwave` | Sóng chấn động | Weapon, `Shockwave` |
| 8 | `might-gauntlet` | Găng sức mạnh | Passive |
| 10 | `hourglass` | Đồng hồ cát | Passive |
| 11 | `area-charm` | Bùa vùng | Passive |
| 13 | `magnet-charm` | Nam châm | Passive |

The four passives use the existing `Passive(...)` helper; their effect formulas already exist in
`PassivePerLevel` and `RecomputeStats` (do not change them). All items have `MaxLevel = 5`.

`SurvivorDefaults.Warrior()`: `WeaponPool = {0, 1, 2, 3, 4, 5}`,
`PassivePool = {6, 7, 8, 9, 10, 11, 12, 13}`. Starting weapon stays 0. The 4 + 4 slot limits stay.

### 2. Spear thrust (index 1, `Thrust`)

- Damage `30 + 10·(L−1)`, cooldown `1.8 · CooldownMul`, knockback 0.3 along the spear direction.
- Length `5 · AreaMul`, half-width `0.3 · AreaMul`.
- Spears per volley by level: `{1, 1, 2, 2, 3}`.
- Fires only when the nearest active enemy is within `length + enemy.Radius` of the hero
  (otherwise the cooldown does not start). Aim = unit vector from the hero to that enemy.
- Spear `k` of `n` points at `aim` rotated by `(k − (n−1)/2) · 20°` (a fan). Put the rotation in
  a public static helper `SurvivorCatalog.ThrustAngleOffset(int k, int n)` (radians) so the view
  draws the same fan.
- A spear is instant (no projectile): it hits **every** enemy whose circle intersects the segment
  from the hero along its direction with length `length` (capsule of half-width `halfWidth`).
  Pierces all. An enemy hit by two spears of the same volley takes both hits.
- Event: one `WeaponFired` per volley, `id = 1`, `point = aim`, `extra = n`.

### 3. Orbiting axes (index 2, `Orbit`)

- Axes by level: `{1, 2, 2, 3, 3}`. Orbit radius `2.2 · AreaMul`, axe hit radius
  `0.5 · AreaMul`, angular speed 300°/s (counter-clockwise), duration 4 s.
- Damage `12 + 4·(L−1)` per touch, knockback 0.4 away from the hero.
- A volley starts when the weapon's cooldown is 0 (it does not need a target). While a volley is
  active the cooldown does not tick down; when the 4 s end, the cooldown is set to
  `3 · CooldownMul` (so the axes are up 4 s, then off 3 s × CooldownMul).
- Axe `k` of `n` sits at angle `orbitAngle + k · 2π / n`; `orbitAngle` starts at the hero facing
  when the volley starts and advances each tick. Positions follow the hero.
- The same enemy can be damaged by this weapon at most once per 0.5 s (any axe). Track it with a
  per-enemy internal field (for example `OrbitNextHitTime`, absolute sim time, reset on spawn),
  no allocations.
- Public read-only state for the view: `int OrbitAxeCount` (0 when no volley is active),
  `Vec2 GetOrbitAxePosition(int k)`, `float OrbitAxeRadius`.
- Event: `WeaponFired`, `id = 2`, `extra = n` when a volley starts.

### 4. Aura (index 4, `Aura`)

- Always on while owned. Radius `1.8 · (1 + 0.1·(L−1)) · AreaMul`.
- Every `0.4 · CooldownMul` s it damages every enemy whose circle overlaps the aura:
  `5 + 2·(L−1)`, and pushes it 0.15 m away from the hero.
- Public read-only `float AuraRadius` (0 when not owned) for the view.
- Event: `WeaponFired`, `id = 4` on each damage tick.

### 5. Shockwave (index 5, `Shockwave`)

- Damage `18 + 6·(L−1)`, knockback 2.0 away from the hero, max radius `5 · AreaMul`, ring speed
  10 m/s (so a full wave takes 0.5 s at area 1).
- Cooldown `(4 − 0.3·(L−1)) · CooldownMul`, started when the wave is emitted.
- Fires only when at least one active enemy is within `maxRadius + enemy.Radius` (otherwise the
  cooldown does not start).
- The wave is centred on the hero position **at emission** and grows each tick; every enemy is hit
  at most once per wave, on the first tick where `distance(center, enemy) ≤ radius + enemy.Radius`.
  Track it with a per-enemy internal wave id. Only one wave exists at a time.
- Public read-only `float ShockwaveRadius` (0 when no wave), `Vec2 ShockwaveCenter`,
  `float ShockwaveMaxRadius` for the view.
- Event: `WeaponFired`, `id = 5`, `point = center` when emitted.

### 6. Spitter (enemy type 3) and enemy projectiles

- Enemy def (type 3, id `spitter`): HP 20, speed 2.4, radius 0.4, mass 1, XP 2, knockback resist
  0, turn 360°/s. New `SurvivorAttackKind.Ranged`. Attack damage 10 (scaled by time, tier and
  elite exactly like other enemies, i.e. `e.Damage`). Add def properties as needed, suggested:
  `PreferredDistance = 7`, `AttackRange = 9` (fire range), `WindupSeconds = 0.5`,
  `RecoverSeconds = 2.5`, `AttackArcDegrees = 60`, `ProjectileSpeed = 7`,
  `ProjectileRadius = 0.3`, `ProjectileRange = 12`.
- Movement (when not stunned and not winding up): distance `d` to the hero; `d > 8` → move toward
  the hero; `d < 6` → move straight away; otherwise stand still. Turning uses the existing turn
  rule.
- Attack: when `AttackCooldown ≤ 0`, `d ≤ AttackRange + Hero.Radius` and the hero is in the
  attack arc, start the windup (existing `WindupRemaining`, so the ray "winding up" flag shows
  it). At the end of the windup it spits **one** projectile from
  `e.Position + dir · (e.Radius + 0.1)` toward the hero's **current** position (no leading), then
  `AttackCooldown = RecoverSeconds`. A stun cancels the windup (existing rule).
- Enemy projectiles live in their **own pool** (new class `SurvivorEnemyProjectile`: `Active`,
  `Id`, `Position`, `Velocity`, `Radius`, `Damage`, `Lifetime`, `SourceId`), capacity 64
  (`EnemyProjectileCapacity`), exposed read-only as `EnemyProjectiles` + `EnemyProjectileLimit`
  like the other pools. Cleared on reset. No allocations.
- Update order in `Step`: after `UpdateEnemies` (so a spit fired this tick moves next tick), move
  every enemy projectile, then:
  - it dies when its lifetime ends, it leaves the map, or it overlaps an obstacle;
  - when it overlaps the hero: if `Hero.Blocking` and the projectile is inside the hero's front
    180° (`InArc(Hero.Facing, p.Position − Hero.Position, 180°)`) it is destroyed with **no
    damage** and a `Blocked` event (`id = SourceId`); otherwise it deals `p.Damage` through the
    same armour / min-1 / HP / `HeroDamaged` path as other hits (refactor `DamageHero` so both
    paths share it; no parry and no stagger for projectiles). Either way the projectile is gone.
  - Dash does not dodge projectiles (same as other damage today).
- If a projectile kills the hero, `DeathCause = Projectile` unless the existing
  "surrounded" rule applies first (same precedence as today).
- Spawn schedule: the reserved 4th weight column becomes `0, 0, 0, 1, 2, 2, 3` for the phases
  0–1, 1–3, 3–5, 5–7, 7–10, 10–12, 12–15 min. The elite rule (type with the highest weight, ties
  to the later type) stays as it is, so the 12-min elite becomes a Spitter; that is intended.

### 7. Magnet pickup

- A **normal** (not elite, not boss) enemy that dies drops a magnet with chance 0.002 (tuning
  `MagnetChance`, not scaled by luck), rolled after the meat roll, placed at the enemy position +
  `FromAngle(MagnetDropAngle) · DropOffset` (pick an angle distinct from gold/meat).
- Collected like other pickups (pickup radius attraction, collect margin). On collect: every
  active gem on the map gets `Attracted = true` (they fly to the hero with the normal fly speed);
  event `MagnetPicked` with `value = number of gems attracted`.

### 8. Chest

- Every elite that dies also drops a chest (`PickupKind.Chest`) at its position +
  `FromAngle(ChestDropAngle) · DropOffset` (distinct from the gold point). Elites keep their gold
  bag and 50-XP gem.
- On collect:
  - pick uniformly (with `rng`) one **owned** weapon or passive whose level is below its
    `MaxLevel`, and raise it by 1 level (`inventory.Set` + `RecomputeStats(true)`; the weapon
    cooldown is not reset). If every owned item is at max, no item changes.
  - gold `floor(rng.Range(ChestGoldMin = 50, ChestGoldMax = 150)) · TierGold · GreedMul`, added to
    `Gold` with a `GoldCollected` event (so the existing gold reward applies).
  - event `ChestOpened`: `value = gold`, `id = item index` (−1 when none), `extra = new level`
    (0 when none), `point = chest position`.
- Chest and magnet do **not** count in `DropsSpawned` / `DropsCollected` (those stay gold + meat
  so the behaviour metrics keep their meaning).

### 9. Observation (schema v4, no layout change)

- Pickups: chest and magnet already map to pickup one-hot offsets `17 + 4` and `17 + 5` through
  `(int)pickup.Kind`; check it with a test.
- Enemy projectiles become solids of kind **10** ("enemy projectile") in the ray pass, with hit
  radius `max(p.Radius, 0.5)` (so a small spit is not lost between 5° rays), nearest-hit rule as
  for every other solid. For a projectile hit: elite / winding-up / stunned flags are 0 and the
  approach speed is `dot(p.Velocity − heroVel, unit(heroPos − p.Position)) / 10` (positive =
  closing in). Walls, obstacles and enemies keep their current formulas.
- The Spitter uses enemy type slot 3 (solid kind `2 + 3 = 5`) through the existing code.
- The density block does not include projectiles. Keep `Write` allocation-free.

### 10. Rewards, behaviour tracker, evaluator

- No change to `SurvivorRewardConfig` or its weights. New damage flows through `DamageDealt`,
  chest gold through `GoldCollected`.
- If `SurvivorBehaviorTracker` counts blocks, a projectile block counts as a block (it emits
  `Blocked`). Add nothing else unless a test needs it; list any change in the Report.

### 11. Determinism, allocations, performance

- Same seed + same inputs → identical run (all new randomness through the sim `rng`).
- `Step` and `SurvivorObservation.Write` allocate nothing (existing allocation tests stay green;
  add one with all 6 weapons owned and spitters + projectiles alive).
- The existing performance test must still pass in **Release** (CI runs Release). If the new
  weapons make it slower, optimise (use the spatial hash like `UpdateProjectiles`), do not relax
  the budget.

### 12. Test hooks

`internal` hooks are fine, e.g. `GrantItemForTests(int index, int level)`,
`SpawnPickupForTests(PickupKind kind, Vec2 point, float value)`,
`SpawnEnemyProjectileForTests(...)`. `SpawnEnemyForTests` already exists.

## Tests (CoreTests/Survivor)

At least one focused test per behaviour:

- Catalog: the 8 new rows exist with the numbers above; Warrior pools are `{0..5}` and `{6..13}`;
  offers can contain every new item and still respect 4 + 4 slots and max level 5. Update any old
  test that asserted the M4A pools / null rows.
- Spear: pierces a line of 3 enemies; misses an enemy outside the half-width; fan by level (1/2/3
  spears, angles via `ThrustAngleOffset`); cooldown `1.8 · CooldownMul`; no fire and no cooldown
  without a target in range; area scales length.
- Axes: count by level; 4 s up then `3 · CooldownMul` off; an enemy standing on the orbit takes a
  hit at most once per 0.5 s; damage formula; area scales orbit radius.
- Aura: tick every `0.4 · CooldownMul` s; radius formula with level and area; push away.
- Shockwave: expands, hits each enemy once per wave, knockback, level-5 cooldown `2.8 · CooldownMul`;
  no fire without an enemy in range.
- Passives: might gauntlet raises dealt damage (+8%/level), hourglass lowers a weapon cooldown
  (−6%/level), area charm widens the sweep, magnet charm widens pickup radius.
- Spitter: approaches from far, backs off when close, holds in the 6–8 m band; winds up then
  fires; projectile damage `10` at minute 0 tier 1 minus armour; a front block takes 0 damage and
  emits `Blocked`; a block facing away does not help; an obstacle stops the projectile; a
  projectile kill gives `DeathCause.Projectile`; spawn weights per phase; XP gem value 2.
- Magnet: forced drop via hook, collect → all gems attracted and collected over the next ticks,
  `MagnetPicked` value = gem count; drop rate ≈ 0.2% over many kills (statistical test with a
  fixed seed is fine).
- Chest: elite kill drops a chest; collect raises exactly one owned non-max item by 1, gold within
  `[50, 150) · TierGold · GreedMul`, `GoldCollected` + `ChestOpened` emitted; everything maxed →
  gold only, `id = −1`.
- Observation: size still 2264 and in [−1, 1] with all content active; enemy projectile → solid
  kind offset 10 + approach speed sign; chest/magnet → pickup offsets 21/22;
  `SurvivorObservationSchemaTests` unchanged and green.
- Determinism with all 6 weapons + spitters + chest/magnet; allocation-free `Step`/`Write`;
  performance test (Release) green.
- Reward sign: opening a chest gives a positive reward (gold), a projectile hit a negative one.

## Done when

- From the worktree root: `dotnet test CoreTests -c Release` is green (use `--no-restore` if the
  sandbox blocks NuGet; say so in the Report). Debug build must at least compile.
- `Trainer/schemas/survivor_v4.json` is untouched and its test passes.
- The Report lists what changed, test counts, and every decision taken on an unspecified detail.

## Report

(Codex fills this in.)
