# T-003: `SimConstants`, `Health`, `Energy`, `Cooldown`, `StunTimer`

- **Owner:** Codex
- **Status:** todo
- **Milestone:** M0
- **Parallel OK with:** T-001, T-002
- **Depends on:** none

## Goal

Small combat building blocks, all tick-based so the simulation stays deterministic.

## Files

- Create in `Unity/Assets/Arena/Core/`: `SimConstants.cs`, `Health.cs`, `Energy.cs`,
  `Cooldown.cs`, `StunTimer.cs`
- Create: `CoreTests/CombatStatsTests.cs`
- Do not touch anything else.

## Spec

```csharp
public static class SimConstants
{
    public const int   TickRate = 60;
    public const float Dt = 1f / TickRate;
    public const int   DecisionPeriod = 5;          // agent acts every 5 ticks = 12 Hz
    public static int SecondsToTicks(float s);      // rounds to nearest, never negative
}

public struct Health     // mutable struct, used as a field
{
    public Health(float max);                       // Current = Max; max must be > 0 (ArgumentOutOfRangeException)
    public float Max { get; }  public float Current { get; }
    public bool IsDead { get; }                     // Current <= 0
    public float Fraction { get; }                  // Current / Max in [0,1]
    public float ApplyDamage(float amount);         // returns damage actually dealt (clamped, 0 if dead or amount <= 0)
    public float Heal(float amount);                // returns healed amount; no heal when dead
}

public struct Energy
{
    public Energy(float max, float regenPerSecond);
    public float Max { get; }  public float Current { get; }
    public bool TrySpend(float amount);             // false and unchanged if not enough
    public void Tick();                             // + regenPerSecond * Dt, clamped to Max
}

public struct Cooldown
{
    public Cooldown(int durationTicks);
    public int DurationTicks { get; }  public int RemainingTicks { get; }
    public bool IsReady { get; }                    // RemainingTicks == 0
    public bool TryTrigger();                       // if ready: Remaining = Duration, true
    public void Tick();                             // decrement, not below 0
    public float Normalized { get; }                // Remaining / Duration, 0 when Duration == 0 (for observations)
}

public struct StunTimer
{
    public bool IsStunned { get; }
    public void Apply(int ticks);                   // keeps the longer of current and new
    public void Tick();
}
```

## Tests (must add)

- Health: damage clamps at 0 and returns real amount; overkill; heal clamps at Max; dead can't heal; negative damage ignored; ctor throws on max <= 0.
- Energy: spend fails when short and leaves value; regen over 60 ticks ≈ regenPerSecond (±1e-4); clamp at Max.
- Cooldown: triggers once, ready again after exactly `DurationTicks` ticks; `Normalized` goes 1 → 0.
- StunTimer: longer stun wins, shorter does not shorten; ends after N ticks.
- `SecondsToTicks(0.5f) == 30`, `SecondsToTicks(-1f) == 0`.

## Done when

- [ ] `dotnet test CoreTests` passes, no warnings
- [ ] AGENTS.md §6 invariants hold
- [ ] Report filled

## Report (filled by the implementer)

- Changed files:
- Test result:
- Notes / open questions:
