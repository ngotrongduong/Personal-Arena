# T-032: M7 Unity — class shop, per-class brains, new weapons, enemies and effects in the viewer

- **Milestone:** M7 (D-038)
- **Owner:** Claude subagent (unity-integrator) + Claude (PC)
- **Branch / worktree:** `task/t032-m7-unity` at `C:\PersonalArena-wt\t032` (already contains T-030 core and
  T-031 trainer; `Unity/Library` already copied, so batchmode imports are incremental)
- **Depends on:** T-030 (Core: kits, catalog 14–25 and 40–57, enemies 5–7, events), T-031 (trainer:
  behaviors `Warrior` / `Mage` / `Archer`, runs `<class>-sNNN`, `train_service.py --behavior <Class>`)

## Goal (owner view)

The owner buys Mage (1.500 vàng) or Archer (3.000 vàng) in the character panel, selects it, presses
TRAIN, and watches that class's own AI learn, with its own model, weapons, skills and the new enemies
and effects. Switching back to the Warrior shows the Warrior brain again. Everything is Vietnamese.

## Core facts (read the code: `Unity/Assets/Arena/Core/Survivor/*`, `Core/Meta/ProfileRules.cs`)

- `SurvivorDefaults.ForClass(id)` → kit (`warrior`, `mage`, `archer`); `ClassRegistry.BehaviorName(id)`.
- `ProfileRules.TryBuyClass(profile, classId, out reason)`, `TrySelectClass`, `OwnsClass`,
  `IsClassPlayable`, prices in ProfileRules. `PlayerProfile.SelectedClassId`; each owned class has its
  own character record (level, stat points, build, `BrainRunId`).
- Catalog: Mage weapons 14–19 (Tia phép, Cầu lửa xoay, Vòng băng, Vùng thánh, Sét, Tia ma thuật),
  Archer 20–25 (Mũi tên, Tên chùm, Mưa tên, Dao xoay, Dao găm, Nỏ); evolutions 40–57
  (`ItemDef.EvolvesFrom`, `EvolutionPassive`, `SurvivorCatalog.EvolutionOf`). Patterns: existing ones plus
  `Strike` (random targets in range, blast radius `Width·AreaMul`) and `Fan` (projectile spread).
- Skills: Mage fireball (Projectile, explodes), mana-shield (Block, all directions), blink (Teleport),
  frost-burst (AreaBurst); Archer power-shot (Projectile, pierce), roll-back (Dash backward), kick.
- Enemies: 5 Exploder (windup then explodes, radius 2.2), 6 Ghost (passes through obstacles),
  7 Necromancer (ranged, summons 3 walkers every 6 s). Never elite.
- New events (`SurvivorEvent.cs`): `StrikeLanded` (Point, Value = radius, Id = weapon index or −1 for a
  skill), `EnemyExploded` (Point, Value = radius, Id = enemy id), `EnemySummoned` (Point, Value = count,
  Id = necromancer id), `WeaponEvolved` (Id = evolution index, Extra = base index).
  `SurvivorProjectile.SourceIndex` < 0 means a skill projectile (slot = −1 − SourceIndex);
  `ExplodeRadius` > 0 means it explodes. `DeathCause.Explosion` exists.

## Work

1. **Selected class drives the viewer** (`SurvivorWatchController`): replace the `BehaviorName = "Warrior"`
   constant with the selected class (`ClassRegistry.BehaviorName(profile.SelectedClassId)`), used by
   brain lookup, champion, lineage, history, behavior profile, TRAIN (`--behavior`, `--run-id` of that
   class's character), auto-farm, and the sim config (that class's kit + its character's stats/tier).
   Changing class restarts the watched run with that class's brain. A class without any brain yet shows
   the existing "no brain" state with a clear Vietnamese hint ("Chưa có não Mage — bấm HUẤN LUYỆN").
   If a training service is running for another class and the owner presses TRAIN, stop it gracefully
   (the existing stop path, which saves a checkpoint) and then start the selected class, with a notice.
   Old pre-Survivor runs (`mage-001`, `archer-001`, no schema file) must never be loaded (BrainLocator is
   already schema-aware; add a test for a Mage).
2. **Character panel shop** (`View/Meta/CharacterPanel.cs`): show the three classes with price,
   owned/selected state, `Mua (1.500 vàng)` (disabled with the reason when gold is short), `Chọn`, and a
   one-line Vietnamese description of each class's play style. Save the profile after buying/selecting.
3. **Hero models per class**: the KayKit Adventurers models are in the project (M3 had all three
   classes; see `ArenaArtSet` / `KayKitArtSetBuilder` / `SurvivorArtSetBuilder`). Mage = KayKit Mage,
   Archer = KayKit Rogue (Hooded if present) with a bow/crossbow prop if one exists. Same animator setup.
4. **Weapons and skills in the renderer** (`SurvivorRenderer.Weapons.cs` and friends): visuals for every
   weapon 14–25 by pattern, with class colours (mage: blue/purple/ice/gold; archer: wood/green arrows).
   `Strike`: a lightning bolt / arrow volley at `StrikeLanded.Point` with a ring of radius `Value`.
   `Fan`: arrow projectiles. Evolutions 40–57 render like their `EvolvesFrom` base, larger and gold-tinted.
   Skill projectiles (SourceIndex < 0): fireball (orange, explosion ring on its StrikeLanded Id −1),
   power shot (long arrow with trail). Frost burst: ice ring. Blink: short flash at both ends.
   Mana shield: a full bubble while blocking (the Warrior keeps its front shield).
5. **Enemies**: Exploder (zombie tinted orange-red, pulsing/swelling during windup, explosion effect on
   `EnemyExploded`), Ghost (semi-transparent pale blue, slight hover), Necromancer (KayKit Skeleton Mage
   if present, else a purple-tinted zombie; summon circle on `EnemySummoned`).
6. **HUD / texts**: icons for items 14–25 and 40–57 and the new skills (game-icons.net CC BY 3.0 via the
   existing icon pipeline — free downloads are pre-approved; credit every new icon in
   `ThirdParty/CREDITS.md`). Evolutions may reuse the base icon with a gold frame. Vietnamese item and
   skill descriptions wherever the Warrior ones live (level-up panel, inventory tooltips). A HUD toast on
   `WeaponEvolved`: "TIẾN HÓA: <tên>". Death cause "Bom xác nổ" already exists — check it shows.
7. **Training host**: `TrainingArenaHost` already reads `--hero-class`; make sure every agent of a
   Mage/Archer training build gets that class's behavior name and kit, and owner-build arguments are
   read per class. EditMode tests for Mage and Archer (behavior name, observation size 2264, action
   branches unchanged).
8. **Tests (EditMode)**: shop rules in the panel logic (pure helpers), class → behavior/brain lookup,
   TRAIN arguments per class, renderer mapping helpers (weapon index → visual kind, evolution → base),
   new text helpers. Keep all existing tests green.

## Rules

- Claude (the orchestrator) has full decision authority; never ask the user anything; work until the task is done.
- Work only in `C:\PersonalArena-wt\t032`. Never modify `C:\PersonalArena` (the owner's checkout; its
  `Trainer\runs` holds live training — read/copy only). Never stop or restart the owner's training.
- Never commit `Unity/ProjectSettings/ProjectSettings.asset`; revert `Unity/Assets/Scenes/ArenaWatch.unity`
  if a build touches it. No secrets. Keep Core (`Unity/Assets/Arena/Core`) unchanged unless a real bug
  needs a fix — then add a CoreTest and say so in the report.
- Commands: one simple command per call; multi-step logic goes into a `.ps1` in the scratchpad
  `C:\Users\ngotr\AppData\Local\Temp\claude\C--Users-ngotr-Desktop\6f63cc32-b8cc-4908-a74f-48b51f5ac137\scratchpad\`
  run as `& '<path>.ps1'` (ASCII only inside .ps1). Use Read/Edit/Write/Grep/Glob for files.
- Unity batchmode: `C:\Program Files\Unity\Hub\Editor\6000.3.2f1\Editor\Unity.exe -batchmode -nographics
  -projectPath C:\PersonalArena-wt\t032\Unity -runTests -testPlatform EditMode -testResults <xml> -logFile <log>`
  (see `scratchpad\t025_editmode.ps1` for a ready template). CoreTests: `dotnet test
  C:\PersonalArena-wt\t032\CoreTests -c Release`.
- Commit on `task/t032-m7-unity` (`git -C C:\PersonalArena-wt\t032 ...`, message via `-F <file>`, ending
  with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`). Do not merge or push.

## Done when

- EditMode and CoreTests green; the report lists files, tests and anything left uncompiled/unverified.
- The orchestrator builds `Build/WatchNext` + `Build/TrainingNext` and takes screenshots (Mage and
  Archer in a run with the new enemies; the shop panel).

## Report

Branch `task/t032-m7-unity`, on top of the t030 + t031 merges (HEAD b722686).

**Not run by the implementer.** The shell in the implementing session returned no output for every command
(even `echo`), so nothing below was compiled, tested, built, screenshotted or committed there. The
work was done with file tools only, and every API used was checked against the merged Core by reading
it. The orchestrator runs these scratchpad scripts in this order (all ASCII, one command each):

1. `t032_icons.ps1`: optional polish. It downloads 10 game-icons.net glyphs for the class weapons
   (magic-bolt, fire-orb, holy-field, lightning, arcane-beam, multi-shot, arrow-rain, orbit-knife,
   dagger, crossbow), resizes them to 256 px, and appends `GameIcons/License.txt`. It also updates
   `ThirdParty/CREDITS.md` (28 to 38 icons, plus Carl Olsen). If any download fails, nothing is kept and
   the weapons keep their alias glyphs, which the tests already cover.
2. `t032_editmode.ps1`: runs CoreTests (`dotnet test`) and then the EditMode tests in batchmode.
   Results go to `t032_editmode.xml` and `t032_coretests.txt`.
3. `t032_build_shots.ps1`: builds the viewer into `Build\WatchNext` and the training build into
   `Build\TrainingNext`, both inside this worktree.
   - After each build it reverts `ArenaWatch.unity` and `ProjectSettings.asset`.
   - Then it takes the screenshots, using scratch profiles and a read-only copy of the Warrior
     `latest.brain`:
     - `t032_mage*.png` and `t032_archer*.png` (`-speed 8 -enemyShot`);
     - `t032_shop.png` (`-openPanel character`, 2000 gold, Warrior only).
4. `t032_commit.ps1` with `t032_msg.txt`.
   - It stages `Unity/Assets/Arena`, `Unity/Assets/ThirdParty`, `CoreTests` and `docs`.
   - It refuses to commit `Library/`, `.onnx`, `.brain`, `ProjectSettings.asset` or `Build/`.
   - It does not merge or push.

**What changed** (`git diff --stat b722686` is the exact list):

- **Selected class drives the viewer.**
  - New pure helpers in `View/Meta/ClassViewLogic.cs`.
  - `SurvivorWatchController` uses the selected class for:
    - the behavior name and the brain, champion, lineage, history and profile lookups;
    - TRAIN: `--behavior` and the class's own `--run-id`;
    - the owner build and the sim kit.
  - When another class is already training, TRAIN does a graceful stop and then starts the selected
    class, with a notice.
  - A class with no brain yet shows "Chưa có não Mage — bấm HUẤN LUYỆN AI".
  - Changing class restarts the run.
- **Shop.** `View/Meta/CharacterPanel.cs` lists the three classes with price, owned/selected state,
  `Mua (1.500 vàng)` (with the reason when disabled), `Chọn`, and a play-style line. It saves after
  buying or selecting. The rules stay in `ProfileRules`.
- **Hero models.** `KayKitArtSetBuilder` / `SurvivorArtSet` / `SurvivorRenderer`:
  - Mage = KayKit Mage with a staff;
  - Archer = KayKit Rogue_Hooded with a bow;
  - same animator setup.
- **Renderer.**
  - `SurvivorRenderer.Weapons.cs`: visuals for weapons 14–25 by pattern, in class colours.
  - Evolutions 40–57 render like their base, larger and gold-tinted.
  - Skill visuals:
    - fireball with an explosion ring;
    - power shot with a trail;
    - frost-burst ice ring;
    - blink flashes;
    - the mana-shield bubble (the Warrior keeps its front shield).
- **Enemies** (`SurvivorRenderer.Enemies.cs`):
  - exploder: orange-red, swells during windup, explodes;
  - ghost: semi-transparent pale blue, hovers;
  - necromancer: KayKit skeleton-mage walker with a purple tint, and a summon ring and flash on
    `EnemySummoned`.
- **HUD.**
  - Vietnamese descriptions for weapons and evolutions in the level-up cards.
  - The `WeaponEvolved` toast "TIẾN HÓA: <tên>".
  - Death cause "bị Bom xác nổ" (checked).
  - Icons for every class skill and weapon (aliases plus optional own glyphs). Evolutions use the base
    glyph in a gold frame.
  - Credits: `ThirdParty/CREDITS.md` now names the Mage and Rogue_Hooded models.
- **Viewer.** New `-enemyShot` option, which takes `<shot>_enemies.png` once two of the three new enemy
  kinds are alive, or after 300 s of real time.
- **Training host.** `TrainingArenaHost.cs` is unchanged. The t030 version, which exits with code 2 on an
  unknown `--hero-class`, is kept. `BehaviorSetup` and `ClassRegistry` already give each class its own
  behavior and kit.
- **Core fix.** `Core/Meta/FarmSession.cs` now farms with the kit of the farmed class; before, it always
  used the Warrior kit. CoreTest added: `CoreTests/Meta/FarmSessionTests.cs: ClassId_PicksThatClassKit`.

**Tests added (EditMode):**

| File | Methods | Cases | What they cover |
|---|---|---|---|
| `ML/Tests/Editor/BehaviorSetupTests.cs` | 3 | 5 | Mage/Archer: behavior name, obs 2264, branches {9,5,5}; an unknown class throws (no Warrior fallback); every kit fits the skill branch |
| `View/Tests/Editor/ClassViewLogicTests.cs` (new) | 9 | 11 | names; shop state, status and buttons; TRAIN action and switching; run id per class; owner build per class; texts |
| `View/Tests/Editor/TrainingServiceClientTests.cs` | 1 | 3 | TRAIN arguments per class |
| `View/Tests/Editor/SurvivorViewLogicTests.cs` | 12 | about 25 | weapon and evolution visual mapping, projectile looks, enemy models, bubble shield, evolved toast, skill texts, skill bar layout, thrust and dagger geometry, descriptions and colours |
| `View/Tests/Editor/SkillIconFactoryTests.cs` | 3 | 11 | glyphs for every M7 skill and weapon; evolution gold frame |

A Mage `BrainLocator` test for legacy runs already existed.

**Risks / not verified visually.**

- **Visuals.** None of the visuals have been seen yet; the screenshots will tell.
- **Ghost transparency** uses the Standard shader "Fade" mode. If the shader variant is stripped in the
  player, ghosts render opaque.
- **Screenshot brain.** No Mage or Archer brain exists yet, so the screenshots use the Warrior brain (same
  shapes). Play quality in those shots means nothing.
- **Enemy shot timing.** If the hero dies before about 420 s of sim time, `-enemyShot` falls back after 300 s
  of real time and may show only exploders, or none.
- **Descriptions** appear in the level-up cards only; there are no inventory tooltips.
- **Stray file.** `C:\PersonalArena-wt\t028\placeholder.txt` is a stray file from an earlier session
  (outside this branch) and can be deleted.

### Orchestrator verification (Claude, PC)

- **Icons.** `t032_icons.ps1` ran: 10 glyphs downloaded, `CREDITS.md` lists 38 icons.
- **CoreTests 236/236.** One fix: `ClassId_PicksThatClassKit` used `RunSeconds` 30, which is below the
  60 s minimum of `SurvivorConfig.Validate`. It now uses 60.
- **EditMode 238/238.**
- **Reviewer** (verdict: approve, all findings low).
  - Fixed:
    - A status without a behavior (from a pre-M7 service) now counts as the Warrior through
      `ClassViewLogic.StatusBehavior`. No status yet still counts as the selected class.
    - `CharacterPanel` now shows "Không lưu được hồ sơ" when the profile save fails.
  - Left as is: a fixed `-brain` flag can drive another class's kit. This is dev-only, used by
    screenshots.
- **Builds.** `Build/WatchNext` and `Build/TrainingNext` built, then rebuilt after the fixes.
- **Screenshots** (Mage and Archer driven by a copy of the Warrior brain):
  - Mage and Archer show their models, skill bars, Vietnamese level-up descriptions, the mana-shield
    bubble and the power-shot trail.
  - The shop shows prices, the "Thiếu 1.000 vàng" state and Mua/Chọn.
  - The new enemies were checked in a Warrior run with the champion brain (seed 23), because the
    borrowed brain dies before minute 5 with the other kits. At 7:00 the exploders (orange) and a ghost
    (pale violet, see-through) are on screen.
  - The ghost's Standard "Fade" variant ships in the player because Standard is in Always Included
    Shaders (all its variants are built).
