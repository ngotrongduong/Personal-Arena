# T-051: codex panel

- **Owner:** Claude
- **Status:** done
- **Milestone:** M16
- **Parallel OK with:** none
- **Depends on:** T-050

## Goal

A panel (key K, menu button CODEX) that lists every weapon, passive, evolution and skill with full details, so the
owner can read what anything does without waiting for the AI to pick it.

## Files

- New: `View/Meta/CodexLogic.cs`, `View/Meta/CodexPanel.cs`, `View/Tests/Editor/CodexLogicTests.cs`.
- Edit: `SurvivorHud*.cs` (panel, menu card of nine buttons), `SurvivorWatchController*.cs` (key, smoke test, screenshots).

## Spec

Pages WEAPONS, PASSIVES, EVOLUTIONS, SKILLS; a class filter (ALL or one class; opens on the class being watched).
A grid of icons; a click shows the summary, the numbers of level 1, what every level adds, the evolution recipe
and the classes. Items the hero on screen owns are marked with their level. Text comes from
`SurvivorItemDetails` (catalog numbers), nothing hand-written per item.

## Tests (must add)

- `CodexLogicTests`: pages share no row, a class lists its own pools and the evolutions of its weapons, details
  give every level and the recipe.

## Done when

- [x] `dotnet test CoreTests` passes, no warnings
- [x] AGENTS.md §6 invariants hold
- [x] Report filled

## Report (filled by the implementer)

- Changed files: as listed above; `-openPanel codex[:page[:entry]]` added for screenshots.
- Test result: EditMode 313/313; the smoke test opens and closes the codex (K) with the other panels.
- Notes / open questions: none.
