# T-050: viewer polish — outer wood, English text, tidier HUD, item details

- **Owner:** Claude
- **Status:** done
- **Milestone:** M16
- **Parallel OK with:** none
- **Depends on:** T-049

## Goal

The owner's requests of 2026-10-05: the view past the fence is no longer a dark empty field; every text in the
game is English (D-049); the HUD is tidy and professional; weapons and skills show detailed information (D-050).

## Files

- Edit: `Unity/Assets/Arena/View/**`, the display names in `Unity/Assets/Arena/Core/**` (no rule changes),
  the messages of `Trainer/train_service.py` and `Trainer/brain_lineage.py`, tests.
- Do not touch: sim rules, the observation schema (v5), saved brains.

## Spec

1. Scenery: rows of trees around the map, ground textured 60 m past the fence, fog range follows the zoom.
2. English: translate every string literal the game shows; numbers as `1,234` and `12.3`; migrate the old
   default loadout names on load.
3. HUD: smaller hero card, level pill beside the clock, slim skill bar, a right column of three cards (AI info,
   training, menu) hidden by Tab, key list in an overlay (H).
4. Details: tooltips on item and skill slots and richer level-up cards, generated from the catalog numbers.

## Tests (must add)

- `SurvivorItemDetailsTests` (every item has a summary, every level says what changes, recipes, skill numbers).
- `ProfileRulesTests.Sanitize_RenamesTheOldDefaultLoadoutNames_AndKeepsTheOwnersNames`.

## Done when

- [x] `dotnet test CoreTests` passes, no warnings
- [x] AGENTS.md §6 invariants hold
- [x] Report filled

## Report (filled by the implementer)

- Changed files: `SurvivorRenderer.Scenery.cs`, `SurvivorCamera.cs` (wood, fog); 756 string literals in 57 files
  plus the trainer messages (English); `SurvivorHud.Build.cs`, `.BuildPanels.cs`, `.Refresh.cs`, new
  `SurvivorHud.Tooltip.cs`, `HudHoverTarget.cs`, `SurvivorItemDetails.cs` (HUD and details);
  `SurvivorWatchController.*` (keys H and Tab, compact info text, check flags `-cameraAt`, `-hudDemo`).
- Test result: CoreTests 645/645, pytest 204/204, EditMode 308/308, smoke test of 3 classes passed; screenshots of
  the HUD, tooltips, key help, level-up cards and five panels checked at 1600 x 900.
- Notes / open questions: the EXP-gem clutter of STATUS item 2 did not show with the warrior champion (it collects
  them); it will need a look with a weak brain before anything is changed. A codex panel listing every item and
  recipe would be the next step for "more information"; tooltips cover the items the hero owns.
