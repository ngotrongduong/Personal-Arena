# T-047: HUD buff timers

- **Owner:** Claude
- **Status:** todo
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

- [ ] `dotnet test CoreTests` passes, no warnings
- [ ] AGENTS.md §6 invariants hold
- [ ] Report filled

## Report (filled by the implementer)

- Changed files:
- Test result:
- Notes / open questions:
