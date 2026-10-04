# T-049: split View files over 700 lines

- **Owner:** Claude
- **Status:** done
- **Milestone:** M15
- **Parallel OK with:** T-047, T-048, T-049
- **Depends on:** none

## Goal

Every file under `Unity/Assets/Arena/View` is under 700 lines; behaviour is unchanged (move-only).

## Files

- Edit: the large `SurvivorRenderer*.cs`, `SurvivorHud*.cs`, `ArenaEffects*.cs` files (partial classes)
- Do not touch anything else.

## Spec

Move code only, grouped by purpose; no renames of public members, no logic edits.

## Tests (must add)

- EditMode suite stays green; smoke test of 3 classes passes.

## Done when

- [ ] `dotnet test CoreTests` passes, no warnings
- [ ] AGENTS.md §6 invariants hold
- [ ] Report filled

## Report (filled by the implementer)

- Changed files: 13 sources split into 36 files, largest now 647 lines (`LineageStore.cs`). New partials:
  `SurvivorWatchController.{Startup,Profile,Brain,Training,Info,Screenshots}.cs`,
  `SurvivorRenderer.{Hero,Events,PickupMeshes,Projectiles,WeaponEvents,EnemyPresent}.cs`,
  `SurvivorHud.{Refresh,BuildPanels,Widgets}.cs`, `BrainLineagePanel.{Actions,Build}.cs`, `LineageStore.Labels.cs`,
  `TrainingHistoryPanel.Drawing.cs`, `ArenaEffects.Animation.cs`, `BehaviorProfilePanel.Helpers.cs`; plain types moved
  to `LineageModels.cs` and `SurvivorViewTypes.cs`.
- Test result: EditMode 302/302, no compiler warning from `Assets/Arena`; smoke test of 3 classes passed on the build.
- Notes / open questions: whole members were moved by a script (line ranges checked against member boundaries), so
  no logic changed; classes that were not `partial` became `partial`. Fields stayed in their original file so the
  order of field initialisers is unchanged.
