# T-048: remove the first-frame hitch of the viewer

- **Owner:** Claude
- **Status:** todo
- **Milestone:** M15
- **Parallel OK with:** T-047, T-048, T-049
- **Depends on:** none

## Goal

The viewer no longer freezes for about 3 seconds on the first frame of a run (measured with `-perfLog`).

## Files

- Edit: `Unity/Assets/Arena/View/ArenaEffects*.cs`, `FxAssets.cs`, `VfxLibrary` loading
- Do not touch anything else.

## Spec

Find what loads on the first frame (effect pools, store VFX set, textures) and warm it up behind the menu or spread it over several frames. Worst frame after the first second must stay under 100 ms.

## Tests (must add)

- `-perfLog` run: first sample no longer shows a multi-second worst frame.

## Done when

- [ ] `dotnet test CoreTests` passes, no warnings
- [ ] AGENTS.md §6 invariants hold
- [ ] Report filled

## Report (filled by the implementer)

- Changed files:
- Test result:
- Notes / open questions:
