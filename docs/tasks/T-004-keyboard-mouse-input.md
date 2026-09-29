# T-004: Keyboard + mouse hero input

- **Owner:** Codex
- **Status:** todo
- **Milestone:** M1
- **Parallel OK with:** T-005
- **Depends on:** T-003 (`IHeroInputSource`)

## Goal

Play the Warrior by hand. The same mapping later serves `HeroAgent.Heuristic()` (M2), so it
must use the Core `HeroInput` helpers only.

## Files

- Create: `Unity/Assets/Arena/Actors/KeyboardMouseInput.cs`
- Do not touch anything else.

## Spec

`public sealed class KeyboardMouseInput : MonoBehaviour, IHeroInputSource` using the **Input
System** package (`Keyboard.current`, `Mouse.current`), not the legacy `Input` class.

- Move: WASD / arrow keys → `HeroInput.MoveFromAxes(dx, dy)` (W = +y = world +z).
- Turn: raycast the mouse onto the plane y = 0 with `[SerializeField] Camera cam`; target angle
  = `atan2(dz, dx)` from hero position; `HeroInput.TurnToward(sim.Hero.Facing, angle, 0.05f)`.
- Skill (slot numbers are 1-based in `HeroInput.Skill`, 0 = none), priority top to bottom:
  Left mouse = 1 (spear), Right mouse **held** = 3 (block), `E` = 2 (kick), `Space` = 4 (dash).
- Edge-trigger slots 1, 2, 4 (only the frame the button goes down, buffered until the next
  sim tick reads it); slot 3 is held.
- If `Keyboard.current` or `Mouse.current` is null, return `new HeroInput(0, 0, 0)` (idle).

Conventions (checked in `ArenaSim.Step`): `Turn` is −1/0/+1 (`TurnToBranch` maps it to the
ML branch 0/1/2); `Skill` is 0 = none, 1..4 = `HeroDef.Skills[Skill - 1]`.

## Done when

- [ ] Compiles in Unity, no warnings
- [ ] Report filled (describe how you checked the slot numbers against `DefaultDefs.Warrior()`)

## Report (filled by the implementer)

- Changed files:
- Test result:
- Notes / open questions:
