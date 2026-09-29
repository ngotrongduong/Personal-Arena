# T-001: Deterministic `Rng` (PCG32)

- **Owner:** Codex
- **Status:** todo
- **Milestone:** M0
- **Parallel OK with:** T-002, T-003
- **Depends on:** none

## Goal

A seeded random generator that is the only source of randomness in Core (AGENTS.md §6).

## Files

- Create: `Unity/Assets/Arena/Core/Rng.cs`, `CoreTests/RngTests.cs`
- Do not touch anything else.

## Spec

`namespace PersonalArena.Core`, `public sealed class Rng` implementing PCG32 (XSH RR,
64-bit state, multiplier `6364136223846793005UL`).

```csharp
public Rng(ulong seed, ulong stream = 54UL);
public ulong State { get; }            // for save/replay
public ulong Increment { get; }
public static Rng FromState(ulong state, ulong increment);
public uint NextUInt();
public int Range(int minInclusive, int maxExclusive);   // unbiased (rejection); throws ArgumentOutOfRangeException if max <= min
public float NextFloat();                                 // [0, 1), 24-bit mantissa: (NextUInt() >> 8) * (1f / 16777216f)
public float Range(float minInclusive, float maxExclusive);
public bool Chance(float probability);                    // p <= 0 → false, p >= 1 → true
public Vec2 InsideUnitCircle();                           // rejection sampling
public Rng Fork();                                        // new independent Rng seeded from this one
```

- Seeding must follow the reference PCG32 `pcg32_srandom_r` so outputs match the reference.
- No `System.Random`, no static state, no allocations in `Next*`/`Range`.

## Tests (must add)

- `SameSeedSameSequence`: two `Rng(42)` give identical first 1000 `NextUInt()`.
- `DifferentSeedsDiffer`.
- `MatchesReferenceVector`: seed 42, stream 54 → first 6 outputs
  `0xa15c02b7, 0x7b47f409, 0xba1d3330, 0x83d2f293, 0xbfa4784b, 0xcbed606e` (pcg32-demo).
- `IntRangeStaysInBounds` (100k draws, both ends hit), `IntRangeThrowsOnEmpty`.
- `FloatInHalfOpenUnitInterval` (never 1.0).
- `StateRoundTrip`: `FromState(r.State, r.Increment)` continues the same sequence.
- `IntRangeRoughlyUniform`: 60k draws in [0,6), each bucket within ±5%.

## Done when

- [ ] `dotnet test CoreTests` passes, no warnings
- [ ] AGENTS.md §6 invariants hold
- [ ] Report filled

## Report (filled by the implementer)

- Changed files:
- Test result:
- Notes / open questions:
