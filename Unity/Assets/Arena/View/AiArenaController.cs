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
        private const float TrainingPollSeconds = 1f;
        private const float StopFeedbackSeconds = 10f;
        private static readonly int[] SpeedSteps = { 1, 2, 4 };
        private static readonly Color TrainColor = new Color(0.2f, 0.6f, 0.32f, 1f);
        private static readonly Color StopColor = new Color(0.72f, 0.2f, 0.18f, 1f);
        private const string PowerPreference = "TrainingPower";
        private const int DefaultPowerIndex = 2;
        /// <summary>
        /// Training power presets, measured on the owner's PC (see docs/TRAINING.md): more arenas per
        /// game beat more games, and a faster time scale did not help reliably.
        /// </summary>
        private static readonly TrainingPower[] Powers =
        {
            new TrainingPower("LIGHT", 2, 16, 20f),
            new TrainingPower("NORMAL", 4, 16, 20f),
            new TrainingPower("FAST", 4, 32, 20f),
            new TrainingPower("MAX", 4, 64, 20f)
        };

        [Header("Arena")]
        [SerializeField, Range(8f, 80f)] private float arenaSize = ArenaConfig.DefaultSize;
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
        private TrainingServiceClient training;
        private TrainingSnapshot trainingSnapshot;
        private float nextTrainingPoll;
        private float stopRequestedAt = float.NegativeInfinity;
        private bool startedTraining;
        private string trainingNotice;
        private int powerIndex = DefaultPowerIndex;
        private bool restartWithNewPower;
        private TrainingHistoryPanel historyPanel;
        private TopDownCamera.ViewMode shownCameraMode;

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

            RefreshHelp();
            arenaHud.SetResultFooter("Next round starts automatically");
            if (!string.IsNullOrWhiteSpace(runsDirectory) && string.IsNullOrWhiteSpace(brainFile))
            {
                training = new TrainingServiceClient(runsDirectory);
                powerIndex = Mathf.Clamp(PlayerPrefs.GetInt(PowerPreference, DefaultPowerIndex), 0, Powers.Length - 1);
                arenaHud.TrainingButtonClicked += OnTrainingButton;
                arenaHud.TrainingPowerClicked += OnPowerButton;
                arenaHud.ShowTrainingPanel(true);
                historyPanel = arenaHud.HistoryPanel;
                historyPanel?.SetRunDirectory(BrainLocator.FindNewestRunDirectory(runsDirectory, behaviorName));
                PollTraining();
            }

            PollBrain();
            CreateSimulation();
        }

        private void OnDestroy()
        {
            if (arenaHud != null)
            {
                arenaHud.TrainingButtonClicked -= OnTrainingButton;
                arenaHud.TrainingPowerClicked -= OnPowerButton;
            }
        }

        private void OnApplicationQuit()
        {
            // The service also stops when this process exits; asking first saves a few seconds.
            if (training != null && startedTraining && trainingSnapshot.IsActive)
            {
                training.RequestStop();
            }
        }

        private void Update()
        {
            HandleKeys();
            if (topDownCamera.Mode != shownCameraMode)
            {
                RefreshHelp();
            }
            if (Time.unscaledTime >= nextPoll)
            {
                PollBrain();
            }
            if (training != null && Time.unscaledTime >= nextTrainingPoll)
            {
                PollTraining();
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

            // Esc first closes the training data screen; only a second press pauses.
            bool historyHandlesEscape = historyPanel != null && (historyPanel.IsOpen || historyPanel.ConsumedEscapeThisFrame);
            if (keyboard.escapeKey.wasPressedThisFrame && !historyHandlesEscape)
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

            if (keyboard.gKey.wasPressedThisFrame && historyPanel != null)
            {
                historyPanel.Toggle();
            }
        }

        private void RefreshHelp()
        {
            shownCameraMode = topDownCamera.Mode;
            arenaHud.SetHelpText(
                "AI is playing by itself\n" +
                "1-6 zombies: 1/2/4/8/16/32   R new round\n" +
                "Space view speed   T choice mode   Esc pause\n" +
                "G training data (learning graphs)\n\n" +
                topDownCamera.ModeLabel.ToUpperInvariant() + "\n" +
                TopDownCamera.ControlsHint);
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
                    if (runsDirectory == null && string.IsNullOrWhiteSpace(brainFile))
                    {
                        brainStatus = "Trainer/runs folder not found (pass -runs <folder>).";
                    }
                    else if (string.IsNullOrWhiteSpace(brainFile) &&
                        BrainLocator.FindNewestBrain(runsDirectory, behaviorName, false) != null)
                    {
                        brainStatus = "The arena changed: a round platform over\na deadly abyss. The old brain only knows\n" +
                            "the square arena, so the warrior waits.\nPress TRAIN THE AI to teach it the new rules.";
                    }
                    else
                    {
                        brainStatus = "Waiting for the AI to save its first brain...";
                    }
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
                    if (training == null || !trainingSnapshot.IsActive)
                    {
                        historyPanel?.SetRunDirectory(BrainLocator.RunDirectory(path));
                    }
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

        private void OnTrainingButton()
        {
            if (training == null)
            {
                return;
            }

            restartWithNewPower = false;
            if (trainingSnapshot.IsActive)
            {
                training.RequestStop();
                stopRequestedAt = Time.unscaledTime;
                trainingNotice = null;
            }
            else if (trainingSnapshot.State != TrainingState.External && trainingSnapshot.State != TrainingState.Unavailable)
            {
                StartTraining();
            }

            PollTraining();
        }

        private void StartTraining()
        {
            trainingNotice = training.Start(System.Diagnostics.Process.GetCurrentProcess().Id, Powers[powerIndex]);
            startedTraining = trainingNotice == null;
            stopRequestedAt = float.NegativeInfinity;
        }

        private void OnPowerButton()
        {
            if (training == null)
            {
                return;
            }

            powerIndex = (powerIndex + 1) % Powers.Length;
            PlayerPrefs.SetInt(PowerPreference, powerIndex);
            PlayerPrefs.Save();
            // Running training picks up the new power after a save-and-restart.
            if (trainingSnapshot.IsActive && trainingSnapshot.State != TrainingState.Stopping)
            {
                restartWithNewPower = true;
                training.RequestStop();
                stopRequestedAt = Time.unscaledTime;
            }

            PollTraining();
        }

        private static string PowerLabel(TrainingPower power)
        {
            return "Power: " + power.Name + " - " + power.Fighters + " arenas at once     change >";
        }

        private void PollTraining()
        {
            nextTrainingPoll = Time.unscaledTime + TrainingPollSeconds;
            trainingSnapshot = training.Read();
            if (restartWithNewPower && !trainingSnapshot.IsActive)
            {
                restartWithNewPower = false;
                if (trainingSnapshot.State == TrainingState.Stopped || trainingSnapshot.State == TrainingState.Idle)
                {
                    StartTraining();
                    trainingSnapshot = training.Read();
                }
            }

            TrainingStatus status = trainingSnapshot.Status;
            if (historyPanel != null && trainingSnapshot.IsActive && status != null && !string.IsNullOrEmpty(status.run_id))
            {
                string activeRun = Path.Combine(runsDirectory, status.run_id);
                if (Directory.Exists(activeRun))
                {
                    historyPanel.SetRunDirectory(activeRun);
                }
            }

            CultureInfo culture = CultureInfo.InvariantCulture;
            bool stopAsked = Time.unscaledTime - stopRequestedAt < StopFeedbackSeconds;
            string lastRun = status != null && status.step > 0
                ? "\nLast run " + status.run_id + ": " + status.step.ToString("N0", culture) + " steps"
                : string.Empty;
            string text;
            switch (trainingSnapshot.State)
            {
                case TrainingState.Unavailable:
                    arenaHud.SetTrainingButton("TRAIN THE AI", false, TrainColor);
                    text = "Training is not available on this PC:\n" + training.MissingPiece();
                    break;
                case TrainingState.Starting:
                    arenaHud.SetTrainingButton(stopAsked ? "STOPPING..." : "STOP TRAINING", !stopAsked, StopColor);
                    text = status != null && status.message != null && status.message.StartsWith("The trainer crashed", StringComparison.Ordinal)
                        ? status.message + "\nProgress is kept; it resumes by itself."
                        : "Starting... loading the training arenas\n(about a minute). The fighter here keeps\nupdating as the AI learns.";
                    break;
                case TrainingState.Training:
                    arenaHud.SetTrainingButton(stopAsked ? "STOPPING..." : "STOP TRAINING", !stopAsked, StopColor);
                    text = "Training run " + status.run_id +
                        "\nStep " + status.step.ToString("N0", culture) +
                        (status.has_reward ? "    Mean reward " + status.mean_reward.ToString("0.0", culture) : string.Empty) +
                        (status.zombies > 0f ? "\nTraining arenas: " + status.zombies.ToString("0", culture) + " zombies" : string.Empty) +
                        (status.num_envs > 0
                            ? "\n" + (status.num_envs * status.arena_agents) + " arenas learning at once (" +
                              status.num_envs + " games x " + status.arena_agents + ")" + (status.cpu ? " on the CPU" : string.Empty)
                            : string.Empty) +
                        "\nThis session " + FormatDuration(status.session_seconds);
                    break;
                case TrainingState.Stopping:
                    arenaHud.SetTrainingButton("SAVING...", false, StopColor);
                    text = "Saving the AI's progress, please wait...";
                    break;
                case TrainingState.External:
                    arenaHud.SetTrainingButton("TRAINING (OUTSIDE)", false, StopColor);
                    text = "Training was started outside the game.\nThe fighter here still updates with\neach new brain.";
                    break;
                case TrainingState.Stopped:
                    arenaHud.SetTrainingButton("TRAIN THE AI", true, TrainColor);
                    text = status.message + "\nPress to continue training.";
                    break;
                case TrainingState.Error:
                    arenaHud.SetTrainingButton("TRAIN THE AI", true, TrainColor);
                    text = "Problem: " + status.message + "\nPress to try again.";
                    break;
                default:
                    arenaHud.SetTrainingButton("TRAIN THE AI", true, TrainColor);
                    text = "Press to let the AI keep learning in the\nbackground while you watch it play." + lastRun;
                    break;
            }

            if (restartWithNewPower)
            {
                text = "Changing power to " + Powers[powerIndex].Name + ":\nsaving progress, then restarting...";
            }
            if (!string.IsNullOrEmpty(trainingNotice))
            {
                text = trainingNotice;
            }

            arenaHud.SetTrainingPower(PowerLabel(Powers[powerIndex]), trainingSnapshot.State != TrainingState.External &&
                trainingSnapshot.State != TrainingState.Unavailable && trainingSnapshot.State != TrainingState.Stopping);
            arenaHud.SetTrainingText(text);
            float[] rewards = status != null ? status.rewards : null;
            string caption = rewards != null && rewards.Length > 0 && status.steps != null && status.steps.Length > 0
                ? "Mean reward, step 0 - " + FormatSteps(status.steps[status.steps.Length - 1]) + " (higher = smarter)"
                : "Mean reward graph appears once training reports";
            arenaHud.SetTrainingGraph(rewards, caption);
        }

        private static string FormatDuration(float seconds)
        {
            int whole = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return whole >= 3600
                ? (whole / 3600) + "h " + (whole % 3600 / 60).ToString("00") + "m"
                : (whole / 60) + "m " + (whole % 60).ToString("00") + "s";
        }

        private static string FormatSteps(long steps)
        {
            CultureInfo culture = CultureInfo.InvariantCulture;
            if (steps >= 1000000)
            {
                return (steps / 1e6).ToString("0.0", culture) + "M";
            }
            return steps >= 1000 ? (steps / 1000).ToString(culture) + "k" : steps.ToString(culture);
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
                "   View speed x" + SpeedSteps[speedIndex] +
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
