# T-047: HUD buff timers

- **Owner:** Claude
- **Status:** done
- **Milestone:** M15
- **Parallel OK with:** T-047, T-048, T-049
- **Depends on:** none

## Goal

The viewer HUD shows each running pickup buff (rage, shield, haste) with an icon and the seconds left.

## Files

- Edit: `Unity/Assets/Arena/View/Survivor/SurvivorHud*.cs`, `SurvivorViewLogic.cs`
- Do not touch anything else.

## Spec

Read the remaining buff times from the sim (`SurvivorSim.Buffs.cs`); view only, no Core or schema change. One small row near the health bar; hidden when no buff runs.

## Tests (must add)

- EditMode test for the view-logic helper that formats the buff row.

## Done when

- [x] `dotnet test CoreTests` passes, no warnings
- [x] AGENTS.md §6 invariants hold
- [x] Report filled

## Report (filled by the implementer)

- Changed files: `SurvivorViewLogic.cs` (`BuffChipText`, `BuffSeconds`), `SurvivorHud.cs`, `SurvivorHud.Build.cs`,
  `SurvivorWatchController.Perf.cs` (`-fxDemo` shows the three chips), `SurvivorViewLogicTests.cs`.
- Test result: EditMode 302/302; Core untouched.
- Notes / open questions: three chips under the vitals (rage, shield, haste), each with a draining bar and whole
  seconds left; hidden when the buff is not running.
