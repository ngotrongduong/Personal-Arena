using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using PersonalArena.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PersonalArena.View
{
    /// <summary>
    /// "Watch AI" mode: a trained brain plays the arena at real speed and the newest checkpoint
    /// exported by Trainer/export_brain.py is hot-loaded while training keeps running.
    /// </summary>
    public sealed class AiArenaController : MonoBehaviour
    {
        private const int MaximumTicksPerFrame = 24;
        private const float BrainPollSeconds = 2f;
        private const float RestartDelaySeconds = 3f;
        private const float SelfTestTolerance = 1e-3f;
        private const int RecentEpisodeCount = 10;
        private static readonly int[] SpeedSteps = { 1, 2, 4 };

        [Header("Arena")]
        [SerializeField, Range(8f, 80f)] private float arenaSize = 20f;
        [SerializeField, Range(0, 64)] private int zombieCount = 4;
        [SerializeField, Min(1f)] private float episodeSeconds = 120f;
        [SerializeField] private int seed = 1;

        [Header("Brain")]
        [SerializeField] private string behaviorName = "Warrior";
        [Tooltip("Optional fixed brain file; otherwise the newest Trainer/runs/*/<Behavior>/latest.brain is used.")]
        [SerializeField] private string brainFile = string.Empty;

        [Header("Scene References")]
        [SerializeField] private ArenaRenderer arenaRenderer;
        [SerializeField] private ArenaHud arenaHud;
        [SerializeField] private TopDownCamera topDownCamera;

        private readonly ArenaStats stats = new ArenaStats();
        private readonly BrainPilot pilot = new BrainPilot();
        private readonly Queue<EpisodeResult> recent = new Queue<EpisodeResult>();
        private ArenaSim sim;
        private string runsDirectory;
        private string loadedPath;
        private DateTime loadedWriteTime;
        private DateTime loadedAt;
        private string brainStatus = "Waiting for the AI to save its first brain...";
        private float nextPoll;
        private float accumulator;
        private float doneTimer;
        private int speedIndex;
        private int episode;
        private bool paused;
        private bool resultRecorded;

        public ArenaSim Sim => sim;
        public BrainPilot Pilot => pilot;

        private void Start()
        {
            ResolveReferences();
            string requestedBrain = CommandLineValue("-brain");
            if (!string.IsNullOrWhiteSpace(requestedBrain))
            {
                brainFile = requestedBrain;
            }

            runsDirectory = CommandLineValue("-runs");
            if (string.IsNullOrWhiteSpace(runsDirectory))
            {
                runsDirectory = BrainLocator.FindRunsDirectory(Application.dataPath);
            }

            arenaHud.SetHelpText(
                "AI is playing   1-6 zombies: 1/2/4/8/16/32   Space speed   T choice mode   R new round   Esc pause");
            arenaHud.SetResultFooter("Next round starts automatically");
            PollBrain();
            CreateSimulation();
        }

        private void Update()
        {
            HandleKeys();
            if (Time.unscaledTime >= nextPoll)
            {
                PollBrain();
            }

            if (sim == null)
            {
                return;
            }

            if (sim.Done)
            {
                RecordResultOnce();
                arenaRenderer.SetInterpolationAlpha(1f);
                doneTimer += Time.unscaledDeltaTime;
                if (doneTimer >= RestartDelaySeconds && !paused)
                {
                    seed++;
                    CreateSimulation();
                }

                return;
            }

            if (paused || pilot.Brain == null)
            {
                arenaRenderer.SetInterpolationAlpha(1f);
                return;
            }

            int speed = SpeedSteps[speedIndex];
            float step = ArenaSim.FixedDeltaTime;
            accumulator += Mathf.Min(Time.unscaledDeltaTime * speed, step * MaximumTicksPerFrame);
            int ticks = 0;
            while (accumulator >= step && ticks < MaximumTicksPerFrame && !sim.Done)
            {
                sim.Step(pilot.NextInput(sim));
                stats.RecordTick(sim);
                arenaRenderer.SyncAfterStep();
                accumulator -= step;
                ticks++;
            }

            if (ticks == MaximumTicksPerFrame && accumulator >= step)
            {
                accumulator %= step;
            }

            arenaRenderer.SetInterpolationAlpha(accumulator / step);
        }

        private void HandleKeys()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.rKey.wasPressedThisFrame)
            {
                seed++;
                CreateSimulation();
                return;
            }

            int preset = ReadPreset(keyboard);
            if (preset >= 0)
            {
                zombieCount = preset;
                seed++;
                CreateSimulation();
                return;
            }

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                paused = !paused;
                accumulator = 0f;
                arenaHud.SetPaused(paused);
            }

            if (keyboard.spaceKey.wasPressedThisFrame)
            {
                speedIndex = (speedIndex + 1) % SpeedSteps.Length;
                accumulator = 0f;
                RefreshInfo();
            }

            if (keyboard.tKey.wasPressedThisFrame)
            {
                pilot.Deterministic = !pilot.Deterministic;
                RefreshInfo();
            }
        }

        private void PollBrain()
        {
            nextPoll = Time.unscaledTime + BrainPollSeconds;
            string path = !string.IsNullOrWhiteSpace(brainFile)
                ? brainFile
                : BrainLocator.FindNewestBrain(runsDirectory, behaviorName);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                if (pilot.Brain == null)
                {
                    brainStatus = runsDirectory == null && string.IsNullOrWhiteSpace(brainFile)
                        ? "Trainer/runs folder not found (pass -runs <folder>)."
                        : "Waiting for the AI to save its first brain...";
                }

                RefreshInfo();
                return;
            }

            DateTime written = File.GetLastWriteTimeUtc(path);
            if (path == loadedPath && written == loadedWriteTime)
            {
                return;
            }

            try
            {
                PolicyBrain brain = PolicyBrain.Load(File.ReadAllBytes(path));
                string problem = BrainPilot.Validate(brain);
                if (problem == null && brain.SelfTestError() > SelfTestTolerance)
                {
                    problem = "Brain failed its self-test.";
                }

                if (problem != null)
                {
                    brainStatus = problem;
                    loadedPath = path;
                    loadedWriteTime = written;
                    Debug.LogWarning("Rejected brain " + path + ": " + problem);
                }
                else
                {
                    pilot.SetBrain(brain);
                    loadedPath = path;
                    loadedWriteTime = written;
                    loadedAt = DateTime.Now;
                    brainStatus = null;
                    Debug.Log("Loaded brain " + path + " (step " + brain.Step + ").");
                }
            }
            catch (IOException)
            {
                // The exporter may be replacing the file right now; try again on the next poll.
            }
            catch (UnauthorizedAccessException)
            {
            }

            RefreshInfo();
        }

        private void RecordResultOnce()
        {
            if (resultRecorded)
            {
                return;
            }

            resultRecorded = true;
            recent.Enqueue(new EpisodeResult(stats.Kills, stats.TimeSurvived, !sim.Hero.Alive));
            while (recent.Count > RecentEpisodeCount)
            {
                recent.Dequeue();
            }

            RefreshInfo();
        }

        private void RefreshInfo()
        {
            if (arenaHud == null)
            {
                return;
            }

            CultureInfo culture = CultureInfo.InvariantCulture;
            string text;
            PolicyBrain brain = pilot.Brain;
            if (brain == null)
            {
                text = "AI " + behaviorName.ToUpperInvariant() + "\n" + brainStatus;
            }
            else
            {
                text = "AI " + behaviorName.ToUpperInvariant() + "   run " + BrainLocator.RunName(loadedPath) +
                    "\nTrained " + brain.Step.ToString("N0", culture) + " steps" +
                    "   (loaded " + loadedAt.ToString("HH:mm", culture) + ")" +
                    "\nUpdates itself when training saves a new brain";
                if (!string.IsNullOrEmpty(brainStatus))
                {
                    text += "\n" + brainStatus;
                }
            }

            text += "\nRound " + episode + "   Zombies " + zombieCount +
                "   Speed x" + SpeedSteps[speedIndex] +
                "   Choice " + (pilot.Deterministic ? "best" : "sampled");
            if (recent.Count > 0)
            {
                float kills = 0f;
                float survived = 0f;
                int deaths = 0;
                foreach (EpisodeResult result in recent)
                {
                    kills += result.Kills;
                    survived += result.Survived;
                    deaths += result.Died ? 1 : 0;
                }

                text += "\nLast " + recent.Count + " rounds: " +
                    (kills / recent.Count).ToString("0.0", culture) + " kills, " +
                    (survived / recent.Count).ToString("0", culture) + "s alive, " +
                    deaths + " deaths";
            }

            arenaHud.SetInfoText(text);
        }

        private void CreateSimulation()
        {
            ResolveReferences();
            ArenaConfig config = new ArenaConfig
            {
                Width = arenaSize,
                Height = arenaSize,
                ZombieCount = zombieCount,
                ZombieSpawns = new[] { new ZombieSpawnEntry(DefaultDefs.Walker()) },
                EpisodeSeconds = episodeSeconds,
                RespawnKilledZombies = true,
                Seed = seed
            };
            sim = new ArenaSim(DefaultDefs.Warrior(), config);
            stats.Reset();
            pilot.ResetEpisode();
            accumulator = 0f;
            doneTimer = 0f;
            paused = false;
            resultRecorded = false;
            episode++;
            arenaRenderer.Bind(sim);
            arenaHud.Bind(sim, stats);
            arenaHud.SetPaused(false);
            topDownCamera.Bind(sim);
            RefreshInfo();
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
                topDownCamera = UnityEngine.Object.FindFirstObjectByType<TopDownCamera>();
            }

            if (arenaRenderer == null || arenaHud == null || topDownCamera == null)
            {
                enabled = false;
                throw new MissingReferenceException("AiArenaController requires ArenaRenderer, ArenaHud, and TopDownCamera.");
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

        private static string CommandLineValue(string key)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }

            return null;
        }

        private readonly struct EpisodeResult
        {
            public readonly int Kills;
            public readonly float Survived;
            public readonly bool Died;

            public EpisodeResult(int kills, float survived, bool died)
            {
                Kills = kills;
                Survived = survived;
                Died = died;
            }
        }
    }
}
