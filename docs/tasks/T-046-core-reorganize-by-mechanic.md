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
