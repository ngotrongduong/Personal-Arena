# T-049: split View files over 700 lines

- **Owner:** Claude
- **Status:** todo
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

- Changed files:
- Test result:
- Notes / open questions:
