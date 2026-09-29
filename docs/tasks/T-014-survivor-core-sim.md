# T-014: M4 Core — Survivor simulation (Warrior), observation v4, rewards, pilot

- **Owner:** Codex
- **Status:** doing
- **Milestone:** M4
- **Parallel OK with:** T-015 (Python only, no shared files)
- **Depends on:** none

> Claude (the orchestrator) has full decision authority; never ask the user anything; work until
> the task is done. Follow `AGENTS.md`. Do not run git commands. Do not touch files outside the
> list below. When a detail is not specified, pick the simplest deterministic option, and write
> it in the Report.

## Goal

The game becomes a Vampire Survivors–style mode (design: `docs/GDD.md` §2 and §4; decisions
D-026..D-029 in `docs/DECISIONS.md`). This task builds the **pure C# simulation** of one run for
the Warrior, in a new folder next to the old arena:

- a 100 × 100 m map with walls and circular obstacles;
- a 15-minute spawn schedule with 4 enemy types, elites, a boss and the Reaper;
- auto-firing weapons (6), passives (8), 3 active skills the agent presses;
- XP gems, level-ups that **pause** the run and offer 3–4 choices the agent picks;
- pickups (gems, gold, meat, magnet, chest);
- the build (13 stat points + difficulty tier) that changes the hero and the enemies;
- observation schema v4 (2152 floats), action mask, rewards, a brain pilot and a random-build
  generator for training.

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
  - `SurvivorPilot.cs`.
- Every new `.cs` needs a `.meta`, and the new folder needs `Unity/Assets/Arena/Core/Survivor.meta`.
  Copy the format of a neighbouring `.meta` (for the folder, a Unity folder meta with
  `folderAsset: yes`) and use a fresh random 32-hex-char guid for each.
- Tests: create `CoreTests/Survivor/*.cs`. `CoreTests.csproj` already compiles
  `Unity/Assets/Arena/Core/**/*.cs`, so it needs no change. `internal` members are visible to the
  tests (same assembly), so test hooks may be `internal`.
- You may reuse existing Core types: `Vec2`, `Rng`, `SkillDef`/`SkillKind`, `PolicyBrain`.
  Do not edit any existing Core file.

## Spec

All numbers are starting balance. Put every tunable number in the defs / config objects, not
inline in logic. Coordinates are world frame: the map spans `[-50, 50]` on both axes, the hero
starts at `(0, 0)`, angle 0 = +x, counter-clockwise (same convention as the old Core).
Fixed step `dt = 1/60 s`.

### 1. Catalog and definitions (`SurvivorDefs.cs`)

**Item catalog** — a global index `0..63` shared by all classes (observation one-hot uses it):

| Index | Id | Kind | Name (VN, for the HUD) |
|---|---|---|---|
| 0 | `sword-sweep` | Weapon (Sweep) | Kiếm quét |
| 1 | `spear-thrust` | Weapon (Thrust) | Giáo đâm |
| 2 | `orbit-axe` | Weapon (Orbit) | Rìu xoay |
| 3 | `thrown-hammer` | Weapon (Thrown) | Búa ném |
| 4 | `aura` | Weapon (Aura) | Hào quang |
| 5 | `shockwave` | Weapon (Shockwave) | Sóng chấn động |
| 6 | `iron-heart` | Passive (+10% max HP / level) | Tim sắt |
| 7 | `bone-armor` | Passive (+1 armor / level) | Giáp xương |
| 8 | `might-gauntlet` | Passive (+8% damage / level) | Găng sức mạnh |
| 9 | `crit-eye` | Passive (+4% crit chance / level) | Mắt chí mạng |
| 10 | `hourglass` | Passive (−6% weapon cooldown / level) | Đồng hồ cát |
| 11 | `area-charm` | Passive (+8% area / level) | Bùa vùng |
| 12 | `wind-boots` | Passive (+8% move speed / level) | Ủng gió |
| 13 | `magnet-charm` | Passive (+25% pickup radius / level) | Nam châm |
| 14–61 | reserved | — | — |
| 62 | `bonus-gold` | Filler offer: +25 gold (× greed × tier gold) | +25 vàng |
| 63 | `bonus-heal` | Filler offer: heal 30 HP | Hồi 30 máu |

Types: `enum ItemKind { None, Weapon, Passive, Filler }`,
`enum WeaponPattern { None, Sweep, Thrust, Orbit, Thrown, Aura, Shockwave }`, class `ItemDef`
(`CatalogIndex`, `Id`, `Name`, `Kind`, `Pattern`, `MaxLevel = 5`, plus the weapon numbers below).
`SurvivorCatalog.CatalogSize = 64`, `MaxWeapons = 4`, `MaxPassives = 4`,
`SurvivorCatalog.Get(int index)` (null for reserved).

**Weapons** (level L = 1..5). "area" = the hero's area multiplier (§3), "cd" = the hero's
cooldown multiplier, damage goes through §4. A weapon that is ready fires at once; its cooldown
restarts when it fires.

| Weapon | Behaviour | Numbers |
|---|---|---|
| Sweep | Instant arc hit toward the hero's facing: every enemy whose centre is within `range + enemy radius` and within ±60° of the facing. At L5 also a second arc toward facing + 180°. Knockback 0.5 m. | damage 20 + 8(L−1); range 2.5 × (1 + 0.1(L−1)) × area; cooldown 1.2 × cd |
| Thrust | Instant line hit(s) from the hero toward the facing, length `5 × area`, half-width `0.3 + enemy radius`, pierces everything. Count 1 (L1–2), 2 (L3–4, at ±10°), 3 (L5, at 0 and ±15°). An enemy is hit at most once per firing. Knockback 0.3 m. | damage 30 + 10(L−1); cooldown 1.8 × cd |
| Orbit | When ready, N axes appear for `4 s`, evenly spaced, rotating counter-clockwise at 180°/s around the hero at radius `2.2 × area`. Hit radius `0.5 × area`. Each enemy can be hit by the orbit at most once per 0.5 s. The cooldown starts **after** the axes vanish. Count 1/2/2/3/3. Knockback 0.3 m (away from the hero). | damage 12 + 4(L−1); cooldown 3 × cd |
| Thrown | Throws N hammers, each at a random alive enemy within 10 m (distinct targets while possible). A hammer is a hero projectile: speed 12 m/s, radius `0.4 × area`, lifetime `12 / 12 s`, pierce 1 (it can hit 2 enemies, each once). If no enemy is within 10 m it does not fire and stays ready. Count 1/2/2/3/3. Knockback 0.5 m. Hero projectiles ignore obstacles. | damage 25 + 7(L−1); cooldown 1.5 × cd |
| Aura | Always on. Every `0.4 × cd s` it hits every enemy within `1.8 × (1 + 0.1(L−1)) × area + enemy radius`. Knockback 0.1 m. | damage 5 + 2(L−1) |
| Shockwave | When ready, a ring starts at the hero's current position and grows from 0 to `5 × area` m in 0.5 s. An enemy is hit once per wave when `distance − enemy radius ≤ ring radius`. Knockback 2 m (away from the wave centre). Keep a small preallocated list of active waves (e.g. 4). | damage 18 + 6(L−1); cooldown `(4 − 0.3(L−1)) × cd` |

Knockback of every weapon and skill is multiplied by `(1 − enemy KnockbackResist)`.

**Enemy types** (`SurvivorEnemyDef`; `TypeIndex` is the observation slot, 8 slots, 6 used):

| TypeIndex | Id | HP (min 0, tier 1) | Speed | Radius | Mass | Attack | XP | KB resist |
|---|---|---|---|---|---|---|---|---|
| 0 | walker | 15 | 2.0 | 0.45 | 1 | Contact 8 | 1 | 0 |
| 1 | runner | 10 | 4.0 | 0.4 | 1 | Contact 5 | 1 | 0 |
| 2 | brute | 80 | 1.6 | 0.7 | 3 | Melee swing: range 1.8, arc 90°, windup 0.9 s, damage 25, recover 1.8 s | 5 | 0.6 |
| 3 | spitter | 20 | 2.4 | 0.45 | 1 | Ranged: keep 7 m (same movement rule as the old Spitter: approach if `d > 8`, back off if `d < 5.5`, else stand), start a 0.6 s windup when `d ≤ 10` and facing within ±10°, then fire a projectile (damage 10, speed 9, radius 0.3, range 12), recover 2.5 s | 2 | 0 |
| 4 | boss (`bone-lord`) | 6000 | 1.4 | 1.75 | 1000 | Melee swing: range 3.5, arc 180°, windup 1.2 s, damage 40, recover 4.8 s (one swing per ~6 s); every 10 s summons 8 walkers on a 3 m ring around itself | 0 | 1.0 |
| 5 | reaper | invulnerable | 8.0 | 0.6 | 1000 | Contact damage 100000 | 0 | 1.0 |
| 6–7 | reserved | | | | | | | |

- **Contact** attackers damage the hero when `distance ≤ heroR + enemyR + 0.1`, at most once per
  0.5 s per enemy (per-enemy timer).
- **Melee swing / ranged** attackers use a windup (telegraph) like the old zombies: they stop
  moving during the windup; a swing resolves at the end of the windup if the hero is still within
  `range + heroR` and the arc.
- Every enemy turns toward the hero (turn speed 360°/s; brute/boss 180°/s) and moves straight
  toward it (spitter: its distance rule). Stunned enemies do nothing and a stun cancels a windup.
- **Scaling at spawn time** (`t` = run seconds, `N` = tier): `hp = baseHp × (1 + 0.12·t/60) ×
  (1 + 0.35(N−1))`; `damage = baseDamage × (1 + 0.05·t/60) × (1 + 0.2(N−1))`. The boss uses only
  the tier factors (no time factor). The reaper is never scaled.
- **Elite** (flag on a normal enemy): radius × 1.6, mass × 3, HP × 10, damage × 1.5, KB resist
  max(own, 0.8), XP 50, always drops a chest **and** gold (§6).

**Spawn schedule** (tier 1; data table, one row per phase):

| From (s) | To (s) | Weights (walker, runner, brute, spitter) | Max alive | Spawns / s |
|---|---|---|---|---|
| 0 | 60 | 1, 0, 0, 0 | 30 | 1 |
| 60 | 180 | 3, 1, 0, 0 | 60 | 2 |
| 180 | 300 | 3, 2, 1, 0 | 90 | 3 |
| 300 | 420 | 2, 2, 1, 1 | 120 | 4 |
| 420 | 600 | 2, 3, 1, 2 | 160 | 5 |
| 600 | 720 | 1, 3, 2, 2 | 200 | 6 |
| 720 | 900 | 1, 3, 3, 3 | 250 | 8 |

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
  spawns on the ring and `BossSpawned` is emitted. Killing it wins the run (§7). At t = 1020 the
  reaper spawns on the ring (`ReaperSpawned`); as a safety net the run ends as a death at
  t = 1200.

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
- `StartingWeapon = 0` (sword-sweep L1), `WeaponPool = {0..5}`, `PassivePool = {6..13}`;
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
ends at that time with `RunTimeUp`, no boss), `Build` (`CharacterBuild`), `ClassDef`
(default Warrior), `Rewards` (`SurvivorRewardConfig`). `Validate()` throws on bad values.

`SurvivorInput` (readonly struct): `Move` 0..8 (0 = stand, k = direction angle (k−1)·45°, world
frame, so 1 = +x, 3 = +y), `Skill` 0..4 (0 = none, k = active skill slot k−1), `Pick` 0..4
(0 = none, k = offer k−1). Constants `MoveBranchSize 9`, `SkillBranchSize 5`,
`PickBranchSize 5`.

`SurvivorEvent` (readonly struct): `Type`, `float Value`, `int Id` (enemy id, catalog index or
−1), `Vec2 Point`. `enum SurvivorEventType`, append-only from now on:
`DamageDealt` (Value = HP actually removed, Id = enemy), `Crit`, `EnemyKilled` (Id = enemy,
Value = TypeIndex), `EliteKilled`, `BossKilled`, `HeroDamaged` (Value = HP removed),
`HeroDied`, `Blocked`, `Parry`, `SkillUsed` (Value = slot), `SkillFailed`, `XpCollected`
(Value = XP after growth), `GoldCollected` (Value = gold), `Healed` (Value = HP restored),
`MagnetPicked`, `ChestOpened` (Id = upgraded catalog index or −1, Value = gold), `LevelUp`
(Value = new level), `OfferShown` (Value = offer count), `ItemPicked` (Id = catalog index,
Value = new level), `WeaponFired` (Id = catalog index, Point = direction or centre),
`EnemySpawned`, `EliteSpawned`, `BossSpawned`, `ReaperSpawned`, `RunWon`, `RunTimeUp`.
The sim's event list is preallocated (≥ 2048) and cleared at the start of each `Step`.

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
(boss included, reaper included), otherwise toward the move direction, otherwise stays.

Active skills behave like the old Warrior's (copy the logic, do not call the old sim):
- Kick hits every enemy in range + arc in front: damage (through §4), stun, knockback.
- Block is **held**: while the agent keeps choosing the block action the hero blocks (energy cost
  to start, drain per second, stops at 0 energy; energy does not regen while blocking). Blocking
  covers enemies within ±90° of the facing. A covered swing within the parry window since the
  block started is a **parry** (no damage, enemy stunned `StunSeconds`, pushed); otherwise damage ×
  0.5 and the enemy is staggered, as in the old `ResolveZombieAttack`. A covered **contact** hit
  does × 0.5 with no stagger. A covered enemy projectile is destroyed (`Blocked`), no damage.
- Dash: direction = move input, else facing; passes **through enemies** (no hero–enemy collision
  while dashing) but not through obstacles or walls.

Incoming damage: `raw × (blocked ? blockMultiplier : 1) − Armor`, at least 1; HP 0 → `HeroDied`,
run ends. Regen heals every tick while alive (never above MaxHp).

Collision:
- Walls: every body is clamped inside the map (`|x|, |y| ≤ 50 − radius`).
- Obstacles: circles; the hero and enemies are pushed out (full correction). Enemy and hero
  projectiles ignore obstacles.
- Hero ↔ enemy bodies: overlaps are separated by mass (`hero share = mE / (mH + mE)`), so a hero
  surrounded on all sides is trapped (this is the core Vampire Survivors danger). Skipped while
  the hero dashes.
- Enemy ↔ enemy: soft separation using the spatial hash: each overlapping pair moves apart by half
  of the overlap × 0.5 per tick, split by mass.

`SpatialHash`: 2 m cells over the map, rebuilt once per tick with preallocated arrays
(head/next int arrays, no lists); queries by circle.

### 4. Damage to enemies

`dmg = base × Might`; roll `rng.NextFloat() < CritChance` per hit → `× CritDamage` and emit
`Crit`. Emit `DamageDealt` with the HP actually removed (≤ remaining HP). At 0 HP the enemy dies
(`EnemyKilled`, plus `EliteKilled` / `BossKilled`) and drops loot (§6). The reaper ignores all
damage.

### 5. XP, level-up and offers

- `XpCurve.Required(int level)` = XP to go from `level` to `level + 1`:
  - `L = 1`: 5; `2 ≤ L ≤ 19`: `5 + 10(L−1)`; `20 ≤ L ≤ 39`: `185 + 13(L−19)`;
    `L ≥ 40`: `445 + 16(L−39)`; plus 600 when `L = 20` and 2400 when `L = 40`.
  - Check values: Required(1)=5, (2)=15, (19)=185, (20)=798, (21)=211, (39)=445, (40)=2861,
    (41)=477.
- Collecting XP adds `value × GrowthMul`. While `xp ≥ Required(level)`: subtract, `level++`,
  `LevelUp`, `pendingLevelUps++`.
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

Pools (preallocated): enemies 400, projectiles 256 (hero hammers + enemy spit), pickups 600 of
which at most 400 gems, obstacles 64. A spawn into a full pool is skipped (deterministic).

On a normal enemy death, in this order:
1. an XP gem worth the type's XP (elite 50; boss and reaper none). If 400 gems are active, add the
   value to the active gem nearest to the drop point (ties → lowest index) instead;
2. gold with chance `0.03 × (1 + Luck/100)` (elites always): value
   `rng.Range(1, 6) floored × TierGold × GreedMul`;
3. meat with chance 0.005 (heals 30);
4. magnet with chance 0.002;
5. elites also drop a chest.

Extra drops are offset by 0.3 m in a deterministic direction so they do not stack exactly.

Collection: a pickup within `PickupRadius` of the hero (or any gem after a magnet) becomes
*attracted* and flies to the hero at 12 m/s; it is collected within `heroR + 0.3`.
- gem → XP (§5); gold → run gold (`GoldCollected`); meat → heal 30 (`Healed`); magnet → attract
  every gem on the map (`MagnetPicked`);
- chest → upgrade one uniformly random owned item below max level by 1 and add
  `rng.Range(50, 151) floored × TierGold × GreedMul` gold; if nothing can be upgraded, the gold is
  doubled. Emit `ChestOpened`. A chest never opens an offer.
- On boss kill: add `500 × TierGold × GreedMul` gold, then the run is won.

Gem size for the view: value < 10 small, 10–99 medium, ≥ 100 large (expose a helper).

### 7. Run end

`EndReason { None, Died, Won, TimeUp }`. `Died`: HP 0, reaper contact, or the t = 1200 safety net
(`HeroDied`). `Won`: boss killed (`BossKilled`, `RunWon`). `TimeUp`: `RunSeconds < 900` and the
time reached it (`RunTimeUp`). After the run ends, `Step` does nothing.

### 8. Public surface for Unity (read-only views, no allocations)

`SurvivorSim(SurvivorConfig config, int seed)`, `Reset(int seed)` (regenerates obstacles from the
seed, re-reads the config and build), `Step(SurvivorInput input)`.
Expose at least: `Config`, `Hero` (position, velocity, facing, HP, MaxHp, energy, skill cooldowns,
blocking, dashing, alive), derived stats, `Time`, `Level`, `Xp`, `XpToNext`, `Gold`, `Kills`,
`EliteKills`, `DamageDealt`, `EndReason`, `IsEnded`, `IsAwaitingPick`, `OfferCount`,
`GetOffer(int i)` (catalog index + next level), `Inventory` (catalog index → level, and ordered
lists of owned weapons / passives for the HUD), `Enemies` / `Projectiles` / `Pickups` /
`Obstacles` (`IReadOnlyList<…>`, only `Active` entries are live), orbit axe positions (count,
angle, radius, active), active shockwaves (centre, radius), `BossAlive` and the boss enemy,
`Events`, `LastStepSeconds`.

### 9. Observation v4 (`SurvivorObservation`)

`SchemaVersion = 4`, `Size = 2152`. `Write(SurvivorSim sim, float[] buffer)` fills exactly `Size`
floats with no allocation. All directions are world frame. Blocks, in order:

**Self** `[0..47]`:
`[0]` HP/MaxHp, `[1]` MaxHp/500, `[2]` energy/MaxEnergy, `[3..6]` active slot 0..3 cooldown
remaining / cooldown (0 if no skill), `[7..10]` slot usable now (same rule as the mask), `[11]`
blocking, `[12]` dashing, `[13]` reserved 0, `[14..15]` velocity / 10, `[16..19]` distance to the
+x, −x, +y, −y wall / 100, `[20]` time / 900, `[21]` level / 50, `[22]` xp / XpToNext, `[23]`
tier / 10, `[24]` awaiting pick, `[25]` boss alive, `[26..27]` unit direction to the boss (0 if
none), `[28]` min(1, boss distance / 50), `[29]` boss HP ratio, `[30..45]` build points / cap per
stat slot (0 for reserved), `[46]` min(1, alive enemies / 300), `[47]` log10(1 + run gold) / 4.

**Inventory** `[48..111]`: level / 5 for each catalog index (0 if not owned).

**Offers** `[112..375]`: 4 slots × 66: one-hot over 64 catalog indices, then next level / 5
(0 for fillers), then valid flag. Empty slots are all 0.

**Rays** `[376..2103]`: 72 rays, ray `i` at angle `i × 5°`, range 20 m, 24 floats each:
- solid part (16): one-hot 12 `[none, wall/obstacle, enemy type 0..7, enemy projectile,
  reserved]`, then distance / 20 (1 if none), elite flag, winding-up flag, stunned flag — for the
  **nearest** hit among the map wall, obstacles, enemies and enemy projectiles (projectile hit
  radius `max(r, 0.4)`);
- pickup part (8): one-hot 7 `[none, gem, gold, meat, chest, magnet, reserved]`, then distance /
  20 — the nearest pickup along the ray (hit radius `max(r, 0.5)`), **ignoring** solids so the
  agent sees gems behind enemies.
- Semantics are exact ray–circle intersection. Implementation is free: angular binning of the
  candidates returned by the spatial hash is much faster than 72 × N tests and is recommended.

**Density** `[2104..2151]`: 8 sectors (sector `k` centred on `k × 45°`, width 45°) × 3 rings
(0–5, 5–12, 12–30 m, by centre distance) × 2 channels (min(1, enemy count / 20), min(1, gem XP
sum / 50)). Index `2104 + (ring × 8 + sector) × 2 + channel`.

Write a table of these offsets in the XML doc of `SurvivorObservation`.

### 10. Action mask (`SurvivorActionMask`)

- Awaiting a pick: move only 0, skill only 0, pick `1..OfferCount`.
- Otherwise: move all 9; skill 0 plus every slot whose skill exists and is usable (cooldown
  ready and enough energy; block always allowed while already blocking; nothing while dashing or
  stunned except 0); pick only 0.
- `WriteMask(SurvivorSim, bool[] move, bool[] skill, bool[] pick)` without allocation.

### 11. Rewards (`SurvivorRewardConfig`, `SurvivorRewardCalculator`)

Config (all public fields): `SurvivePerSecond 0.01`, `PerXp 0.01`, `PerGold 0.02`,
`PerDamage 0.0005`, `Kill 0.01`, `EliteKill 0.5`, `BossKill 5`, `HpLostPenalty 0.01` (applied as
−0.01 per HP), `Death 3` (applied as −3).
`Compute(IReadOnlyList<SurvivorEvent> events, float survivedSeconds)` sums: survive ×
`LastStepSeconds`, `XpCollected`, `GoldCollected`, `DamageDealt`, `EnemyKilled` (Kill),
`EliteKilled` (extra EliteKill), `BossKilled`, `HeroDamaged` (negative), `HeroDied` (negative).
Picking an item gives 0. Every term gets a sign test.

### 12. Build randomizer and pilot

`BuildRandomizer.Random(Rng rng, int maxLevel, int tierMin, int tierMax)`:
level = uniform `0..maxLevel`; choose 1–3 distinct focus stats among the 13; each point goes to a
random focus stat not at cap with probability 0.7, else to a uniform random used stat not at cap;
if the chosen group is full use the other group; stop when everything is capped. Tier uniform
`tierMin..tierMax`. Result passes `Validate()`.

`SurvivorPilot` (like `BrainPilot`): holds a `PolicyBrain`; `Validate(brain)` checks observation
size 2152 and 3 branches of sizes 9/5/5; decides every 5 ticks, **and immediately** whenever the
sim is awaiting a pick; applies the mask; `Deterministic` flag like `BrainPilot`. Returns the
`SurvivorInput` to use this tick.

### 13. Invariants (AGENTS.md §6)

Pure C#, no `UnityEngine`, no LINQ in `Step`/`Write`, no static mutable state, deterministic for
the same seed + config + inputs (all randomness from the sim's own `Rng`), no allocations in
`Step`, `Write` or `WriteMask` after construction. `TreatWarningsAsErrors` stays on.

## Tests (must add, `CoreTests/Survivor/`)

- `XpCurve_MatchesTable` (the check values in §5).
- `Catalog_IndicesAndPools` (Warrior pools, reserved slots null, fillers 62/63).
- `Spawn_FirstMinuteOnlyWalkers_AndRespectsMax` (idle invulnerable test hero: count ≤ 30 in
  minute 0, only walkers; later phases spawn the listed types).
- `Spawn_TierScalesCountAndHp`.
- `Spawn_NeverInsideObstacleOrOutsideMap`.
- `Elite_SpawnsAt180s_AndDropsChest`.
- `Boss_SpawnsAt900_AndKillingItWins` (use internal hooks to jump time / set boss HP).
- `ShortRun_EndsWithTimeUp` (RunSeconds 180).
- `Reaper_KillsHero`.
- One test per weapon: fires on its cooldown, hits what the spec says (sweep arc and L5 back arc,
  thrust pierce and extra thrusts, orbit hit interval, hammer pierce 1 and no fire without target,
  aura tick, shockwave once per wave).
- `Passives_ChangeDerivedStats` (each passive and each stat point changes exactly its stat).
- `Crit_And_Armor_Math` (seeded crit, minimum 1 damage).
- `Block_FrontalContactHalved_ParryStunsBrute_SpitBlocked`.
- `Dash_PassesThroughEnemies_NotObstacles`.
- `Surrounded_HeroCannotPushThroughRing`.
- `LevelUp_PausesAndOffersAreUnique`; `Offer_RespectsFourWeaponsFourPassives`;
  `Offer_FillersWhenEverythingMaxed`; `Offer_LuckGivesFourthOption` (statistical, fixed seed);
  `Pick_AppliesItem_AndResumes`; `MultipleLevelUps_OfferInSequence`.
- `Gems_MergeWhenPoolFull`; `Gold_ScalesWithTierAndGreed`; `Magnet_AttractsAllGems`;
  `Chest_UpgradesOwnedItem_OrDoublesGold`.
- `Observation_SizeIs2152_AndOffsets` (write known states and check the self block, inventory,
  an offer slot, a ray hitting an enemy / wall / gem, and a density cell).
- `ActionMask_PickOnlyWhenOffer`.
- `Reward_Signs` (each term).
- `BuildRandomizer_RespectsCapsAndRange` (many seeds).
- `Pilot_RejectsWrongSizes` (build small fake brains the way `PolicyBrainTests` does).
- `Determinism_TwoSimsSameSeed` (scripted inputs incl. picks, 3 minutes, compare full state
  hashes).
- `Performance_LateGame`: a sim held at ~250 enemies and ~400 gems (internal hooks OK). Measure
  average `Step` and `Write`. Assert (Debug build) `Step` < 0.2 ms and `Write` < 1.0 ms, and write
  the measured numbers in the Report. Target for Release: `Step` ≤ 0.05 ms, `Write` ≤ 0.2 ms;
  training runs 16+ sims at time scale 20, so speed matters.

## Done when

- [ ] `dotnet test C:\PersonalArena\CoreTests` passes with no warnings (old tests too).
- [ ] AGENTS.md §6 invariants hold.
- [ ] The Report below is filled in.

## Report (filled by the implementer)

- Changed files:
- Test result:
- Measured performance:
- Notes / open questions:
