# M1 Core simulation notes

Implemented the pure C# 9, deterministic 60 Hz arena simulation in
`Unity/Assets/Arena/Core/`, with NUnit coverage in `CoreTests/`.

## Added

- Seeded PCG32 random generation and fresh data-only Warrior/Walker defaults.
- Validated arena configuration with weighted zombie types and stable-ID respawns.
- Hero/zombie state, movement, collision separation, wall clamping, combat events,
  Warrior strike/kick/block/parry/dash, scripted Walker attacks, death and timeout.
- Fixed-size ray observations, hero observations, and event-only reward calculation.
- Tests for determinism, validation, combat, episodes/respawn, sensing, observation
  size, every reward sign, and a 16-zombie performance smoke case.

## M1 boundaries and clarifications

- Projectile, area-burst, and teleport definitions exist but execution throws
  `NotSupportedException`, as allowed for M1.
- Damage event values are the actual HP removed (overkill is excluded).
- Respawning zombies retain their original ID and list slot; their type is retained.
- The four local wall observations are ordered forward, backward, left, right.
- Unity rendering, input adapters, and EditMode integration remain outside this
  headless Core task.
