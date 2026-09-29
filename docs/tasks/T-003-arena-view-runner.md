# T-003: Unity view layer — `ArenaRunner`, `HeroView`, `ZombieView`

- **Owner:** Codex
- **Status:** todo
- **Milestone:** M1
- **Parallel OK with:** T-001, T-002
- **Depends on:** none (Core M1 is merged on `develop`)

## Goal

MonoBehaviours that own an `ArenaSim`, advance it at exactly 60 ticks/s and mirror its state
onto GameObjects. Code only; Claude builds the scene in T-006.

## Files

- Create `Unity/Assets/Arena/Actors/`:
  - `PersonalArena.Actors.asmdef` (name `PersonalArena.Actors`, references `PersonalArena.Core`
    and `Unity.InputSystem`, root namespace `PersonalArena.Actors`)
  - `IHeroInputSource.cs`, `ArenaRunner.cs`, `HeroView.cs`, `ZombieView.cs`, `SimToUnity.cs`
- Do not touch Core, ProjectSettings or any scene. Do not write `.meta` files (Unity creates them).

## Spec

```csharp
namespace PersonalArena.Actors
{
    public interface IHeroInputSource { HeroInput Read(ArenaSim sim); }

    public static class SimToUnity
    {
        public static Vector3 ToWorld(Vec2 p, float y = 0f);     // (p.X, y, p.Y)
        public static Quaternion ToRotation(float facingRadians); // facing 0 = +x, CCW positive
                                                                  // => Quaternion.Euler(0, 90 - deg, 0)
    }

    public sealed class ArenaRunner : MonoBehaviour
    {
        [SerializeField] float width = 20f, height = 20f;
        [SerializeField] int zombieCount = 3;
        [SerializeField] int seed = 1;
        [SerializeField] float speed = 1f;            // 1 = real time; ticks/frame capped at 8
        [SerializeField] MonoBehaviour inputSource;   // must implement IHeroInputSource; null = idle input
        [SerializeField] HeroView heroView;
        [SerializeField] ZombieView zombiePrefab;
        [SerializeField] bool autoResetOnDone = true;

        public ArenaSim Sim { get; private set; }
        public event Action<IReadOnlyList<SimEvent>> TickEvents; // raised after every Step
        public void ResetArena(int? seed = null);
    }
}
```

- Build the sim in `Awake`: `new ArenaSim(DefaultDefs.Warrior(), new ArenaConfig { Width, Height, ZombieCount, Seed })`.
- **Do not rely on `FixedUpdate`:** the project's fixed timestep is 0.02 s. In `Update`, add
  `Time.deltaTime * speed` to an accumulator and call `Sim.Step(input)` while the accumulator
  ≥ `ArenaSim.FixedDeltaTime`, max 8 steps per frame (drop the rest).
- `ZombieView` instances are pooled by list index of `Sim.Zombies`; hide when `!Alive`.
- `HeroView` / `ZombieView` expose `Sync(...)` called by the runner after stepping, and lerp
  visual position toward the latest sim position for smoothness (render only; never write back).
- When `Sim.Done`: if `autoResetOnDone`, `ResetArena()`; else stop stepping.
- No game rules here; everything is read from Core state.

## Tests

- Add `Unity/Assets/Arena/Tests/EditMode/PersonalArena.Tests.EditMode.asmdef` (Editor only,
  references Core, Actors, `UnityEngine.TestRunner`, `UnityEditor.TestRunner`, nunit) and
  `SimToUnityTests.cs`: `ToWorld` maps Y→z; `ToRotation(0)` faces +x; `ToRotation(π/2)` faces +z.

## Done when

- [ ] Compiles in Unity 6000.3 with no warnings (owner or Claude opens the project)
- [ ] `dotnet test CoreTests` still passes
- [ ] Report filled

## Report (filled by the implementer)

- Changed files:
- Test result:
- Notes / open questions:
