# T-006: `ManualArena` scene — play by hand (M1 acceptance)

- **Owner:** Claude (PC linked, via Unity MCP or `unity:unity-cli`) — fallback: Owner with the steps below
- **Status:** todo
- **Milestone:** M1
- **Depends on:** T-003, T-004, T-005

## Goal

Owner opens `Unity/Assets/Arena/Scenes/ManualArena.unity`, presses Play and fights N Walkers
with the Warrior. This closes M1.

## Steps

1. Scene `Assets/Arena/Scenes/ManualArena.unity`, add to Build Settings.
2. Floor: Plane scaled to 20×20 m; 4 wall cubes on the edges (visual only; Core clamps).
3. Hero: Capsule (blue) + `HeroView`; a small cube in front as "spear" to show facing.
4. Zombie prefab `Assets/Arena/Prefabs/Walker.prefab`: Capsule (green) + `ZombieView`.
5. `ArenaRunner` on an empty "Arena" object, `inputSource` = `KeyboardMouseInput` on the same object.
6. Camera: orthographic or 60° perspective looking down from (0, 22, −8), follows nothing (arena fits).
7. Canvas (Screen Space Overlay) with bars + text wired to `ArenaHud`.
8. EditMode smoke test: load the runner logic headless — create `ArenaSim` like the runner and
   step 600 ticks with idle input, assert no exception and `Time ≈ 10 s`.
9. Owner plays: can move, turn to mouse, strike, kick, block, dash; zombies die and respawn.

## Done when

- [ ] Owner confirms playable → set M1 done in `docs/STATUS.md`, merge `develop` → `main`
- [ ] Screenshot saved to the session (not committed)
