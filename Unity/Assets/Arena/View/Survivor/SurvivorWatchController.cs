using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using PersonalArena.Core;
using PersonalArena.Core.Survivor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PersonalArena.View
{
    /// <summary>
    /// "Watch AI" mode of the survivor game: the newest Warrior brain plays 15-minute graveyard runs,
    /// new brains exported by training are hot-loaded, and each level-up shows which item the AI picked.
    /// All rules run in <see cref="SurvivorSim"/>; this class only steps it and drives the views.
    /// </summary>
    public sealed class SurvivorWatchController : MonoBehaviour
    {
        public const string BehaviorName = "Warrior";
        private const int MaximumTicksPerFrame = 48;
        private const float BrainPollSeconds = 2f;
        private const float EndScreenSeconds = 5f;
        private const float SelfTestTolerance = 1e-3f;
        private const int RecentRunCount = 10;
        private const float TrainingPollSeconds = 1f;
        private const float StopFeedbackSeconds = 10f;
        private const string PowerPreference = "TrainingPower";
        private const int DefaultPowerIndex = 2;
        private static readonly int[] SpeedSteps = { 1, 2, 4, 8 };
        private static readonly Color TrainColor = new Color(0.2f, 0.6f, 0.32f, 1f);
        private static readonly Color StopColor = new Color(0.72f, 0.2f, 0.18f, 1f);

        /// <summary>Training power presets (same as the arena viewer, see docs/TRAINING.md).</summary>
        private static readonly TrainingPower[] Powers =
        {
            new TrainingPower("LIGHT", 2, 16, 20f),
            new TrainingPower("NORMAL", 4, 16, 20f),
            new TrainingPower("FAST", 4, 32, 20f),
            new TrainingPower("MAX", 4, 64, 20f)
        };

        [SerializeField] private int seed = 1;
        [Tooltip("Optional fixed brain file; otherwise the newest Trainer/runs/*/Warrior/latest.brain (schema 4) is used.")]
        [SerializeField] private string brainFile = string.Empty;

        [Header("Scene References")]
        [SerializeField] private SurvivorRenderer survivorRenderer;
        [SerializeField] private SurvivorHud hud;
        [SerializeField] private SurvivorCamera followCamera;

        private readonly SurvivorPilot pilot = new SurvivorPilot();
        private readonly PickHighlight highlight = new PickHighlight();
        private readonly Queue<RunResult> recent = new Queue<RunResult>();
        private readonly int[] offerItems = new int[PickHighlight.MaximumOffers];
        private readonly int[] offerLevels = new int[PickHighlight.MaximumOffers];
        private SurvivorSim sim;
        private string runsDirectory;
        private string loadedPath;
        private DateTime loadedWriteTime;
        private DateTime loadedAt;
        private string brainStatus = "Đang chờ AI lưu bộ não đầu tiên...";
        private float nextPoll;
        private float accumulator;
        private float endTimer;
        private int speedIndex;
        private int run;
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

        // Screenshot mode (-screenshot <png> [-quitAfterScreenshot]) used to check the build.
        private string screenshotPath;
        private bool quitAfterScreenshot;
        private bool highlightShotTaken;
        private bool hordeShotTaken;
        private float quitAt = float.PositiveInfinity;

        public SurvivorSim Sim => sim;
        public SurvivorPilot Pilot => pilot;

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
            if (int.TryParse(CommandLineValue("-seed"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int requestedSeed))
            {
                seed = requestedSeed;
            }
            if (int.TryParse(CommandLineValue("-speed"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int requestedSpeed))
            {
                int index = Array.IndexOf(SpeedSteps, requestedSpeed);
                speedIndex = index >= 0 ? index : speedIndex;
            }
            screenshotPath = CommandLineValue("-screenshot");
            quitAfterScreenshot = HasArgument("-quitAfterScreenshot");

            hud.SetHelpText(
                "AI tự chơi - bạn chỉ cần xem\n" +
                "Space tốc độ xem   T kiểu chọn   Esc tạm dừng\n" +
                "R trận mới   G dữ liệu huấn luyện\n" +
                "Lăn chuột: phóng to / thu nhỏ");

            if (!string.IsNullOrWhiteSpace(runsDirectory) && string.IsNullOrWhiteSpace(brainFile))
            {
                training = new TrainingServiceClient(runsDirectory);
                powerIndex = Mathf.Clamp(PlayerPrefs.GetInt(PowerPreference, DefaultPowerIndex), 0, Powers.Length - 1);
                hud.TrainingButtonClicked += OnTrainingButton;
                hud.TrainingPowerClicked += OnPowerButton;
                hud.ShowTrainingPanel(true);
                historyPanel = hud.HistoryPanel;
                historyPanel?.SetRunDirectory(BrainLocator.FindNewestRunDirectory(runsDirectory, BehaviorName));
                PollTraining();
            }

            sim = new SurvivorSim(new SurvivorConfig(), seed);
            followCamera.SetTarget(survivorRenderer);
            survivorRenderer.SetCamera(followCamera.ViewCamera);
            survivorRenderer.Bind(sim);
            StartRun();
            PollBrain();
        }

        private void OnDestroy()
        {
            if (hud != null)
            {
                hud.TrainingButtonClicked -= OnTrainingButton;
                hud.TrainingPowerClicked -= OnPowerButton;
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

            float realDelta = Time.unscaledDeltaTime;
            UpdateScreenshots();

            if (sim.IsEnded)
            {
                RecordResultOnce();
                survivorRenderer.SetInterpolationAlpha(1f);
                if (!paused)
                {
                    endTimer += realDelta;
                }
                hud.SetEndCountdown(EndScreenSeconds - endTimer);
                if (endTimer >= EndScreenSeconds)
                {
                    seed++;
                    sim.Reset(seed);
                    survivorRenderer.ResetRun();
                    StartRun();
                }
                return;
            }

            if (paused || pilot.Brain == null)
            {
                survivorRenderer.SetInterpolationAlpha(1f);
                return;
            }

            int speed = SpeedSteps[speedIndex];
            if (highlight.BlocksSim)
            {
                // The level-up cards run in real time, a little faster at high view speeds.
                highlight.Tick(realDelta * Mathf.Max(1f, speed * 0.5f));
                if (highlight.ReadyToPick)
                {
                    ResolvePick();
                }
                accumulator = 0f;
                survivorRenderer.SetInterpolationAlpha(1f);
                if (highlight.BlocksSim)
                {
                    return;
                }
            }

            if (sim.IsAwaitingPick)
            {
                BeginOffer();
                accumulator = 0f;
                survivorRenderer.SetInterpolationAlpha(1f);
                return;
            }

            float step = SurvivorSim.FixedDeltaTime;
            accumulator += Mathf.Min(realDelta * speed, step * MaximumTicksPerFrame);
            int ticks = 0;
            while (accumulator >= step && ticks < MaximumTicksPerFrame)
            {
                sim.Step(pilot.NextInput(sim));
                survivorRenderer.SyncAfterStep();
                accumulator -= step;
                ticks++;
                if (sim.IsAwaitingPick || sim.IsEnded)
                {
                    accumulator = 0f;
                    break;
                }
            }

            if (ticks == MaximumTicksPerFrame && accumulator >= step)
            {
                accumulator %= step;
            }

            survivorRenderer.SetInterpolationAlpha(accumulator / step);
        }

        /// <summary>Freezes the run and shows the offered cards; the AI picks once they have been visible for a moment.</summary>
        private void BeginOffer()
        {
            int count = Mathf.Min(sim.OfferCount, PickHighlight.MaximumOffers);
            for (int i = 0; i < PickHighlight.MaximumOffers; i++)
            {
                if (i < count)
                {
                    (int catalogIndex, int nextLevel) = sim.GetOffer(i);
                    offerItems[i] = catalogIndex;
                    offerLevels[i] = nextLevel;
                }
                else
                {
                    offerItems[i] = -1;
                    offerLevels[i] = 0;
                }
            }
            highlight.ShowOffer(count, offerItems, offerLevels);
        }

        /// <summary>Asks the brain for its pick, applies it with one paused tick, and highlights the chosen card.</summary>
        private void ResolvePick()
        {
            if (!sim.IsAwaitingPick)
            {
                highlight.Clear();
                return;
            }

            SurvivorInput input = pilot.NextInput(sim);
            int pick = input.Pick;
            if (pick < 1 || pick > sim.OfferCount)
            {
                // The action mask should prevent this; never leave the run stuck on the offer.
                pick = 1;
            }
            sim.Step(new SurvivorInput(input.Move, input.Skill, pick));
            survivorRenderer.SyncAfterStep();
            if (!highlight.Choose(pick - 1))
            {
                highlight.Clear();
            }
        }

        private void StartRun()
        {
            pilot.ResetEpisode();
            highlight.Clear();
            accumulator = 0f;
            endTimer = 0f;
            resultRecorded = false;
            run++;
            hud.Bind(sim, highlight);
            hud.SetPaused(paused);
            RefreshInfo();
        }

        private void RestartNow()
        {
            seed++;
            sim.Reset(seed);
            survivorRenderer.ResetRun();
            StartRun();
        }

        private void HandleKeys()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.rKey.wasPressedThisFrame && sim != null)
            {
                RestartNow();
                return;
            }

            // Esc first closes the training data screen; only a second press pauses.
            bool historyHandlesEscape = historyPanel != null && (historyPanel.IsOpen || historyPanel.ConsumedEscapeThisFrame);
            if (keyboard.escapeKey.wasPressedThisFrame && !historyHandlesEscape)
            {
                paused = !paused;
                accumulator = 0f;
                hud.SetPaused(paused);
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

        private void PollBrain()
        {
            nextPoll = Time.unscaledTime + BrainPollSeconds;
            string path = !string.IsNullOrWhiteSpace(brainFile)
                ? brainFile
                : BrainLocator.FindNewestBrain(runsDirectory, BehaviorName);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                if (pilot.Brain == null)
                {
                    if (runsDirectory == null && string.IsNullOrWhiteSpace(brainFile))
                    {
                        brainStatus = "Không tìm thấy thư mục Trainer/runs.";
                    }
                    else if (string.IsNullOrWhiteSpace(brainFile) &&
                        BrainLocator.FindNewestBrain(runsDirectory, BehaviorName, false) != null)
                    {
                        brainStatus = "Luật chơi đã đổi sang chế độ Sinh tồn.\nBộ não cũ không biết luật mới nên chiến binh đứng chờ.\n" +
                            "Bấm HUẤN LUYỆN AI để dạy nó luật mới.";
                    }
                    else if (training != null)
                    {
                        brainStatus = "Chiến binh chưa có bộ não nên đứng chờ.\nBấm HUẤN LUYỆN AI để bắt đầu dạy nó.";
                    }
                    else
                    {
                        brainStatus = "Đang chờ AI lưu bộ não đầu tiên...";
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
                string problem = SurvivorPilot.Validate(brain);
                if (problem == null && brain.SelfTestError() > SelfTestTolerance)
                {
                    problem = "Bộ não không qua được bài tự kiểm tra.";
                }

                loadedPath = path;
                loadedWriteTime = written;
                if (problem != null)
                {
                    brainStatus = "Bộ não bị từ chối: " + problem;
                    Debug.LogWarning("Rejected brain " + path + ": " + problem);
                }
                else
                {
                    bool deterministic = pilot.Deterministic;
                    pilot.SetBrain(brain);
                    pilot.Deterministic = deterministic;
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
            catch (Exception exception) when (exception is InvalidDataException || exception is ArgumentException || exception is FormatException)
            {
                loadedPath = path;
                loadedWriteTime = written;
                brainStatus = "Không đọc được bộ não: " + exception.Message;
                Debug.LogWarning("Could not load brain " + path + ": " + exception.Message);
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
            trainingNotice = training.Start(System.Diagnostics.Process.GetCurrentProcess().Id, Powers[powerIndex], BehaviorName);
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
            return "Sức mạnh: " + power.Name + " - " + power.Fighters + " đấu trường cùng lúc     đổi >";
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
                ? "\nLần trước " + status.run_id + ": " + status.step.ToString("N0", culture) + " bước"
                : string.Empty;
            string text;
            switch (trainingSnapshot.State)
            {
                case TrainingState.Unavailable:
                    hud.SetTrainingButton("HUẤN LUYỆN AI", false, TrainColor);
                    text = "Máy này chưa huấn luyện được:\n" + training.MissingPiece();
                    break;
                case TrainingState.Starting:
                    hud.SetTrainingButton(stopAsked ? "ĐANG DỪNG..." : "DỪNG HUẤN LUYỆN", !stopAsked, StopColor);
                    text = status != null && status.message != null && status.message.StartsWith("The trainer crashed", StringComparison.Ordinal)
                        ? "Trình huấn luyện bị lỗi và sẽ tự chạy lại.\nTiến độ vẫn được giữ."
                        : "Đang khởi động... nạp các đấu trường huấn luyện\n(khoảng một phút). Chiến binh ở đây sẽ\ntự cập nhật khi AI học tiến bộ.";
                    break;
                case TrainingState.Training:
                    hud.SetTrainingButton(stopAsked ? "ĐANG DỪNG..." : "DỪNG HUẤN LUYỆN", !stopAsked, StopColor);
                    text = "Đang huấn luyện " + status.run_id +
                        "\nBước " + status.step.ToString("N0", culture) +
                        (status.has_reward ? "    Điểm thưởng TB " + status.mean_reward.ToString("0.0", culture) : string.Empty) +
                        (status.num_envs > 0
                            ? "\n" + (status.num_envs * status.arena_agents) + " đấu trường học cùng lúc (" +
                              status.num_envs + " game x " + status.arena_agents + ")" + (status.cpu ? " trên CPU" : string.Empty)
                            : string.Empty) +
                        "\nPhiên này " + FormatDuration(status.session_seconds);
                    break;
                case TrainingState.Stopping:
                    hud.SetTrainingButton("ĐANG LƯU...", false, StopColor);
                    text = "Đang lưu tiến độ của AI, vui lòng chờ...";
                    break;
                case TrainingState.External:
                    hud.SetTrainingButton("ĐANG HUẤN LUYỆN (NGOÀI)", false, StopColor);
                    text = "Huấn luyện được chạy từ bên ngoài game.\nChiến binh ở đây vẫn tự cập nhật\nmỗi khi có bộ não mới.";
                    break;
                case TrainingState.Stopped:
                    hud.SetTrainingButton("HUẤN LUYỆN AI", true, TrainColor);
                    text = "Đã dừng và lưu tiến độ.\nBấm để học tiếp." + lastRun;
                    break;
                case TrainingState.Error:
                    hud.SetTrainingButton("HUẤN LUYỆN AI", true, TrainColor);
                    text = "Có lỗi: " + (status != null ? status.message : string.Empty) + "\nBấm để thử lại.";
                    break;
                default:
                    hud.SetTrainingButton("HUẤN LUYỆN AI", true, TrainColor);
                    text = "Bấm để AI tiếp tục học ở chế độ nền\ntrong lúc bạn xem nó chơi." + lastRun;
                    break;
            }

            if (restartWithNewPower)
            {
                text = "Đổi sức mạnh sang " + Powers[powerIndex].Name + ":\nđang lưu tiến độ rồi chạy lại...";
            }
            if (!string.IsNullOrEmpty(trainingNotice))
            {
                text = trainingNotice;
            }

            hud.SetTrainingPower(PowerLabel(Powers[powerIndex]), trainingSnapshot.State != TrainingState.External &&
                trainingSnapshot.State != TrainingState.Unavailable && trainingSnapshot.State != TrainingState.Stopping);
            hud.SetTrainingText(text);
            float[] rewards = status != null ? status.rewards : null;
            string caption = rewards != null && rewards.Length > 0 && status.steps != null && status.steps.Length > 0
                ? "Điểm thưởng TB, bước 0 - " + FormatSteps(status.steps[status.steps.Length - 1]) + " (cao hơn = giỏi hơn)"
                : "Biểu đồ điểm thưởng hiện khi bắt đầu huấn luyện";
            hud.SetTrainingGraph(rewards, caption);
        }

        private static string FormatDuration(float seconds)
        {
            int whole = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return whole >= 3600
                ? (whole / 3600) + " giờ " + (whole % 3600 / 60).ToString("00") + " phút"
                : (whole / 60) + " phút " + (whole % 60).ToString("00") + " giây";
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
            recent.Enqueue(new RunResult(sim.Time, sim.Level, sim.Gold));
            while (recent.Count > RecentRunCount)
            {
                recent.Dequeue();
            }

            RefreshInfo();
        }

        private void RefreshInfo()
        {
            if (hud == null)
            {
                return;
            }

            CultureInfo culture = CultureInfo.InvariantCulture;
            string text;
            PolicyBrain brain = pilot.Brain;
            if (brain == null)
            {
                text = "AI CHIẾN BINH\nChưa có bộ não";
            }
            else
            {
                text = "AI CHIẾN BINH   " + BrainLocator.RunName(loadedPath) +
                    "\nĐã học " + brain.Step.ToString("N0", culture) + " bước" +
                    "   (nạp lúc " + loadedAt.ToString("HH:mm", culture) + ")" +
                    "\nTự cập nhật khi huấn luyện lưu não mới";
                if (!string.IsNullOrEmpty(brainStatus))
                {
                    text += "\n" + brainStatus;
                }
            }

            text += "\nTrận " + run + "   Tốc độ xem x" + SpeedSteps[speedIndex] +
                "   Chọn: " + (pilot.Deterministic ? "tốt nhất" : "ngẫu nhiên");
            if (recent.Count > 0)
            {
                float time = 0f;
                float level = 0f;
                float gold = 0f;
                foreach (RunResult result in recent)
                {
                    time += result.Seconds;
                    level += result.Level;
                    gold += result.Gold;
                }

                text += "\n" + recent.Count + " trận gần nhất (TB): sống " + SurvivorViewLogic.FormatClock(time / recent.Count) +
                    ", cấp " + (level / recent.Count).ToString("0.0", culture) +
                    ", vàng " + (gold / recent.Count).ToString("0", culture);
            }
            else
            {
                text += "\nTrung bình 10 trận gần nhất: chưa có trận nào xong";
            }

            hud.SetInfoText(text);
            hud.SetNotice(brain == null ? brainStatus : null);
        }

        private void UpdateScreenshots()
        {
            if (string.IsNullOrEmpty(screenshotPath))
            {
                return;
            }

            if (Time.unscaledTime >= quitAt)
            {
                quitAt = float.PositiveInfinity;
                Debug.Log("Screenshot mode finished.");
                Application.Quit();
                return;
            }

            float real = Time.realtimeSinceStartup;
            // The level-up highlight over a grown horde; after a while any highlight will do.
            if (!highlightShotTaken && highlight.Current == PickHighlight.Phase.Highlight && highlight.Progress > 0.3f &&
                (sim.Time >= 45f || real > 70f))
            {
                highlightShotTaken = true;
                Capture(screenshotPath);
            }
            else if (!hordeShotTaken && !highlight.BlocksSim && !sim.IsEnded &&
                (sim.AliveEnemyCount >= 55 || sim.Time >= 120f || real > 100f))
            {
                hordeShotTaken = true;
                Capture(SiblingPath(screenshotPath, "_horde"));
            }
            else if (!highlightShotTaken && real > 130f)
            {
                highlightShotTaken = true;
                Capture(screenshotPath);
            }

            if (highlightShotTaken && hordeShotTaken && quitAfterScreenshot && float.IsPositiveInfinity(quitAt))
            {
                quitAt = Time.unscaledTime + 1.5f;
            }
        }

        private void Capture(string path)
        {
            string directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("Screenshot " + path + " at run time " + SurvivorViewLogic.FormatClock(sim.Time) + ", level " + sim.Level +
                ", enemies " + sim.AliveEnemyCount + ", phase " + highlight.Current + ".");
        }

        private static string SiblingPath(string path, string suffix)
        {
            string extension = Path.GetExtension(path);
            return Path.Combine(Path.GetDirectoryName(path) ?? string.Empty, Path.GetFileNameWithoutExtension(path) + suffix + extension);
        }

        private void ResolveReferences()
        {
            if (survivorRenderer == null)
            {
                survivorRenderer = GetComponent<SurvivorRenderer>();
            }
            if (hud == null)
            {
                hud = GetComponent<SurvivorHud>();
            }
            if (followCamera == null)
            {
                followCamera = FindFirstObjectByType<SurvivorCamera>();
            }

            if (survivorRenderer == null || hud == null || followCamera == null)
            {
                enabled = false;
                throw new MissingReferenceException("SurvivorWatchController requires SurvivorRenderer, SurvivorHud, and SurvivorCamera.");
            }
        }

        private static bool HasArgument(string key)
        {
            foreach (string argument in Environment.GetCommandLineArgs())
            {
                if (string.Equals(argument, key, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
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

        private readonly struct RunResult
        {
            public readonly float Seconds;
            public readonly int Level;
            public readonly float Gold;

            public RunResult(float seconds, int level, float gold)
            {
                Seconds = seconds;
                Level = level;
                Gold = gold;
            }
        }
    }
}
