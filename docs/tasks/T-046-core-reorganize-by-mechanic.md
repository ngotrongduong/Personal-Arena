# T-046: Reorganise Core by mechanic (pure moves, no behaviour change)

- **Milestone:** M9 cleanup
- **Owner:** Claude subagent (`core-sim-engineer`), Claude reviews and does git
- **Branch:** `claude/serene-sagan-3a70q5` (do NOT run any git command)

## Goal

Core code is split into partial files named after **when** things were added (`SurvivorSim.Content.cs`, `Wave1b`,
`GroupC`) and the catalog is one 886-line file. Reorganise by **what the code does**, so the next weapon has an obvious
home. **Behaviour must stay bit-for-bit identical.** This is a move-and-rename refactor: no logic change, no renamed
public/internal members, no new features, no "while I'm here" fixes (list anything you notice in the Report instead).

## Target layout (`Unity/Assets/Arena/Core/Survivor/`)

`SurvivorSim` partials (fields move with the code that uses them; a field used by several files goes to the most central one):

| File | Contents (methods today) |
|---|---|
| `SurvivorSim.Weapons.cs` | `FireWeapons` (dispatch), shared helpers: `OwnedWeaponWithPattern`, `VolleyCount`, `CountAtLevel`, `HasEnemyInRange`, `NearestEnemy`, `RandomTarget`, `DamageEnemy`, `KillEnemy`, `RollMagnetDrop`, `Blast`, `Stun`, `Rotate`, `CircleIntersectsSegment`, `AwayFromPoint`, `ResetContentState` |
| `SurvivorSim.Weapons.Melee.cs` | `Sweep`, `ThrustSpears`, `UpdateCombo` |
| `SurvivorSim.Weapons.AroundHero.cs` | `GetOrbitAxePosition`, `UpdateOrbitAxes`, `ApplyOrbitHits`, `TickAura`, `UpdateShockwave` |
| `SurvivorSim.Weapons.Projectiles.cs` | `NewProjectile`, `LaunchProjectile`, `UpdateProjectiles`, `AlreadyHit`, `RecordBounceHit`, `ThrowHammers`, `UsedHammerTarget`, `FireFan`, `FireTrio`, `FireQuad`, `ThrowBomb`, `FireBounce`, `BounceProjectile`, `FindObstacle`, `FireMomentum`, `UpdateMovementFactor` (if the file passes ~450 lines, move the bounce/boomerang returners to `SurvivorSim.Weapons.Returning.cs` with `ThrowBoomerangs`, `UpdateBoomerangs`, `HitWithBoomerang`) |
| `SurvivorSim.Weapons.Area.cs` | `StrikeTargets`, `StrikeStones`, `DropZone`, `UpdateZones`, `StartBombRing`, `UpdatePendingBlasts`, `Purge`, `UpdateFreeze` (+ boomerang methods if not in Returning/Projectiles) |
| `SurvivorSim.Weapons.Defense.cs` | `SyncBarrier`, `UpdateBarrier`, `TryAbsorbHit`, `ResolveRetaliate`, `ResolveReflect` |
| `SurvivorSim.Skills.cs` | add `Kick`, `FireSkillProjectile`, `AreaBurst`, `Teleport` to the existing skill code |

Delete `SurvivorSim.Content.cs`, `SurvivorSim.Wave1b.cs`, `SurvivorSim.GroupC.cs` and their `.meta` files when empty.
`SurvivorSim.cs`, `.Enemies.cs`, `.EnemyProjectiles.cs`, `.Progression.cs` keep their content.

**Dispatch:** turn the `if/else if` chain on `def.Pattern` in `FireWeapons` into a `switch` with one case per pattern,
keeping exactly the same conditions, side effects and order of calls (the order of rng draws and events must not change).

Catalog (`SurvivorDefs.cs` today):

| File | Contents |
|---|---|
| `SurvivorDefs.cs` | the enums and definition types (`ItemKind`, `WeaponPattern`, `StatId`, `ItemDef`, enemy def types, …) |
| `SurvivorCatalog.cs` | `SurvivorCatalog` constants, `Get`, `EvolutionOf`, `PassivePerLevel`, the `Passive`/`Filler`/`Evolve` helpers |
| `SurvivorCatalog.Items.cs` | every `static readonly ItemDef` row and both evolution tables, grouped by section comments (Warrior, Mage, Archer, shared weapons, passives, fillers, evolutions) |
| `SurvivorClassKits.cs` | `SurvivorDefaults` (class kits) |
| `SurvivorEnemyDefs.cs` | enemy definition table (if it lives in `SurvivorDefs.cs` today) |

**Static initialisation hazard (must read):** C# runs static field initialisers in textual order within one file, but the
order across files of a `partial` class is NOT guaranteed. The evolution tables reference the base rows
(`Evolve(Sweep, …)`). Therefore every `static readonly` field that another static initialiser reads must be in the
**same file and above** its readers. Keep all rows and both evolution tables in `SurvivorCatalog.Items.cs` in dependency
order, and keep any other cross-referenced static fields together the same way. Add a test that `Get(i)` returns non-null
rows with `EvolvesFrom`'s base non-null for every evolution, if one does not exist already.

## Verification (all required)

1. `export PATH=$PATH:/root/.dotnet; dotnet test CoreTests -c Release`: every test passes (count must equal the count
   before you start, 621, plus any test you add).
2. `dotnet build Tools/SurvivorEval -c Release`: 0 warnings, 0 errors.
3. **Bit-exact fingerprint.** Copy
   `/tmp/claude-0/-home-user-Personal-Arena/daec6a06-5836-5261-8f43-8c544d4cd91e/scratchpad/TmpRefactorFingerprint.cs`
   into `CoreTests/Survivor/`, run
   `dotnet test CoreTests -c Release --filter "Name~TmpFingerprint" --logger "console;verbosity=normal"`, and compare
   every `FP …` line with the file `…/scratchpad/fp-before.txt` (taken before this task). They must be **identical**.
   Then DELETE the copied `TmpRefactorFingerprint.cs` from `CoreTests` (it must not stay in the repo). Paste the
   comparison result in the Report.
4. New `.cs` files under `Unity/` need a `.meta` (copy an existing Core `.cs.meta` and put a new random 32-hex `guid`);
   removed files lose their `.meta` too.

## Hard rules
- Only edit `Unity/Assets/Arena/Core/Survivor`, `CoreTests` (only if a test must follow a moved *internal* member — it
  should not be needed) and this task file. No Unity ML/View, no `Trainer/`. No git commands.
- No public/internal API change, no behaviour change, no reformatting beyond what the move needs (keep the dense style).

## Report (fill in when done): new file list with line counts, anything moved differently from the table and why, the
fingerprint comparison, test count, anything you noticed but did not change.

**Status:** done by `core-sim-engineer` subagent (no git run). Ready for review.

### Files (`Unity/Assets/Arena/Core/Survivor/`, line counts after the move)

| File | Lines | State |
|---|---|---|
| `SurvivorSim.Weapons.cs` | 316 | rewritten: `FireWeapons` (switch) + shared helpers + `ResetContentState` |
| `SurvivorSim.Weapons.Melee.cs` | 85 | new (+ `.meta`) |
| `SurvivorSim.Weapons.AroundHero.cs` | 142 | new (+ `.meta`) |
| `SurvivorSim.Weapons.Projectiles.cs` | 422 | new (+ `.meta`), includes the boomerang |
| `SurvivorSim.Weapons.Area.cs` | 220 | new (+ `.meta`) |
| `SurvivorSim.Weapons.Defense.cs` | 95 | new (+ `.meta`) |
| `SurvivorSim.Skills.cs` | 291 | `Kick`, `FireSkillProjectile`, `AreaBurst`, `Teleport`, `teleportShift` added |
| `SurvivorDefs.cs` | 177 | now only enums, `ItemDef`, `StatInfo`, enemy/spawn/class def types |
| `SurvivorCatalog.cs` | 267 | new (+ `.meta`): constants, `Get`, `EvolutionOf`, `Evolve`, `PassivePerLevel`, `Passive`, `Filler`, `ThrustAngleOffset` |
| `SurvivorCatalog.Items.cs` | 299 | new (+ `.meta`): every `static readonly` row + `Evolutions` + `NewEvolutions` |
| `SurvivorClassKits.cs` | 90 | new (+ `.meta`): `Warrior`, `Mage`, `Archer`, `Skills`, `ForClass` |
| `SurvivorEnemyDefs.cs` | 97 | new (+ `.meta`): `EnemyDefs`, enemy type constants, `Phases`, `EnemyDef`, `PhaseAt`, `PhaseCount`, `GetPhase`, `Enemy`, `Phase` |

Deleted: `SurvivorSim.Content.cs`, `SurvivorSim.Wave1b.cs`, `SurvivorSim.GroupC.cs` and their `.meta` files.
Unchanged: `SurvivorSim.cs`, `.Enemies.cs`, `.EnemyProjectiles.cs`, `.Progression.cs`.
`SurvivorCatalog` and `SurvivorDefaults` became `static partial` (no API change).
Tests: `CoreTests/Survivor/SurvivorDefsTests.cs` gets `Catalog_EveryEvolutionAndItsBaseAreInitialised`.

### Differences from the table, and why

- **Boomerang** (`ThrowBoomerangs`, `UpdateBoomerangs`, `HitWithBoomerang`, `BoomerangMaxAge`) is in `Weapons.Projectiles.cs`. With it the
  file is 422 lines, under the ~450 limit, so no `Weapons.Returning.cs` was needed. The table allows this.
- **Fields** live with the mechanic that owns them, not with `ResetContentState`, even though `ResetContentState` (in
  `Weapons.cs`) touches all of them. Orbit/shockwave state and public getters are in AroundHero. `comboHitsLeft`/`comboTimer` are in Melee.
  Barrier, retaliate, reflect and `lastHeroDamage` are in Defense. `bombRingAngle`, the `pendingBlast*` arrays, the zone constants and
  `PendingBlastCount` are in Area. `movementFactor`/`MovementFactor` are in Projectiles. `teleportShift` sits in Skills next to `Teleport`,
  its only writer (`SurvivorSim.Step` only resets and reads it).
  None of these instance initialisers reads another field, so their order across files does not matter.
- **Spawn phases** (`Phases`, `PhaseAt`, …) moved with the enemy table into `SurvivorEnemyDefs.cs`. That keeps every static field of
  `SurvivorDefaults` in one file, so `SurvivorClassKits.cs` has no static fields.
- **Catalog rows are regrouped** into sections in `SurvivorCatalog.Items.cs`: Warrior-only, Mage-only, Archer-only, shared weapons (in two
  or more class pools: flame cone, bomb, barrier, boomerang, poison pool, bounce shot, bracelet trio), passives, fillers, then
  the evolutions. Moving the rows is safe because a row initialiser only reads constants or pure helpers (`Passive` → `PassivePerLevel`).
  Only `Evolve(...)` reads other static fields, and both evolution tables stay at the bottom of the same file. `SurvivorCatalog.cs` has
  no static fields at all, and a header comment in `Items.cs` explains the rule.
- **Dispatch:** the first group (Orbit, Shockwave, Combo, Retaliate, Barrier, Freeze, all running before the cooldown check) is a
  `switch` with `continue`. After the unchanged `weaponCooldowns[index] > 0f` check, a second `switch` has one case per pattern. Each
  case has the same condition, call, cooldown formula and event arguments as the old `else if`. Each pattern matched only its own branch,
  so the order of rng draws and events is unchanged.

### Fingerprint (verification step 3)

`diff fp-before.txt fp-after.txt` gives **no differences** (RAW-IDENTICAL; all 18 `FP` lines are bit-identical):
```
FP warrior seed1 v0 end=None t=499.52 lvl=22 kills=1475 hash=-2414696907385982677
FP warrior seed1 v1 end=None t=149.79 lvl=5 kills=231 hash=-3259433317285077271
FP warrior seed2 v0 end=None t=499.42 lvl=25 kills=1493 hash=-7872422625952133194
FP warrior seed2 v1 end=None t=149.82 lvl=4 kills=231 hash=159378298131147571
FP warrior seed3 v0 end=None t=499.49 lvl=24 kills=1486 hash=-7610241952995487327
FP warrior seed3 v1 end=None t=149.82 lvl=5 kills=230 hash=8068510590809312612
FP mage seed1 v0 end=None t=499.57 lvl=20 kills=1446 hash=6488252398478163980
FP mage seed1 v1 end=None t=149.89 lvl=3 kills=233 hash=2792508119483585977
FP mage seed2 v0 end=None t=499.52 lvl=20 kills=1475 hash=-2421315420702523020
FP mage seed2 v1 end=None t=149.97 lvl=3 kills=233 hash=-1768969959874371149
FP mage seed3 v0 end=None t=499.47 lvl=26 kills=1508 hash=8627928780651757282
FP mage seed3 v1 end=None t=149.94 lvl=3 kills=231 hash=6843672881192014881
FP archer seed1 v0 end=None t=499.44 lvl=24 kills=1432 hash=-4718888861692968420
FP archer seed1 v1 end=None t=149.92 lvl=4 kills=233 hash=1674495333109962063
FP archer seed2 v0 end=None t=499.42 lvl=25 kills=1426 hash=4966722144604185246
FP archer seed2 v1 end=None t=149.84 lvl=4 kills=234 hash=-4033316909905084751
FP archer seed3 v0 end=None t=499.46 lvl=23 kills=1457 hash=-8455327884221481413
FP archer seed3 v1 end=None t=149.92 lvl=4 kills=232 hash=-5435189624236604904
```
`CoreTests/Survivor/TmpRefactorFingerprint.cs` was deleted after the run.

### Tests and build

- Before: `dotnet test CoreTests -c Release`: 621 passed, 0 failed.
- After: 622 passed, 0 failed (621 + `Catalog_EveryEvolutionAndItsBaseAreInitialised`).
- `dotnet build Tools/SurvivorEval -c Release --no-incremental`: 0 warnings, 0 errors.
- Every `.cs` in the folder has a `.meta`, and no `.meta` is orphaned. The new GUIDs are random 32-hex values.

### Noticed but not changed

1. `Kick` writes `e.StunRemaining = MathF.Max(...)` directly instead of calling `Stun(e, …)`, so a kick also stuns the **boss**. Weapon
   stuns go through `Stun`, which skips bosses. The parry/block in `SurvivorSim.Enemies.cs` also writes `StunRemaining` directly,
   so a skill stunning the boss may be intended. This is a design question worth confirming.
2. `FireFan` returns `true` even when the projectile pool is full and nothing launched, so the cooldown is spent anyway.
   `FireTrio`, `FireQuad`, `FireBounce` and `FireMomentum` return `fired > 0`.
3. `ThrustSpears` repeats the nearest-enemy search inline (same maths as `NearestEnemy(length)`).
4. The buffer sizes `orbitAxePositions = new Vec2[8]` and `hammerTargetIds = new int[8]` are literals. They should be
   `SurvivorCatalog.MaxVolleyCount`, since `VolleyCount` caps at it.
5. `hammerTargetIds`/`UsedHammerTarget` now also serve strikes, stones and trio. A neutral name (e.g. `volleyTargetIds`) would read better.
   This is an internal rename, so it is out of scope here.
6. Old task files (T-021, T-036, T-038, T-042..T-045) still name `SurvivorSim.Content.cs`/`Wave1b`/`GroupC`. They are history, so I left them.
