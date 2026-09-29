# M1 Unity view and keyboard play

## Added files

- `Unity/Assets/Arena/View/`: simulation-space conversion, episode stats, input latching,
  procedural renderer, top-down camera, keyboard controller, and runtime uGUI HUD.
- `Unity/Assets/Arena/View/Editor/`: the play-scene builder and editor-only assembly.
- `Unity/Assets/Arena/View/Tests/Editor/`: EditMode tests for coordinate conversion,
  stats counting, and skill input latching/priority.

The renderer is passive: `Bind(ArenaSim)` supplies a simulation, `SyncAfterStep()` records
the latest tick and its visual events, and `SetInterpolationAlpha()` controls interpolation.
It also detects an externally advanced bound simulation during `LateUpdate`. It never reads
gameplay input or advances the simulation itself.

## Controls

| Input | Action |
|---|---|
| W/A/S/D | Move in arena space |
| Mouse | Aim on the arena floor |
| Q / E | Turn left / right after the mouse has been idle for 1.25 seconds |
| Left mouse / J | Spear strike |
| F / K | Kick |
| Right mouse (hold) / L (hold) | Shield block |
| Space / either Shift | Dash |
| R | Restart with the current settings |
| Escape | Pause/resume simulation stepping |
| 1 / 2 / 3 / 4 / 5 / 6 | Restart with 1 / 2 / 4 / 8 / 16 / 32 walkers |

Block has skill priority, followed by strike, kick, and dash. Button presses are latched
until the next 60 Hz simulation tick, so short taps between ticks are retained.

## Build the scene

In the Unity Editor, choose **Personal Arena > Build Play Scene**. For automation, run Unity
with `-executeMethod PersonalArena.View.Editor.PlaySceneBuilder.Build`. The builder creates
`Assets/Scenes/ArenaPlay.unity`, adds it as the first enabled build scene, and exits Unity only
when running in batch mode.

Open `ArenaPlay` and enter Play mode. Arena settings are serialized on the `Arena` object's
`KeyboardArenaController` component.

## Known limits

- All visuals are runtime primitives and intentionally low-poly; there are no imported assets,
  audio effects, or world-space zombie HP bars in M1.
- Mouse aiming yields to Q/E after 1.25 seconds without mouse movement. Moving the mouse restores
  cursor aiming.
- Large arenas use a smooth hero-follow camera; smaller arenas are fully framed.
- This worktree cannot launch Unity, so Unity compilation, EditMode tests, and visual QA must be
  run by the follow-up batchmode/editor pass.
