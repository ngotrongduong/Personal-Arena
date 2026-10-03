# T-041: M9 Unity side of schema v5 — six skills in the viewer, EditMode green, new builds

- **Owner:** Codex (on the owner's Windows PC)
- **Status:** todo
- **Milestone:** M9 (D-041)
- **Parallel OK with:** none (do this before T-037)
- **Depends on:** T-036..T-046 (all in Core/Trainer on branch `claude/serene-sagan-3a70q5`, PR #7)

## Before you start (owner, once)

Codex never runs git (AGENTS.md §3). The owner runs, in `C:\PersonalArena` (training may keep running; builds go to the
`*Next` folders):

```
git fetch origin claude/serene-sagan-3a70q5
git checkout claude/serene-sagan-3a70q5
```

## Goal

Core is now schema v5 (observation 2592, actions 9/7/5, six active skills, catalog 128, 6 weapon + 6 passive slots).
The Unity viewer and training build must compile and work with it: the HUD shows up to six skills and six + six items,
skills 5 and 6 can be used, EditMode tests pass, and fresh `Build/WatchNext` and `Build/TrainingNext` exist.
Graphics for the new items are T-037, not this task (missing icons may use the existing fallback).

## Files

- Edit: `Unity/Assets/Arena/View/Survivor/SurvivorHud.cs` (`SkillSlots = 4` → `SurvivorInput.SkillSlotCount`),
  `SurvivorHud.Build.cs` (skill bar layout), `SurvivorViewLogic.cs` (skill-panel helpers around the `SkillPanelWidth` /
  `SkillSlotX` code), `SurvivorWatchController.cs` (manual keys for skills 5 and 6 if keys 1–4 exist there), and any
  other View/ML file that assumes 4 skills, 4 + 4 items, 2264 inputs or the 9/5/5 branches.
- Edit tests under `Unity/Assets/Arena/ML/Tests/Editor` and `Unity/Assets/Arena/View/Tests/Editor` only where they
  assume the old sizes (`SurvivorViewLogicTests.cs:~235`, `SoundCueMapTests.cs:~85` iterate `ActiveSkills` and may now
  meet `none` entries; tests that loop evolutions only over 40–57 should also cover 112–127).
- Do not touch `Unity/Assets/Arena/Core`, `CoreTests` or `Trainer/` (they are done and reviewed). If you think one of
  them is wrong, stop and write it in the Report instead of changing it.

## Spec

1. **Skill bar:** show one button per non-`none` skill of the current class, in slot order, up to 6. The Archer has
   `none` in slot 3 (skills are in 0, 1, 2, 4, 5): the bar must not leave a gap and the cooldown sweep must follow the
   right slot. Mage and Warrior use all six. Keep the existing look (icon, cooldown sweep, no key hints).
2. **Items:** weapons and passives rows show up to `SurvivorTuning.MaxWeaponSlots` / `MaxPassiveSlots` (6 each).
   Check at 1920×1080 and 1280×720 that nothing overlaps.
3. **Manual input** (if the viewer has keys for skills): keys 1–6 map to skill choices 1–6.
4. **Brains:** the viewer loads only schema-5 brains (`BrainLocator.CurrentSchemaVersion` already follows Core). Old
   schema-4 brains must give the existing "old brain" message, never a crash.
5. **Builds:** run `PersonalArena.ML.Editor.TrainingBuild.BuildWindows` with `-buildPath Build/TrainingNext` and
   `PersonalArena.View.Editor.WatchBuild.BuildWindows` with `-watchBuildOutput Build/WatchNext` (batchmode, always with
   `-quit`). **Without those arguments the scripts overwrite `Build/Training` and `Build/Watch`, which the owner may be
   running: always pass them.** The services swap the `*Next` builds in on the next TRAIN / viewer start.
6. **Smoke test:** run the viewer build with `-smokeTest <report.json>` and a temporary `-profile`, as in T-034. The
   champions on disk are schema 4, so the viewer will not load them: make a schema-5 copy with
   `Trainer/brain_upgrade.py` into a TEMP folder (never modify `Trainer/runs/` in place) if the smoke test needs a brain,
   or report what the smoke test does without one.

7. **Split the large View files** (after 1–6 work, same task). These files are too big to read cheaply; split each
   into `partial class` files (or small helper classes) **by function**, moving code only, no behaviour change:

   | File (lines) | Split idea |
   |---|---|
   | `SurvivorWatchController.cs` (1719) | `.Brains` (load/swap brain), `.Input` (keys), `.Panels` (menus/panels wiring), `.Training` (service client) |
   | `BrainLineagePanel.cs` (1277) | `.Build` (UI creation), `.Tree` (branch tree), `.Actions` (buttons) |
   | `SurvivorRenderer.cs` (1051) | by drawn thing: hero, pickups, effects pool (weapons/enemies are already separate) |
   | `LineageStore.cs` (1034) | `.Read`, `.Write`, `.Model` |
   | `SurvivorHud.Build.cs` (930), `SurvivorHud.cs` (888) | `.SkillBar`, `.Items`, `.LevelUp`, `.EndScreen` |
   | `TrainingHistoryPanel.cs` (828), `SurvivorRenderer.Weapons.cs` (798), `ArenaEffects.cs` (754), `SurvivorRenderer.Enemies.cs` (725), `BehaviorProfilePanel.cs` (720) | split by section so each part is under ~500 lines |

   Target: no file in `Unity/Assets/Arena/View` or `Unity/Assets/Arena/ML` above ~700 lines. Each new `.cs` needs a
   `.meta` (let Unity generate it in the compile run). Proof: compile clean and the same EditMode count passes as
   before the split. Use search to find each region; do not read these files whole.

## Hard rules

- No git commands. No changes to `Trainer/runs/` (the owner's trained brains). Do not stop the owner's running training.
- Run Unity only through `Tools/unity-run.ps1` (AGENTS.md §8, §8b): `-Compile`, `-Tests EditMode`,
  `-Method <X> -ExtraArgs "..."`. It prints a short summary; never read whole Unity logs or test XML.
- Every new Unity asset gets its `.meta`. Do not commit (you cannot) `Library/`, builds, `results/`.
- Do not change `ProjectSettings.asset` scripting defines (the EditMode run can remove `SENTIS_ANALYTICS_ENABLED`: restore it).

## Done when

- [ ] `dotnet test CoreTests -c Release` still passes (nothing in Core changed)
- [ ] Unity EditMode tests all pass (paste the count)
- [ ] `Build/TrainingNext` and `Build/WatchNext` built without errors
- [ ] Screenshots (or a description) of the HUD at 1920×1080 and 1280×720 for Warrior, Mage and Archer with 6 skills shown
- [ ] No View/ML file above ~700 lines (paste `Get-ChildItem` line counts of the split files)
- [ ] Report filled

## Report (filled by the implementer)

- Changed files:
- EditMode result:
- Build result:
- Smoke test result:
- Notes / open questions:
