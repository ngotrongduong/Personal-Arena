# T-005: Arena HUD (uGUI)

- **Owner:** Codex
- **Status:** todo
- **Milestone:** M1
- **Parallel OK with:** T-004
- **Depends on:** T-003 (`ArenaRunner.Sim`)

## Goal

Minimal HUD like the video: HP bar, energy bar, 4 cooldown fills, zombies alive, time, kills.

## Files

- Create: `Unity/Assets/Arena/Actors/ArenaHud.cs`
- Do not touch anything else (Claude builds the Canvas in T-006).

## Spec

`public sealed class ArenaHud : MonoBehaviour` with serialized refs:
`ArenaRunner runner; Image hpFill, energyFill; Image[] cooldownFills (4); Text statsText;`
(`UnityEngine.UI`, package `com.unity.ugui` is installed).

- `LateUpdate`: `hpFill.fillAmount = Hero.Hp / HeroDef.MaxHp`; energy likewise with `MaxEnergy`;
  cooldown i = `CooldownRemaining[i] / HeroDef.Skills[i].Cooldown` (0 when Cooldown is 0).
- Kills: subscribe to `runner.TickEvents`, count `SimEventType.ZombieKilled`; reset on arena reset.
- `statsText`: `"Zombies {alive}/{total}   Kills {kills}   {time:0.0}s"`.
- No allocations per frame except the text string (update text at most 10×/s).

## Done when

- [ ] Compiles in Unity, no warnings
- [ ] Report filled

## Report (filled by the implementer)

- Changed files:
- Test result:
- Notes / open questions:
