# T-014: M4A Core — Survivor vertical slice (Warrior), observation v4, rewards, evaluator

- **Owner:** Codex
- **Status:** doing
- **Milestone:** M4A
- **Parallel OK with:** T-015 (Python only, no shared files)
- **Depends on:** none

> Claude (the orchestrator) has full decision authority; never ask the user anything; work until
> the task is done. Follow `AGENTS.md`. Do not run git commands. Do not touch files outside the
> list below. When a detail is not specified, pick the simplest deterministic option, and write
> it in the Report.

## Goal

The game becomes a Vampire Survivors–style mode (design: `docs/GDD.md` §2 and §4; decisions
D-026..D-030 in `docs/DECISIONS.md`). M4A is a **vertical slice**: a small but complete run the
agent can learn, with room in the data layout for the rest of the content (M4C). This task
builds the **pure C# simulation** of one run for the Warrior, in a new folder next to the old
arena:

- a 100 × 100 m map with walls and circular obstacles;
- a 15-minute spawn schedule with 3 enemy types (walker, runner, brute), elites and a boss;
- 2 auto-firing weapons (sword sweep, thrown hammer), 4 passives, 3 active skills the agent
  presses;
- XP gems, level-ups that **pause** the run and offer 3–4 choices the agent picks;
- pickups (gems, gold, meat);
- the build (13 stat points + difficulty tier) that changes the hero and the enemies;
- observation schema v4 (**2264 floats, every value in [−1, 1]**, with motion features), a strict
  action mask, a **reward hierarchy** (outcome > survival > HP > progress > gold), death causes;
- a brain pilot, a random-build generator, and an **evaluator** that plays many unseen seeds
  and reports survival statistics (the M4A acceptance test), plus a small console tool that
  runs it.

The old arena (`ArenaSim` and friends) stays untouched and keeps compiling. Claude removes it later
(T-017) after the Unity side switches. Nothing under `Unity/Assets/Arena/ML`, `View`, `Editor`,
`Trainer/` or `docs/` changes in this task (except this file's Report).

## Files

- Create everything under `Unity/Assets/Arena/Core/Survivor/` (namespace
  `PersonalArena.Core.Survivor`). Suggested split (you may merge or split further to keep each
  file readable, under ~600 lines):
  - `SurvivorDefs.cs` — catalog, item defs, enemy defs, stats, class def, spawn schedule,
    `SurvivorDefaults`.
  - `CharacterBuild.cs`, `BuildRandomizer.cs`, `XpCurve.cs`.
  - `SurvivorConfig.cs`, `SurvivorInput.cs`, `SurvivorEvent.cs`.
  - `SurvivorEntities.cs` — hero, enemy, projectile, pickup, obstacle state classes.
  - `SpatialHash.cs`.
  - `SurvivorSim.cs` (+ partial files such as `SurvivorSim.Weapons.cs`,
    `SurvivorSim.Enemies.cs`, `SurvivorSim.Pickups.cs`, `SurvivorSim.Progression.cs`).
  - `SurvivorObservation.cs`, `SurvivorActionMask.cs`.
  - `SurvivorRewardConfig.cs`, `SurvivorRewardCalculator.cs`.
  - `SurvivorPilot.cs`, `SurvivorEvaluator.cs`.
- Every new `.cs` needs a `.meta`, and the new folder needs `Unity/Assets/Arena/Core/Survivor.meta`.
  Copy the format of a neighbouring `.meta` (for the folder, a Unity folder meta with
  `folderAsset: yes`) and use a fresh random 32-hex-char guid for each.
- Tests: create `CoreTests/Survivor/*.cs`. `CoreTests.csproj` already compiles
  `Unity/Assets/Arena/Core/**/*.cs`, so it needs no change. `internal` members are visible to the
  tests (same assembly), so test hooks may be `internal`.
- Console tool: create `Tools/SurvivorEval/SurvivorEval.csproj` and `Tools/SurvivorEval/Program.cs`
  (§14). It compiles the Core sources the same way `CoreTests.csproj` does (same target
  framework, `TreatWarningsAsErrors`, `Compile Include` of `../../Unity/Assets/Arena/Core/**/*.cs`).
  Unity ignores folders outside `Assets`, so the tool needs no `.meta` files.
- You may reuse existing Core types: `Vec2`, `Rng`, `SkillDef`/`SkillKind`, `PolicyBrain`.
  Do not edit any existing Core file.

## Spec

All numbers are starting balance. Put every tunable number in the defs / config objects, not
inline in logic. Coordinates are world frame: the map spans `[-50, 50]` on both axes, the hero
starts at `(0, 0)`, angle 0 = +x, counter-clockwise (same convention as the old Core).
Fixed step `dt = 1/60 s`.

### 1. Catalog and definitions (`SurvivorDefs.cs`)

**Item catalog** — a global index `0..63` shared by all classes (observation one-hot uses it).
Indices are **stable forever**. M4A implements only the rows marked "M4A"; the other named rows
are reserved for M4C (their index is fixed now, `SurvivorCatalog.Get` returns `null` for them
in this task).

| Index | Id | Kind | Name (VN, for the HUD) | In M4A |
|---|---|---|---|---|
| 0 | `sword-sweep` | Weapon (Sweep) | Kiếm quét | yes |
| 1 | `spear-thrust` | Weapon (Thrust) | Giáo đâm | reserved |
| 2 | `orbit-axe` | Weapon (Orbit) | Rìu xoay | reserved |
| 3 | `thrown-hammer` | Weapon (Thrown) | Búa ném | yes |
| 4 | `aura` | Weapon (Aura) | Hào quang | reserved |
| 5 | `shockwave` | Weapon (Shockwave) | Sóng chấn động | reserved |
| 6 | `iron-heart` | Passive (+10% max HP / level) | Tim sắt | yes |
| 7 | `bone-armor` | Passive (+1 armor / level) | Giáp xương | yes |
| 8 | `might-gauntlet` | Passive (+8% damage / level) | Găng sức mạnh | reserved |
| 9 | `crit-eye` | Passive (+4% crit chance / level) | Mắt chí mạng | yes |
| 10 | `hourglass` | Passive (−6% weapon cooldown / level) | Đồng hồ cát | reserved |
| 11 | `area-charm` | Passive (+8% area / level) | Bùa vùng | reserved |
| 12 | `wind-boots` | Passive (+8% move speed / level) | Ủng gió | yes |
| 13 | `magnet-charm` | Passive (+25% pickup radius / level) | Nam châm | reserved |
| 14–61 | reserved | — | — | — |
| 62 | `bonus-gold` | Filler offer: +25 gold (× greed × tier gold) | +25 vàng | yes |
| 63 | `bonus-heal` | Filler offer: heal 30 HP | Hồi 30 máu | yes |

Types: `enum ItemKind { None, Weapon, Passive, Filler }`,
`enum WeaponPattern { None, Sweep, Thrust, Orbit, Thrown, Aura, Shockwave }` (all values declared
now, only Sweep and Thrown implemented), class `ItemDef` (`CatalogIndex`, `Id`, `Name`, `Kind`,
`Pattern`, `MaxLevel = 5`, plus the weapon numbers below).
`SurvivorCatalog.CatalogSize = 64`, `MaxWeapons = 4`, `MaxPassives = 4`,
`SurvivorCatalog.Get(int index)` (null for reserved).

The derived-stat formulas in §3 already include the reserved passives (their level is always 0
in M4A), so M4C only adds defs and weapon logic.

**Weapons** (level L = 1..5). "area" = the hero's area multiplier (§3), "cd" = the hero's
cooldown multiplier, damage goes through §4. A weapon that is ready fires at once; its cooldown
restarts when it fires.

| Weapon | Behaviour | Numbers |
|---|---|---|
| Sweep | Instant arc hit toward the hero's facing: every enemy whose centre is within `range + enemy radius` and within ±60° of the facing. At L5 also a second arc toward facing + 180°. Knockback 0.5 m. | damage 20 + 8(L−1); range 2.5 × (1 + 0.1(L−1)) × area; cooldown 1.2 × cd |
| Thrown | Throws N hammers, each at a random alive enemy within 10 m (distinct targets while possible). A hammer is a hero projectile: speed 12 m/s, radius `0.4 × area`, lifetime `12 / 12 s`, pierce 1 (it can hit 2 enemies, each once). If no enemy is within 10 m it does not fire and stays ready. Count 1/2/2/3/3. Knockback 0.5 m. Hero projectiles ignore obstacles. | damage 25 + 7(L−1); cooldown 1.5 × cd |

Knockback of every weapon and skill is multiplied by `(1 − enemy KnockbackResist)`.

**Enemy types** (`SurvivorEnemyDef`; `TypeIndex` is the observation slot, 8 slots):

| TypeIndex | Id | HP (min 0, tier 1) | Speed | Radius | Mass | Attack | XP | KB resist | In M4A |
|---|---|---|---|---|---|---|---|---|---|
| 0 | walker | 15 | 2.0 | 0.45 | 1 | Contact 8 | 1 | 0 | yes |
| 1 | runner | 10 | 4.0 | 0.4 | 1 | Contact 5 | 1 | 0 | yes |
| 2 | brute | 80 | 1.6 | 0.7 | 3 | Melee swing: range 1.8, arc 90°, windup 0.9 s, damage 25, recover 1.8 s | 5 | 0.6 | yes |
| 3 | spitter | — | — | — | — | (M4C) | — | — | reserved |
| 4 | boss (`bone-lord`) | 6000 | 1.4 | 1.75 | 1000 | Melee swing: range 3.5, arc 180°, windup 1.2 s, damage 40, recover 4.8 s (one swing per ~6 s); every 10 s summons 8 walkers on a 3 m ring around itself | 0 | 1.0 | yes |
| 5–7 | reserved | | | | | | | | |

- **Contact** attackers damage the hero when `distance ≤ heroR + enemyR + 0.1`, at most once per
  0.5 s per enemy (per-enemy timer).
- **Melee swing** attackers use a windup (telegraph) like the old zombies: they stop moving during
  the windup; a swing resolves at the end of the windup if the hero is still within
  `range + heroR` and the arc.
- Every enemy turns toward the hero (turn speed 360°/s; brute/boss 180°/s) and moves straight
  toward it. Stunned enemies do nothing and a stun cancels a windup.
- Every enemy keeps its **velocity** (`Vec2`, m/s, the displacement it actually made this tick /
  dt, knockback included); the observation uses it (§9).
- **Scaling at spawn time** (`t` = run seconds, `N` = tier): `hp = baseHp × (1 + 0.12·t/60) ×
  (1 + 0.35(N−1))`; `damage = baseDamage × (1 + 0.05·t/60) × (1 + 0.2(N−1))`. The boss uses only
  the tier factors (no time factor).
- **Elite** (flag on a normal enemy): radius × 1.6, mass × 3, HP × 10, damage × 1.5, KB resist
  max(own, 0.8), XP 50, always drops a gold burst (§6).

**Spawn schedule** (tier 1; data table, one row per phase). The weight columns are indexed by
TypeIndex so M4C can add spitter weights without changing the table shape:

| From (s) | To (s) | Weights (walker, runner, brute, spitter) | Max alive | Spawns / s |
|---|---|---|---|---|
| 0 | 60 | 1, 0, 0, 0 | 30 | 1 |
| 60 | 180 | 3, 1, 0, 0 | 60 | 2 |
| 180 | 300 | 3, 2, 1, 0 | 90 | 3 |
| 300 | 420 | 2, 2, 1, 0 | 120 | 4 |
| 420 | 600 | 2, 3, 1, 0 | 160 | 5 |
| 600 | 720 | 1, 3, 2, 0 | 200 | 6 |
| 720 | 900 | 1, 3, 3, 0 | 250 | 8 |

- Tier multiplies max alive and spawns/s by `1 + 0.15(N−1)`; max alive is capped by the enemy
  pool (400) minus room for boss summons (keep 20 free).
- Spawning: accumulate `spawnsPerSecond × dt`; while the accumulator ≥ 1 and alive normal enemies
  < max: pick a type by weight, pick a point on a ring of radius uniform 18–24 m around the hero at
  a uniform random angle; it must be inside the map (margin 1 m) and not overlap an obstacle. Try
  up to 8 angles; if all fail, drop that spawn. Subtract 1 either way.
- **Elites** at t = 180, 360, 540, 720 s: one elite of the phase's highest-weight type (ties →
  higher TypeIndex), spawned the same way (retry until a spot is found; after 32 failed tries
  place it at the nearest valid point).
- **Relocation:** a normal or elite enemy farther than 40 m from the hero is moved to a new ring
  point in front of the hero: angle = hero velocity angle ± uniform 60° if the hero moves, else
  uniform random. Checked once per second per enemy is enough (stagger by Id), or every tick if
  cheaper to write.
- **Boss:** only when `Config.RunSeconds ≥ 900`. At t = 900 normal spawning stops, the boss
  spawns on the ring and `BossSpawned` is emitted. Killing it wins the run (§7). **There is no
  reaper:** if the boss is still alive at `t = 1020` the run ends with `EndReason.Expired` (§7).

**Stats** (`enum StatId`, 16 slots, 13 used): 0 MaxHp, 1 Armor, 2 Regen, 3 Might, 4 Crit,
5 CritDamage, 6 Cooldown, 7 Area, 8 MoveSpeed, 9 Magnet, 10 Luck, 11 Greed, 12 Growth,
13–15 reserved. Per point / cap: MaxHp +5% / 20, Armor +0.5 / 10, Regen +0.1 HP/s / 10,
Might +3% / 20, Crit +2% / 20, CritDamage +10% / 20, Cooldown −2% / 15, Area +3% / 15,
MoveSpeed +2% / 10, Magnet +10% / 10, Luck +5 / 10, Greed +5% / 20, Growth +3% / 10.
Expose `StatInfo.PerPoint(StatId)`, `StatInfo.Cap(StatId)`, `StatInfo.UsedCount = 13`,
`StatInfo.SlotCount = 16`.

**`CharacterBuild`**: `int[16] Points`, `int Tier` (1..10), `Level` = sum of points (derived).
`Validate()` (throws on negative points, over-cap points, tier out of range), `Clone()`.

**`SurvivorClassDef`** and `SurvivorDefaults.Warrior()`:
- base stats: MaxHp 150, Regen 0.2/s, Armor 0, MoveSpeed 4.5, Acceleration 30, Radius 0.5,
  PickupRadius 1.5, CritChance 0.05, CritDamage 1.5, MaxEnergy 100, EnergyRegen 15/s,
  Mass 1;
- `StartingWeapon = 0` (sword-sweep L1), `WeaponPool = {0, 3}`, `PassivePool = {6, 7, 9, 12}`;
- `ActiveSkills` (4 slots, action = slot + 1), reusing `SkillDef`:
  - slot 0 `kick`: Kick, damage 10, range 1.8, arc 100°, stun 1.2 s, knockback 3, cooldown 3,
    energy 0;
  - slot 1 `shield-block`: Block, as the old Warrior (energy 10 + 20/s, move × 0.4,
    damage × 0.5, parry window 0.2 s, parry stun 1.5 s, stagger 0.6 s, pushback 0.8);
  - slot 2 `dash`: Dash, cooldown 2.5, energy 15, distance 3, speed 18;
  - slot 3: none (`SkillKind.None`, always masked).

### 2. Config, input, events

`SurvivorConfig`: `MapHalfSize 50`, `ObstacleCount 40`, `ObstacleRadiusMin 0.5`,
`ObstacleRadiusMax 1.5`, `ObstacleClearRadius 6`, `RunSeconds 900` (60..900; if < 900 the run
ends at that time with `TimeUp`, no boss), `BossExpireSeconds 1020`, `Build`
(`CharacterBuild`), `ClassDef` (default Warrior), `Rewards` (`SurvivorRewardConfig`).
`Validate()` throws on bad values.

`SurvivorInput` (readonly struct): `Move` 0..8 (0 = stand, k = direction angle (k−1)·45°, world
frame, so 1 = +x, 3 = +y), `Skill` 0..4 (0 = none, k = active skill slot k−1), `Pick` 0..4
(0 = none, k = offer k−1). Constants `MoveBranchSize 9`, `SkillBranchSize 5`,
`PickBranchSize 5`.

`SurvivorEvent` (readonly struct): `Type`, `float Value`, `float Extra`, `int Id` (enemy id,
catalog index or −1), `Vec2 Point`. `enum SurvivorEventType`, append-only from now on:

| Type | Value | Extra | Id |
|---|---|---|---|
| `DamageDealt` | HP actually removed | that HP / the enemy's max HP | enemy |
| `BossDamaged` (also emitted, after `DamageDealt`, when the enemy is the boss) | HP removed | that HP / boss max HP | boss |
| `Crit` | | | enemy |
| `EnemyKilled` | TypeIndex | | enemy |
| `EliteKilled`, `BossKilled` | | | enemy |
| `HeroDamaged` | HP removed | that HP / hero MaxHp | **source enemy** |
| `HeroDied` | | | killing enemy or −1 |
| `Blocked`, `Parry` | | | enemy |
| `SkillUsed` | slot | enemies hit by it (kick) | |
| `SkillFailed` | slot | | |
| `XpCollected` | XP after growth | **levels gained**: the sum over each level segment of `xpInSegment / Required(level)` (so crossing a level boundary is counted per level) | |
| `GoldCollected` | gold | | |
| `Healed` | HP restored | | |
| `LevelUp` | new level | | |
| `OfferShown` | offer count | | |
| `ItemPicked` | new level | | catalog index |
| `WeaponFired` | | | catalog index (`Point` = direction) |
| `EnemySpawned`, `EliteSpawned`, `BossSpawned` | | | enemy |
| `RunWon`, `RunTimeUp`, `RunExpired` | | | |

Reserve (declare, never emitted in M4A) `MagnetPicked` and `ChestOpened` at the end of the enum
so M4C can use them. The sim's event list is preallocated (≥ 2048) and cleared at the start of
each `Step`.

### 3. Hero (`SurvivorSim`)

Derived hero stats (recomputed when an item is gained or on `Reset`), `p(s)` = build points:
- `MaxHp = base × (1 + 0.10·ironHeartLvl + 0.05·p(MaxHp))`; when MaxHp grows mid-run, HP grows
  by the same amount.
- `Armor = 1·boneArmorLvl + 0.5·p(Armor)`; `Regen = base + 0.1·p(Regen)` HP/s.
- `Might = 1 + 0.08·gauntletLvl + 0.03·p(Might)`.
- `CritChance = min(1, base + 0.04·critEyeLvl + 0.02·p(Crit))`;
  `CritDamage = base + 0.1·p(CritDamage)`.
- `CooldownMul = max(0.4, 1 − 0.06·hourglassLvl − 0.02·p(Cooldown))`.
- `AreaMul = 1 + 0.08·areaCharmLvl + 0.03·p(Area)`.
- `MoveSpeed = base × (1 + 0.08·bootsLvl + 0.02·p(MoveSpeed))`.
- `PickupRadius = base × (1 + 0.25·magnetLvl + 0.1·p(Magnet))`.
- `Luck = 5·p(Luck)`, `GreedMul = 1 + 0.05·p(Greed)`, `GrowthMul = 1 + 0.03·p(Growth)`.
- Tier gold multiplier `TierGold = 1 + 0.5(N−1)`.

Movement: velocity moves toward `moveDir × MoveSpeed (× block multiplier)` with the acceleration
limit, like the old hero. **Facing** turns (720°/s) toward the nearest alive enemy within 12 m
(boss included), otherwise toward the move direction, otherwise stays.

The sim remembers the **last applied** `Move` and `Skill` (from the last non-paused step; 0 at
reset) for the observation (§9).

Active skills behave like the old Warrior's (copy the logic, do not call the old sim):
- Kick hits every enemy in range + arc in front: damage (through §4), stun, knockback.
- Block is **held**: while the agent keeps choosing the block action the hero blocks (energy cost
  to start, drain per second, stops at 0 energy; energy does not regen while blocking). Blocking
  covers enemies within ±90° of the facing. A covered swing within the parry window since the
  block started is a **parry** (no damage, enemy stunned `StunSeconds`, pushed); otherwise damage ×
  0.5 and the enemy is staggered, as in the old `ResolveZombieAttack`. A covered **contact** hit
  does × 0.5 with no stagger.
- Dash: direction = move input, else facing; passes **through enemies** (no hero–enemy collision
  while dashing) but not through obstacles or walls.

Incoming damage: `raw × (blocked ? blockMultiplier : 1) − Armor`, at least 1; HP 0 → `HeroDied`,
run ends. Regen heals every tick while alive (never above MaxHp).

**Death cause** (`enum DeathCause { None, Surrounded, Boss, Brute, Contact, Projectile }`,
`Projectile` reserved for M4C), set when the hero dies, checked in this order:
1. `Surrounded` if at least 6 alive enemies are within `heroR + enemyR + 0.1` of the hero;
2. otherwise by the source of the killing blow: the boss → `Boss`; a brute swing → `Brute`;
   any contact hit → `Contact`.

Collision:
- Walls: every body is clamped inside the map (`|x|, |y| ≤ 50 − radius`).
- Obstacles: circles; the hero and enemies are pushed out (full correction). Hero projectiles
  ignore obstacles.
- Hero ↔ enemy bodies: overlaps are separated by mass (`hero share = mE / (mH + mE)`), so a hero
  surrounded on all sides is trapped (this is the core Vampire Survivors danger). Skipped while
  the hero dashes.
- Enemy ↔ enemy: soft separation using the spatial hash: each overlapping pair moves apart by half
  of the overlap × 0.5 per tick, split by mass.

`SpatialHash`: 2 m cells over the map, rebuilt once per tick with preallocated arrays
(head/next int arrays, no lists); queries by circle.

### 4. Damage to enemies

`dmg = base × Might`. Crit chance per hit = `CritChance`, **doubled (capped at 1) when the
target is stunned** (so a crit build wants to kick first). Roll `rng.NextFloat() < chance` →
`× CritDamage` and emit `Crit`. Emit `DamageDealt` (and `BossDamaged` for the boss) with the HP
actually removed (≤ remaining HP). At 0 HP the enemy dies (`EnemyKilled`, plus `EliteKilled` /
`BossKilled`) and drops loot (§6).

### 5. XP, level-up and offers

- `XpCurve.Required(int level)` = XP to go from `level` to `level + 1`:
  - `L = 1`: 5; `2 ≤ L ≤ 19`: `5 + 10(L−1)`; `20 ≤ L ≤ 39`: `185 + 13(L−19)`;
    `L ≥ 40`: `445 + 16(L−39)`; plus 600 when `L = 20` and 2400 when `L = 40`.
  - Check values: Required(1)=5, (2)=15, (19)=185, (20)=798, (21)=211, (39)=445, (40)=2861,
    (41)=477.
- Collecting XP adds `value × GrowthMul`. While `xp ≥ Required(level)`: subtract, `level++`,
  `LevelUp`, `pendingLevelUps++`. `XpCollected.Extra` is computed as described in §2.
- When `pendingLevelUps > 0` and no offer is open, **open an offer** (this can happen mid-step;
  finish the current step, then the sim is paused):
  - candidates, in catalog order: owned items below max level, then unowned weapons of the class
    pool if fewer than 4 weapons are owned, then unowned passives if fewer than 4 passives;
  - count = 3, or 4 with probability `Luck / (100 + Luck)`; draw `min(count, candidates)` uniformly
    **without replacement**;
  - if there are no candidates, offer the fillers 62 and 63;
  - emit `OfferShown`.
- **Paused** (`IsAwaitingPick`): `Step` does not advance time, does not move anything, ignores
  move and skill. A pick `k` in `1..OfferCount` applies offer `k−1` (new item at level 1, or
  level + 1; fillers: +25 gold × GreedMul × TierGold / heal 30), emits `ItemPicked`,
  `pendingLevelUps--`, and opens the next offer at once if more are pending. Any other pick
  value does nothing. `LastStepSeconds` is 0 for paused steps and `dt` otherwise.
- A newly acquired weapon is ready at once.

### 6. Pickups and loot

Pools (preallocated): enemies 400, projectiles 128 (hero hammers), pickups 600 of which at most
400 gems, obstacles 64. A spawn into a full pool is skipped (deterministic).

`enum PickupKind { None, Gem, Gold, Meat, Chest, Magnet }` (Chest and Magnet reserved for M4C,
never spawned now).

On a normal enemy death, in this order:
1. an XP gem worth the type's XP (elite 50; boss none). If 400 gems are active, add the value to
   the active gem nearest to the drop point (ties → lowest index) instead;
2. gold with chance `0.03 × (1 + Luck/100)`: value `rng.Range(1, 6) floored × TierGold × GreedMul`;
   an **elite** instead always drops one gold pickup worth `rng.Range(20, 41) floored × TierGold ×
   GreedMul`;
3. meat with chance 0.005 (heals 30).

Extra drops are offset by 0.3 m in a deterministic direction so they do not stack exactly.

Collection: a pickup within `PickupRadius` of the hero becomes *attracted* and flies to the hero
at 12 m/s; it is collected within `heroR + 0.3`: gem → XP (§5); gold → run gold
(`GoldCollected`); meat → heal 30 (`Healed`).

On boss kill: add `500 × TierGold × GreedMul` gold (`GoldCollected`), then the run is won.

Gem size for the view: value < 10 small, 10–99 medium, ≥ 100 large (expose a helper).

Keep running totals for the evaluator and the HUD: `TotalXp` (after growth), `DamageTaken`
(HP removed from the hero), `DamageDealtTotal`, `BossDamageFraction` (sum of `BossDamaged.Extra`),
`MinHpRatio` and `MinHpTime` (lowest HP/MaxHp reached and when), `SkillUses[4]`,
`DropsSpawned` / `DropsCollected` (gold + meat pickups only; M4B uses them for the Greed profile).

### 7. Run end

`EndReason { None, Died, Won, TimeUp, Expired }`:
- `Died`: HP 0 (`HeroDied`, `DeathCause` set);
- `Won`: boss killed (`BossKilled`, `RunWon`);
- `TimeUp`: `RunSeconds < 900` and the time reached it (`RunTimeUp`);
- `Expired`: the boss is still alive at `BossExpireSeconds` (`RunExpired`). Not a death.

After the run ends, `Step` does nothing.

### 8. Public surface for Unity (read-only views, no allocations)

`SurvivorSim(SurvivorConfig config, int seed)`, `Reset(int seed)` (regenerates obstacles from the
seed, re-reads the config and build), `Step(SurvivorInput input)`.
Expose at least: `Config`, `Hero` (position, velocity, facing, HP, MaxHp, energy, skill cooldowns,
blocking, dashing, alive), derived stats, `Time`, `Level`, `Xp`, `XpToNext`, `Gold`, `Kills`,
`EliteKills`, the running totals of §6, `EndReason`, `DeathCause`, `IsEnded`, `IsAwaitingPick`,
`OfferCount`, `GetOffer(int i)` (catalog index + next level), `Inventory` (catalog index → level,
and ordered lists of owned weapons / passives for the HUD), `Enemies` / `Projectiles` / `Pickups`
/ `Obstacles` (`IReadOnlyList<…>`, only `Active` entries are live; enemies expose velocity,
elite, windup and stun state), `BossAlive` and the boss enemy, `LastMove`, `LastSkill`, `Events`,
`LastStepSeconds`.

### 9. Observation v4 (`SurvivorObservation`)

`SchemaVersion = 4`, `Size = 2264`. `Write(SurvivorSim sim, float[] buffer)` fills exactly `Size`
floats with no allocation. All directions are world frame.

**Every value is normalized inside the schema and then clamped to [−1, 1]** (the trainer uses
`normalize: false`). Divide by the stated scale, then clamp; never write NaN or infinity.

**Self** `[0..63]`:
- `[0]` HP/MaxHp, `[1]` MaxHp/500, `[2]` energy/MaxEnergy;
- `[3..6]` active slot 0..3 cooldown remaining / cooldown (0 if no skill);
- `[7..10]` slot usable now (same rule as the mask);
- `[11]` blocking, `[12]` dashing, `[13]` reserved 0;
- `[14..15]` velocity / 20;
- `[16..19]` distance to the +x, −x, +y, −y wall / 100;
- `[20]` time / 1020, `[21]` level / 50, `[22]` xp / XpToNext, `[23]` tier / 10;
- `[24]` awaiting pick, `[25]` boss alive, `[26..27]` unit direction to the boss (0 if none),
  `[28]` boss distance / 50, `[29]` boss HP ratio;
- `[30..45]` build points / cap per stat slot (0 for reserved);
- `[46]` alive enemies / 300, `[47]` log10(1 + run gold) / 4;
- `[48..56]` previous move, one-hot over 9 (`LastMove`);
- `[57..61]` previous skill, one-hot over 5 (`LastSkill`);
- `[62..63]` cos / sin of the facing angle.

**Inventory** `[64..127]`: level / 5 for each catalog index (0 if not owned).

**Offers** `[128..391]`: 4 slots × 66: one-hot over 64 catalog indices, then next level / 5
(0 for fillers), then valid flag. Empty slots are all 0.

**Rays** `[392..2191]`: 72 rays, ray `i` at angle `i × 5°`, range 20 m, 25 floats each, ray base
`392 + 25·i`:
- solid part (17), offsets 0..16: one-hot 12 `[none, wall/obstacle, enemy type 0..7, enemy
  projectile, reserved]`, then distance / 20 (1 if none), elite flag, winding-up flag, stunned
  flag, then **approach speed** — for the **nearest** hit among the map wall, obstacles and
  enemies (enemy projectile slot stays 0 in M4A):
  - enemy: `dot(enemyVel − heroVel, unit(heroPos − enemyPos)) / 10` (positive = closing in);
  - wall/obstacle: `dot(heroVel, rayDir) / 10` (positive = the hero runs into it);
  - none: 0;
- pickup part (8), offsets 17..24: one-hot 7 `[none, gem, gold, meat, chest, magnet, reserved]`,
  then distance / 20 — the nearest pickup along the ray (hit radius `max(r, 0.5)`), **ignoring**
  solids so the agent sees gems behind enemies.
- Semantics are exact ray–circle intersection. Implementation is free: angular binning of the
  candidates returned by the spatial hash is much faster than 72 × N tests and is recommended.

**Density** `[2192..2263]`: 8 sectors (sector `k` centred on `k × 45°`, width 45°) × 3 rings
(0–5, 5–12, 12–30 m, by centre distance) × 3 channels: enemy count / 20, gem XP sum / 50, mean
enemy approach speed (same formula as the rays, averaged over the enemies in that cell, 0 if
none) / 5. Index `2192 + (ring × 8 + sector) × 3 + channel`.

Write a table of these offsets in the XML doc of `SurvivorObservation`, and expose the block
offsets as constants (`SelfOffset`, `InventoryOffset`, `OffersOffset`, `RaysOffset`,
`RayStride = 25`, `DensityOffset`).

### 10. Action mask (`SurvivorActionMask`)

- Awaiting a pick: move only 0, skill only 0, pick `1..OfferCount`.
- Otherwise: move all 9; skill 0 plus every slot whose skill exists and is usable (cooldown
  ready and enough energy; block always allowed while already blocking; nothing while dashing or
  stunned except 0); pick only 0.
- `WriteMask(SurvivorSim, bool[] move, bool[] skill, bool[] pick)` without allocation.
- A skill action that is masked but still arrives (e.g. from a heuristic) does nothing and emits
  `SkillFailed`.

### 11. Rewards (`SurvivorRewardConfig`, `SurvivorRewardCalculator`)

The hierarchy is **outcome > survival > HP > progress > gold**. There is **no kill reward**.

Config (all public fields, positive magnitudes, sign applied by the calculator):

| Field | Default | Applied on |
|---|---|---|
| `Win` | 10 | `RunWon`: +Win |
| `TimeUp` | 5 | `RunTimeUp`: +TimeUp (a curriculum run survived to its end) |
| `Death` | 5 | `HeroDied`: −Death |
| `SurvivePerSecond` | 0.01 | every step: +SurvivePerSecond × `LastStepSeconds` |
| `HpLostPerMaxHp` | 1.0 | `HeroDamaged`: −HpLostPerMaxHp × `Extra` |
| `PerLevelProgress` | 0.05 | `XpCollected`: +PerLevelProgress × `Extra` |
| `PerGold` | 0.001 | `GoldCollected`: +PerGold × `Value` |
| `BossDamage` | 2.0 | `BossDamaged`: +BossDamage × `Extra` |
| `PerDamage` | 0 | `DamageDealt`: +PerDamage × `Value` (auxiliary knob, off) |

`RunExpired` gives 0. Picking an item gives 0.
`Compute(IReadOnlyList<SurvivorEvent> events, float stepSeconds)` returns the step reward.
`SurvivorRewardConfig` has a `Validate()` (all fields finite and ≥ 0).

### 12. Build randomizer and pilot

`BuildRandomizer.Random(Rng rng, int maxLevel, int tierMin, int tierMax)`:
level = uniform `0..maxLevel`; choose 1–3 distinct focus stats among the 13; each point goes to a
random focus stat not at cap with probability 0.7, else to a uniform random used stat not at cap;
if the chosen group is full use the other group; stop when everything is capped. Tier uniform
`tierMin..tierMax`. Result passes `Validate()`. (M4A training uses level 0, tier 1; M4B turns it
on.)

`SurvivorPilot` (like `BrainPilot`): holds a `PolicyBrain`; `Validate(brain)` checks observation
size 2264 and 3 branches of sizes 9/5/5; decides every 5 ticks, **and immediately** whenever the
sim is awaiting a pick; applies the mask; `Deterministic` flag like `BrainPilot` (sampling uses
the pilot's own `Rng`, seeded by the caller). Returns the `SurvivorInput` to use this tick; between
decisions it repeats the last move and skill (block stays held).

### 13. Evaluator (`SurvivorEvaluator`)

Plays whole runs with a brain and reports statistics. It is the M4A acceptance test and, from
M4B, the judge of champion vs challenger.

- `SurvivorRunStats RunOne(PolicyBrain brain, SurvivorConfig config, int seed, bool deterministic)`:
  a fresh `SurvivorSim(config, seed)` and `SurvivorPilot` (pilot rng seeded from `seed`), steps
  until the run ends. Returns: `Seed`, `SurvivedSeconds`, `EndReason`, `DeathCause`, `Level`,
  `Kills`, `EliteKills`, `Gold`, `TotalXp`, `DamageTaken`, `DamageDealt`, `BossDamageFraction`,
  `MinHpRatio`, `MinHpTime`, `SkillUses[4]`, `FinalItemLevels[64]`.
- `SurvivorEvalSummary Summarize(IReadOnlyList<SurvivorRunStats> runs)`: `Runs`,
  `MedianSurvivedSeconds`, `P10SurvivedSeconds` (nearest-rank: sorted ascending, index
  `ceil(0.1·n) − 1`), `MeanSurvivedSeconds`, `WinRate`, `CatastrophicCount` (died before 180 s),
  `GoldPerMinute`, `XpPerMinute`, `DamageTakenPerMinute` (totals / total minutes survived),
  `MeanBossDamageFraction`, `MeanLevel`, counts per `EndReason` and per `DeathCause`.
- `bool PassesM4A(SurvivorEvalSummary s)`: median ≥ 600, P10 ≥ 420, `CatastrophicCount == 0`.
- The evaluator allocates freely (it is not a hot path) but `Step`/`Write` stay allocation-free.
  It is single-threaded; one `PolicyBrain` must not be shared across threads (the tool loads one
  per thread).

### 14. Console tool (`Tools/SurvivorEval`)

`dotnet run --project Tools/SurvivorEval -c Release -- --brain <file.brain> [--seeds 100]
[--seed-start 1000000] [--tier 1] [--run-seconds 900] [--deterministic] [--threads N]
[--out <file.json>]`:
- loads the brain with `PolicyBrain.Load(Stream)` (one instance per worker thread), validates it
  with `SurvivorPilot.Validate`, runs seeds `seed-start .. seed-start + seeds − 1` in parallel
  (default threads = processor count), keeps results ordered by seed;
- prints the summary as readable text and, with `--out`, writes JSON
  `{ "summary": {...}, "passes_m4a": bool, "runs": [ {...}, ... ] }` (enum values as strings,
  `System.Text.Json`, camelCase not required — use the property names as they are);
- exit code 0 on success (pass or fail of M4A does not change it), 2 on bad arguments or an
  invalid brain.
- Training seeds come from Unity's random, and evaluation seeds start at 1 000 000, so they count
  as unseen.

### 15. Invariants (AGENTS.md §6)

Pure C#, no `UnityEngine`, no LINQ in `Step`/`Write`, no static mutable state, deterministic for
the same seed + config + inputs (all randomness from the sim's own `Rng`), no allocations in
`Step`, `Write` or `WriteMask` after construction. `TreatWarningsAsErrors` stays on. No
`System.Random` in Core (the CI guardrail greps for it).

## Tests (must add, `CoreTests/Survivor/`)

- `XpCurve_MatchesTable` (the check values in §5).
- `Catalog_IndicesAndPools` (Warrior pools {0,3} and {6,7,9,12}, reserved rows null, fillers
  62/63).
- `Spawn_FirstMinuteOnlyWalkers_AndRespectsMax` (idle invulnerable test hero: count ≤ 30 in
  minute 0, only walkers; later phases spawn only the listed types).
- `Spawn_TierScalesCountAndHp`.
- `Spawn_NeverInsideObstacleOrOutsideMap`.
- `Elite_SpawnsAt180s_AndDropsGoldBurstAndBigGem`.
- `Boss_SpawnsAt900_AndKillingItWins` (use internal hooks to jump time / set boss HP).
- `Boss_AliveAt1020_RunExpires` (EndReason `Expired`, no `HeroDied`).
- `ShortRun_EndsWithTimeUp` (RunSeconds 180).
- Weapons: `Sweep_ArcAndL5BackArc_OnCooldown`; `Hammer_PierceOne_NoFireWithoutTarget_CountByLevel`.
- `Passives_ChangeDerivedStats` (each M4A passive and each stat point changes exactly its stat).
- `Crit_And_Armor_Math` (seeded crit, minimum 1 damage).
- `Crit_DoubledAgainstStunned` (statistical with a fixed seed, or with an internal roll hook).
- `Block_FrontalContactHalved_ParryStunsBrute`.
- `Dash_PassesThroughEnemies_NotObstacles`.
- `Surrounded_HeroCannotPushThroughRing`.
- `DeathCause_SurroundedBruteBossContact` (one scenario each).
- `LevelUp_PausesAndOffersAreUnique`; `Offer_RespectsPools`; `Offer_FillersWhenEverythingMaxed`;
  `Offer_LuckGivesFourthOption` (statistical, fixed seed); `Pick_AppliesItem_AndResumes`;
  `MultipleLevelUps_OfferInSequence`.
- `Gems_MergeWhenPoolFull`; `Gold_ScalesWithTierAndGreed`; `Meat_Heals`.
- `Events_ExtraFields` (XpCollected.Extra across a level boundary, e.g. 3 XP at level 1 with 4/5
  already → 1/5 + 2/15; HeroDamaged.Extra = HP/MaxHp and Id = source enemy; BossDamaged.Extra).
- `Observation_SizeIs2264_AndOffsets` (write known states and check the self block incl. previous
  action one-hots and facing, inventory, an offer slot, a ray hitting an enemy / wall / gem, and a
  density cell).
- `Observation_ApproachSpeed_Sign` (an enemy running at the hero gives a positive ray value and a
  positive density channel; one running away gives negative; hero running into a wall gives
  positive).
- `Observation_AllValuesFiniteAndInRange` (several seeds, random inputs incl. picks, 2 minutes each
  plus a late-game state with ~250 enemies: every value finite and within [−1, 1]).
- `ActionMask_PickOnlyWhenOffer_SkillsMaskedOnCooldownAndEnergy`.
- `Reward_Signs` (each term in §11 has the stated sign; `RunExpired` and `ItemPicked` give 0).
- `Reward_OutcomeDominates` (with defaults: Win and Death each outweigh the progress + gold of a
  strong run — 50 levels and 2000 gold; 900 s of survival outweighs the same progress + gold;
  losing a full MaxHp costs less than dying).
- `BuildRandomizer_RespectsCapsAndRange` (many seeds).
- `Pilot_RejectsWrongSizes` (build small fake brains the way `PolicyBrainTests` does).
- `Evaluator_RunsShortRuns_AndSummarizes` (a tiny fake brain with the right sizes, RunSeconds 60,
  3 seeds; stats filled, summary median/P10 computed as specified; `PassesM4A` logic checked on
  a hand-made summary).
- `Determinism_TwoSimsSameSeed` (scripted inputs incl. picks, 3 minutes, compare full state
  hashes).
- `Performance_LateGame`: a sim held at ~250 enemies and ~400 gems (internal hooks OK). Measure
  average `Step` and `Write`. Assert (Debug build) `Step` < 0.2 ms and `Write` < 1.0 ms, and write
  the measured numbers in the Report. Target for Release: `Step` ≤ 0.05 ms, `Write` ≤ 0.2 ms;
  training runs 16+ sims at time scale 20, so speed matters.

## Done when

- [ ] `dotnet test C:\PersonalArena-wt\t014\CoreTests` (your checkout) passes with no warnings
  (old tests too).
- [ ] `dotnet build <your checkout>\Tools\SurvivorEval -c Release` succeeds with no warnings, and
  running it on a tiny fake brain written by a test helper (or by a `--self-test` flag that builds
  one in memory) prints a summary.
- [ ] AGENTS.md §6 invariants hold.
- [ ] The Report below is filled in.

## Report (filled by the implementer)

- Changed files:
- Test result:
- Measured performance:
- Notes / open questions:
