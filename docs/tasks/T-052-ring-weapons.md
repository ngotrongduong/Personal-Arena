# T-052: ring weapons shared by every class, smaller purge effect

- **Owner:** Claude
- **Status:** done
- **Milestone:** M16
- **Parallel OK with:** none
- **Depends on:** T-051

## Goal

The owner's requests of 2026-10-05: the Purge effect is too big; more weapons that circle the hero at a fixed
distance; weapons may be shared by all three classes instead of belonging to one class.

## Files

- Core: `SurvivorDefs.cs` (`WeaponPattern.Ring`), new `SurvivorSim.Weapons.Rings.cs`, `SurvivorCatalog*.cs`
  (rows 73–76), `SurvivorClassKits.cs` (pools), `SurvivorEntities.cs`, `SurvivorSim.Enemies.cs`, `SurvivorSim.Weapons.cs`.
- View: new `SurvivorRenderer.Rings.cs`, `SurvivorRenderer.M9.cs` (purge), `SurvivorItemDetails.cs`,
  `SurvivorViewLogic.cs`, `SkillIconFactory.cs`, `SurvivorWatchController.Profile.cs` (check flags).
- Do not touch: the observation schema (v5) and the existing orbit weapons.

## Spec

1. `WeaponPattern.Ring` (D-051): bodies circle the hero all the time (no cooldown) at `BaseRange`; each ring keeps
   its own angle and per-enemy hit timer by inventory slot, so several rings turn at once. The old Orbit weapons
   (one volley state, on for a while then on cooldown) are unchanged, bit for bit.
2. Four rings in every class pool: Spirit Orbs (3.2 m), Saw Ring (1.6 m, fast), Frost Halo (2.4 m, turns the other
   way, strong knockback), Comet (5 m, heavy). No evolutions yet.
3. Purge effect: one ring that grows to the real radius plus a faint fill; no blast scaled to 14 m.

## Tests (must add)

- `SurvivorRingTests`: pools and distances, bodies stay at the distance and never stop, damage formula, hit
  interval, two rings at once with their own timers, area and amount bonuses.

## Done when

- [x] `dotnet test CoreTests` passes, no warnings
- [x] AGENTS.md §6 invariants hold
- [x] Report filled

## Report (filled by the implementer)

- Changed files: as listed above; seven Core tests that pinned the old pools and "row 73 is empty" updated.
- Test result: CoreTests 651/651 (goldens unchanged), EditMode 313/313; screenshots of the rings and of the purge
  ring checked. Check flags (with `-perfLog`): `-startWeapon <index>`, `-ringDemo`.
- Notes / open questions: the rings reuse existing icon glyphs with their own colours (no new art downloaded). A ring
  alone is a weak starting weapon; they are meant as additions. Brains have never seen rows 73–76, so picks of
  them are untrained until the next TRAIN. First versions had trails and pack ball effects on the bodies: at x8 the
  trails became full circles and the pack effects left particles behind, so both were removed.
