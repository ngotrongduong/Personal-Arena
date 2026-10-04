# T-048: remove the first-frame hitch of the viewer

- **Owner:** Claude
- **Status:** done
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

- Changed files: `Unity/ProjectSettings/ProjectSettings.asset` (Unity splash off), new `View/PerfTrace.cs`,
  `SurvivorWatchController.Perf.cs` (+ marks in `SurvivorWatchController.cs`, `SurvivorRenderer.cs`, `SurvivorHud.cs`,
  `SurvivorAudio.cs`, `ArenaEffects.Store.cs`), `Tools/unity-run.ps1` (example path).
- Test result: EditMode 302/302. `-perfLog` on the built viewer (1600x900): first sample worst frame 2909 ms → 17 ms;
  first frame on screen at about 0.85 s after launch instead of 3.9 s; 60 s at x8 (6 weapons) worst frame 50 ms.
- Notes / open questions: the "3 s hitch" was not a stutter in the run. `-perfLog` counted the launch as frame 1,
  and 2.2 s of that launch was the Unity splash screen (plus 0.6 s it added before `Start`). Effects were never the
  cause: the store set loads in 22 ms and no effect took 2 ms to create. So the files listed in the spec were the
  wrong place; the fix is the splash setting, and `-perfLog` now writes the launch to `*_startup.csv`
  (steps and every frame over 50 ms) instead of counting it as a frame.
