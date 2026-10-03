# T-037: M9 visuals and sounds for the new weapons, passives, evolutions and skills

- **Owner:** Codex (on the owner's Windows PC)
- **Status:** todo
- **Milestone:** M9 (D-041)
- **Parallel OK with:** none
- **Depends on:** T-041 (do it first; same branch `claude/serene-sagan-3a70q5`)

## Goal

Every M9 item and skill is recognisable in the viewer: an icon on the level-up card and HUD, a visible effect in the
arena, and a sound. Today they run correctly in Core but use fallback visuals.

## What needs graphics (the Reports in the task files list the exact events and fields)

| Catalog ids / skills | What | Data the view reads (see the Report of the task) |
|---|---|---|
| 26–33 | Warrior weapons wave 1a/1b (flame cone, combo blade, heavy hammer, bomb, retaliate, barrier, boomerang, poison pool) | T-036, T-038 |
| 34–37, 58–61 | Passives (duration charm, duplicator, spiked armor, omni box, recovery, clover, greed, crown) | icons only |
| 64–68 | Group C (bounce shot, momentum spirit, time clock, purge, bomb ring) | T-042 |
| 69–72 | Mage/Archer weapons (fireball nova, bracelet trio, quad shot, magi stone) | T-044 |
| 112–127 | Evolutions of the above (names in the catalog; evolution notice already generic) | T-045 |
| skills | war-cry (Warrior slot 3); leap-slam, whirlwind, caltrop-trap, arrow-barrage, fire-wall, chain-lightning (slots 4–5) | T-036, T-043 (`Traps`, `Walls`, `Whirling`, `StrikeLanded` id −1) |

## Files

- Icons: add PNGs to `Unity/Assets/ThirdParty/GameIcons/Resources/SkillIcons/<item-or-skill-id>.png` exactly like the
  existing ones (same size, same import settings via `SkillIconImporter`), and list every new icon with author and
  licence in `Unity/Assets/ThirdParty/CREDITS.md`. Sources must be CC0, MIT or CC BY with credit (AGENTS.md §5; D-025);
  game-icons.net (CC BY 3.0) is what the project already uses.
- Effects: `Unity/Assets/Arena/View/Survivor/SurvivorRenderer.cs` and the existing effect helpers it uses; follow how
  the M4C/M7 weapons are drawn (projectile looks by `SourceIndex`, rings/blasts from `StrikeLanded`, orbit/aura state).
- Sounds: `SoundCueMap.cs` (cases for patterns `Combo, Bomb, Retaliate, Barrier, Boomerang, Zone, Bounce, Momentum,
  Freeze, Purge, BombRing, Trio, Quad, Stone` and the six new `SkillKind`s) using the existing Kenney CC0 / CC0 music
  sets only (no new audio packs unless CC0, credited).
- View tests under `Unity/Assets/Arena/View/Tests/Editor` for the new cases.
- Do not touch `Unity/Assets/Arena/Core`, `CoreTests`, `Trainer/`.

## Spec

- Each effect must read Core state/events only (no game rules in the view, AGENTS.md §6).
- Keep the frame rate: pool any new effect objects like the existing ones; no per-frame allocations in hot paths.
- Run Unity only via `Tools/unity-run.ps1` (summary output; AGENTS.md §8b). Put new effect code in new files by
  feature (e.g. `SurvivorRenderer.M9Weapons.cs`), keeping every file under ~700 lines.
- Rebuild the viewer at the end into `Build/WatchNext` (`-watchBuildOutput Build/WatchNext`, as in T-041; never the default `Build/Watch`).

## Done when

- [ ] EditMode tests pass (paste the count), `dotnet test CoreTests -c Release` still passes
- [ ] Every id in the table has an icon (a test that loops the catalog and checks an icon or a known fallback exists)
- [ ] Screenshots of each new effect, or a short list of which ones you could check visually
- [ ] `CREDITS.md` updated
- [ ] Report filled

## Report (filled by the implementer)

- Changed files:
- Icons added (id → source, author, licence):
- EditMode result:
- Notes / open questions:
