# T-053: evolutions and own icons for the ring weapons

- **Owner:** Claude
- **Status:** done
- **Milestone:** M16
- **Parallel OK with:** none
- **Depends on:** T-052

## Goal

The four ring weapons (catalog 73–76) evolve like every other weapon and have icons of their own instead of
borrowed glyphs.

## Files

- Core: `SurvivorCatalog.cs` (rows 77–80, `FirstRingEvolutionIndex`), `SurvivorCatalog.Items.cs` (`RingEvolutions`).
- View: `SurvivorRenderer.Rings.cs` (evolved looks), `SurvivorEvolutionStyle.cs`, `SurvivorItemDetails.cs`,
  `SkillIconFactory.cs` (aliases removed), new glyphs in `View/Resources/SkillIcons/`.
- Do not touch: the observation schema (v5), the other evolutions.

## Spec

| Ring | Evolution (row) | Passive | Bodies | Distance |
|---|---|---|---|---|
| Spirit Orbs | Guardian Spirits (77) | Recovery | 4 → 6 | 3.2 → 3.68 m |
| Saw Ring | Razor Tempest (78) | Thorn Armor | 4 → 6 | 1.6 → 1.84 m |
| Frost Halo | Glacier Crown (79) | Time Charm | 5 → 6 | 2.4 → 2.76 m |
| Comet | Starfall (80) | Crown | 2 → 3 | 5 → 5.5 m |

The generic evolution rule gives the rest (damage of level 5 × 1.5, body × 1.2, hit interval × 0.8). Evolved rings
keep their shape, take their evolution's colour and gain one part (halo, ground glow, shard cluster, longer tail).
Glyphs: white shape on transparent, 256 px, drawn by a script in the style of the game-icons.net glyphs.

## Tests (must add)

- `SurvivorRingTests`: every ring evolves, its passive is in every class pool, a chest evolves a maxed ring.
- `RingIconTests`: every ring has its own glyph and no alias; every ring evolution has a style and a summary.

## Done when

- [x] `dotnet test CoreTests` passes, no warnings
- [x] AGENTS.md §6 invariants hold
- [x] Report filled

## Report (filled by the implementer)

- Changed files: as listed above; check flag `-ringDemoEvolved` (with `-perfLog`).
- Test result: CoreTests 653/653, EditMode 315/315, smoke test of 3 classes passed; screenshots of the evolved
  rings, the HUD icons and the codex checked.
- Notes / open questions: the glyphs are the project's own drawings (no download), kept apart from the CC BY glyphs
  of `ThirdParty/GameIcons`. With all four evolved rings at once 21 bodies circle the hero; that is a deliberate
  extreme of the demo, a real build rarely holds more than two.
