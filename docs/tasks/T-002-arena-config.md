# T-002: `ArenaConfig` + validation + presets

- **Owner:** Codex
- **Status:** todo
- **Milestone:** M0
- **Parallel OK with:** T-001, T-003
- **Depends on:** none

## Goal

One plain data type that describes an arena the player (or the curriculum) chooses, with
validation and three presets.

## Files

- Create: `Unity/Assets/Arena/Core/ZombieKind.cs`, `Unity/Assets/Arena/Core/ArenaConfig.cs`,
  `CoreTests/ArenaConfigTests.cs`
- Do not touch anything else.

## Spec

```csharp
public enum ZombieKind { Walker = 0, Runner = 1, Brute = 2, Spitter = 3 }

public sealed class ArenaConfig
{
    public const int ZombieKindCount = 4;
    public float Width  = 20f;          // metres, allowed [10, 60]
    public float Height = 20f;          // metres, allowed [10, 60]
    public int   ZombieCount = 4;       // allowed [1, 32]
    public float[] KindWeights = { 1f, 0f, 0f, 0f }; // length 4, each >= 0, sum > 0
    public float HealthMultiplier = 1f; // allowed [0.25, 4]
    public float DamageMultiplier = 1f; // allowed [0.25, 4]
    public float SpawnInterval = 1f;    // seconds between spawns, allowed [0, 10]
    public ulong Seed = 1;

    public bool Validate(List<string> errors);   // appends one message per problem, returns errors added == 0
    public ArenaConfig Clone();                  // deep copy (KindWeights too)
    public float KindProbability(ZombieKind kind); // weight / sum

    public static ArenaConfig Easy();    // 20x20, 2 zombies, Walker only, x0.75 health/damage
    public static ArenaConfig Normal();  // 25x25, 6 zombies, Walker 3 / Runner 1, x1
    public static ArenaConfig Hard();    // 30x30, 12 zombies, Walker 2 / Runner 2 / Brute 1 / Spitter 1, x1.5
}
```

- Presets return new instances (no shared static mutable objects).
- Validation messages name the field, e.g. `"ZombieCount 40 out of range [1, 32]"`.
- NaN or infinity in any float is an error.

## Tests (must add)

- Each preset validates with no errors.
- Each out-of-range field produces exactly one error mentioning the field name.
- `KindWeights` wrong length / negative / all zero → error.
- `Clone` is deep: changing clone's `KindWeights[0]` leaves the original unchanged.
- `KindProbability` sums to 1 for Hard.

## Done when

- [ ] `dotnet test CoreTests` passes, no warnings
- [ ] AGENTS.md §6 invariants hold
- [ ] Report filled

## Report (filled by the implementer)

- Changed files:
- Test result:
- Notes / open questions:
