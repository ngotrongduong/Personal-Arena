using PersonalArena.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PersonalArena.View
{
    /// <summary>Owns and steps the manual-play ArenaSim; rendering remains passive.</summary>
    public sealed class KeyboardArenaController : MonoBehaviour
    {
        private const int MaximumTicksPerFrame = 8;
        private const float MouseAimHoldSeconds = 1.25f;
        private const float TurnDeadZoneRadians = 0.06981317f;

        [Header("Arena")]
        [SerializeField, Range(8f, 80f)] private float arenaWidth = 20f;
        [SerializeField, Range(8f, 80f)] private float arenaHeight = 20f;
        [SerializeField, Range(0, 64)] private int zombieCount = 4;
        [SerializeField, Range(0.25f, 4f)] private float hpMultiplier = 1f;
        [SerializeField, Range(0.25f, 4f)] private float damageMultiplier = 1f;
        [SerializeField, Range(0.25f, 4f)] private float speedMultiplier = 1f;
        [SerializeField, Min(1f)] private float episodeSeconds = 120f;
        [SerializeField] private int seed = 1;
        [SerializeField] private bool respawnKilledZombies = true;

        [Header("Scene References")]
        [SerializeField] private ArenaRenderer arenaRenderer;
        [SerializeField] private ArenaHud arenaHud;
        [SerializeField] private TopDownCamera topDownCamera;

        private readonly InputLatch inputLatch = new InputLatch();
        private readonly ArenaStats stats = new ArenaStats();
        private ArenaSim sim;
        private float accumulator;
        private float lastMouseMoveTime = float.NegativeInfinity;
        private bool paused;

        public ArenaSim Sim => sim;
        public ArenaStats Stats => stats;
        public bool Paused => paused;

        private void Start()
        {
            ResolveReferences();
            CreateSimulation();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (keyboard != null)
            {
                if (keyboard.rKey.wasPressedThisFrame)
                {
                    CreateSimulation();
                    return;
                }

                int preset = ReadPreset(keyboard);
                if (preset >= 0)
                {
                    zombieCount = preset;
                    CreateSimulation();
                    return;
                }

                if (keyboard.escapeKey.wasPressedThisFrame)
                {
                    paused = !paused;
                    accumulator = 0f;
                    arenaHud.SetPaused(paused);
                }
            }

            bool strikePressed = (mouse != null && mouse.leftButton.wasPressedThisFrame) ||
                (keyboard != null && keyboard.jKey.wasPressedThisFrame);
            bool kickPressed = keyboard != null && (keyboard.fKey.wasPressedThisFrame || keyboard.kKey.wasPressedThisFrame);
            bool dashPressed = keyboard != null && (keyboard.spaceKey.wasPressedThisFrame ||
                keyboard.leftShiftKey.wasPressedThisFrame || keyboard.rightShiftKey.wasPressedThisFrame);
            bool blockHeld = (mouse != null && mouse.rightButton.isPressed) ||
                (keyboard != null && keyboard.lKey.isPressed);
            inputLatch.Capture(strikePressed, kickPressed, dashPressed, blockHeld);

            if (sim == null || paused || sim.Done)
            {
                arenaRenderer.SetInterpolationAlpha(1f);
                return;
            }

            if (mouse != null && mouse.delta.ReadValue().sqrMagnitude > 0.01f)
            {
                lastMouseMoveTime = Time.unscaledTime;
            }

            accumulator += Mathf.Min(Time.unscaledDeltaTime, ArenaSim.FixedDeltaTime * MaximumTicksPerFrame);
            int ticks = 0;
            while (accumulator >= ArenaSim.FixedDeltaTime && ticks < MaximumTicksPerFrame && !sim.Done)
            {
                HeroInput input = BuildInput(keyboard, mouse);
                sim.Step(input);
                stats.RecordTick(sim);
                arenaRenderer.SyncAfterStep();
                accumulator -= ArenaSim.FixedDeltaTime;
                ticks++;
            }

            if (ticks == MaximumTicksPerFrame && accumulator >= ArenaSim.FixedDeltaTime)
            {
                accumulator %= ArenaSim.FixedDeltaTime;
            }
            arenaRenderer.SetInterpolationAlpha(accumulator / ArenaSim.FixedDeltaTime);
        }

        private HeroInput BuildInput(Keyboard keyboard, Mouse mouse)
        {
            int dx = 0;
            int dy = 0;
            if (keyboard != null)
            {
                dx = (keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0);
                dy = (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0);
            }

            int turn = 0;
            bool useMouseAim = mouse != null && Time.unscaledTime - lastMouseMoveTime <= MouseAimHoldSeconds;
            if (useMouseAim && TryGetMouseArenaPoint(mouse, out Vec2 target))
            {
                Vec2 offset = target - sim.Hero.Position;
                if (offset.LengthSquared > 0.001f)
                {
                    turn = HeroInput.TurnToward(sim.Hero.Facing, offset.Angle(), TurnDeadZoneRadians);
                }
            }
            else if (keyboard != null)
            {
                turn = (keyboard.qKey.isPressed ? 1 : 0) - (keyboard.eKey.isPressed ? 1 : 0);
            }

            return HeroInput.FromAxes(dx, dy, turn, inputLatch.ConsumeSkill());
        }

        private bool TryGetMouseArenaPoint(Mouse mouse, out Vec2 point)
        {
            Camera camera = topDownCamera != null ? topDownCamera.Camera : Camera.main;
            if (camera != null)
            {
                Ray ray = camera.ScreenPointToRay(mouse.position.ReadValue());
                Plane ground = new Plane(Vector3.up, Vector3.zero);
                if (ground.Raycast(ray, out float distance))
                {
                    point = ArenaSpace.ToSim(ray.GetPoint(distance));
                    return true;
                }
            }

            point = Vec2.Zero;
            return false;
        }

        private void CreateSimulation()
        {
            ResolveReferences();
            ArenaConfig config = new ArenaConfig
            {
                Width = arenaWidth,
                Height = arenaHeight,
                ZombieCount = zombieCount,
                ZombieSpawns = new[] { new ZombieSpawnEntry(DefaultDefs.Walker()) },
                HpMultiplier = hpMultiplier,
                DamageMultiplier = damageMultiplier,
                SpeedMultiplier = speedMultiplier,
                EpisodeSeconds = episodeSeconds,
                RespawnKilledZombies = respawnKilledZombies,
                Seed = seed
            };
            sim = new ArenaSim(DefaultDefs.Warrior(), config);
            stats.Reset();
            inputLatch.Clear();
            accumulator = 0f;
            paused = false;
            lastMouseMoveTime = float.NegativeInfinity;
            arenaRenderer.Bind(sim);
            arenaHud.Bind(sim, stats);
            arenaHud.SetPaused(false);
            topDownCamera.Bind(sim);
        }

        private void ResolveReferences()
        {
            if (arenaRenderer == null)
            {
                arenaRenderer = GetComponent<ArenaRenderer>();
            }
            if (arenaHud == null)
            {
                arenaHud = GetComponent<ArenaHud>();
            }
            if (topDownCamera == null)
            {
                topDownCamera = Object.FindFirstObjectByType<TopDownCamera>();
            }

            if (arenaRenderer == null || arenaHud == null || topDownCamera == null)
            {
                enabled = false;
                throw new MissingReferenceException("KeyboardArenaController requires ArenaRenderer, ArenaHud, and TopDownCamera.");
            }
        }

        private static int ReadPreset(Keyboard keyboard)
        {
            if (keyboard.digit1Key.wasPressedThisFrame) return 1;
            if (keyboard.digit2Key.wasPressedThisFrame) return 2;
            if (keyboard.digit3Key.wasPressedThisFrame) return 4;
            if (keyboard.digit4Key.wasPressedThisFrame) return 8;
            if (keyboard.digit5Key.wasPressedThisFrame) return 16;
            if (keyboard.digit6Key.wasPressedThisFrame) return 32;
            return -1;
        }

        private void OnValidate()
        {
            arenaWidth = Mathf.Clamp(arenaWidth, 8f, 80f);
            arenaHeight = Mathf.Clamp(arenaHeight, 8f, 80f);
            zombieCount = Mathf.Clamp(zombieCount, 0, 64);
            hpMultiplier = Mathf.Clamp(hpMultiplier, 0.25f, 4f);
            damageMultiplier = Mathf.Clamp(damageMultiplier, 0.25f, 4f);
            speedMultiplier = Mathf.Clamp(speedMultiplier, 0.25f, 4f);
            episodeSeconds = Mathf.Max(1f, episodeSeconds);
        }
    }
}
