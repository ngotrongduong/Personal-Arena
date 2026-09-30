using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using PersonalArena.Core;
using PersonalArena.Core.Meta;
using PersonalArena.Core.Survivor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PersonalArena.View
{
    /// <summary>
    /// "Watch AI" mode of the survivor game: the newest Warrior brain plays 15-minute graveyard runs,
    /// new brains exported by training are hot-loaded, and each level-up shows which item the AI picked.
    /// M5: every run uses the owner's build and tier (<see cref="ProfileRules.ToBuild"/>, applied at run start),
    /// a run the brain played pays gold into the saved profile, and the character (C), Auto Farm (F) and
    /// build comparison (V) panels edit the profile. All rules run in Core; this class only steps, saves and draws.
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
        private readonly Queue<RecentRun> recent = new Queue<RecentRun>();
        private readonly SpectatorLabeler labeler = new SpectatorLabeler();
        private readonly RunChronicleRecorder chronicle = new RunChronicleRecorder();
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
        private BehaviorProfilePanel profilePanel;

        // M5 economy: the saved profile, the build of the run on screen, and the meta panels.
        private const float ProfileNoticeSeconds = 8f;
        private const float FarmInfoRefreshSeconds = 1f;
        private ProfileStore store;
        private CharacterBuild runBuild;
        private int runLoadout;
        private string runLoadoutName;
        private bool brainPlayedRun;
        private byte[] loadedBrainBytes;
        private string loadedBrainName;
        private OwnerTraining startedOwner;
        private CharacterPanel characterPanel;
        private AutoFarmPanel farmPanel;
        private LoadoutComparePanel comparePanel;
        private BrainLineagePanel lineagePanel;
        private float nextFarmInfoRefresh;
        private float shortRunSeconds;

        // B switches between the newest brain from training and the trainer's best brain (champion).
        private const string WatchBestPreference = "WatchBestBrain";
        private const float SwitchNoticeSeconds = 4f;
        private bool watchBest;
        private bool loadedIsChampion;
        private ChampionRecord loadedChampion;
        private long loadedStep;
        private string switchNotice;
        private float switchNoticeUntil;

        // M6: a saved brain version picked in the lineage panel ("Xem ngay"); not saved across restarts. B clears it.
        private LineageVersion watchedVersion;
        private string watchedBranchName;
        private bool loadedIsVersion;
        private int brainLoadCount;

        // Screenshot mode (-screenshot <png> [-quitAfterScreenshot] [-showProfile] [-openPanel character|farm|compare|lineage]
        // [-farmRuns N] [-endShot] [-labelShot]) used to check the build. Pass -profile <scratch path> with it.
        private const float PanelOpenSeconds = 6f;
        private const float PanelShotSeconds = 8f;
        private const float FarmShotSeconds = 14f;
        private string screenshotPath;
        private bool quitAfterScreenshot;
        private bool showProfile;
        private string openPanel;
        private int farmRuns = 10;
        private bool endShot;
        private bool labelShot;
        private bool panelOpened;
        private bool highlightShotTaken;
        private bool hordeShotTaken;
        private bool endShotTaken;
        private bool labelShotTaken;
        private float labelSeenSince = float.PositiveInfinity;
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
            showProfile = HasArgument("-showProfile");
            openPanel = CommandLineValue("-openPanel");
            if (int.TryParse(CommandLineValue("-farmRuns"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int requestedFarmRuns))
            {
                farmRuns = Mathf.Clamp(requestedFarmRuns, 1, 100);
            }
            endShot = HasArgument("-endShot");
            labelShot = HasArgument("-labelShot");
            watchBest = HasArgument("-best") || PlayerPrefs.GetInt(WatchBestPreference, 0) == 1;

            // Profile: -profile <folder or file> for checks and screenshots, else the owner's real profile.
            string profileOverride = CommandLineValue("-profile");
            store = ProfileStore.Create(profileOverride, Application.persistentDataPath,
                Path.Combine(Application.temporaryCachePath, "profile-fallback"));
            ShowProfileLoadNotice(store.Load());

            // -runSeconds N (60..900) shortens runs so the end screen can be checked; only with a scratch -profile.
            if (!string.IsNullOrWhiteSpace(profileOverride) &&
                float.TryParse(CommandLineValue("-runSeconds"), NumberStyles.Float, CultureInfo.InvariantCulture, out float requestedRun))
            {
                shortRunSeconds = Mathf.Clamp(requestedRun, 60f, 900f);
            }

            hud.SetHelpText(
                "AI tự chơi - bạn chỉ cần xem\n" +
                "Space tốc độ xem   T kiểu chọn   Esc tạm dừng\n" +
                "R trận mới   G biểu đồ học   P hồ sơ AI\n" +
                "C nhân vật   F farm vàng   V so sánh build\n" +
                "B đổi não mới nhất / giỏi nhất   L lịch sử não\n" +
                "Lăn chuột: phóng to / thu nhỏ");

            characterPanel = hud.CharacterPanel;
            farmPanel = hud.FarmPanel;
            comparePanel = hud.ComparePanel;
            characterPanel.Bind(store, IsBuildChangePending);
            farmPanel.Bind(store, () => loadedBrainBytes, () => loadedBrainName);
            comparePanel.Bind(store);
            characterPanel.ProfileChanged += OnProfileChanged;
            farmPanel.ProfileChanged += OnProfileChanged;
            comparePanel.ProfileChanged += OnProfileChanged;

            // M6 brain lineage (L): needs the runs folder; with -brain it still opens but cannot switch brains.
            lineagePanel = hud.LineagePanel;
            if (lineagePanel != null && !string.IsNullOrWhiteSpace(runsDirectory) && Directory.Exists(runsDirectory))
            {
                lineagePanel.Bind(store, runsDirectory, BehaviorName, () => trainingSnapshot);
                lineagePanel.ProfileChanged += OnProfileChanged;
                lineagePanel.WatchVersionRequested += OnWatchVersionRequested;
            }

            if (!string.IsNullOrWhiteSpace(runsDirectory) && string.IsNullOrWhiteSpace(brainFile))
            {
                training = new TrainingServiceClient(runsDirectory);
                powerIndex = Mathf.Clamp(PlayerPrefs.GetInt(PowerPreference, DefaultPowerIndex), 0, Powers.Length - 1);
                hud.TrainingButtonClicked += OnTrainingButton;
                hud.TrainingPowerClicked += OnPowerButton;
                hud.ShowTrainingPanel(true);
                historyPanel = hud.HistoryPanel;
                historyPanel?.SetRunDirectory(BrainLocator.FindNewestRunDirectory(runsDirectory, BehaviorName));
                profilePanel = hud.ProfilePanel;
                profilePanel?.SetSource(runsDirectory, BehaviorName);
                if (showProfile)
                {
                    profilePanel?.SetOpen(true);
                }
                PollTraining();
            }

            SurvivorConfig config = new SurvivorConfig { Build = NextRunBuild() };
            if (shortRunSeconds > 0f)
            {
                config.RunSeconds = shortRunSeconds;
            }

            sim = new SurvivorSim(config, seed);
            followCamera.SetTarget(survivorRenderer);
            survivorRenderer.SetCamera(followCamera.ViewCamera);
            survivorRenderer.Bind(sim);
            hud.BindHeroLabel(survivorRenderer, followCamera.ViewCamera);
            StartRun();
            PollBrain();

            // -watchVersion <id> (checks the build): start on a saved lineage version, as "Xem ngay" does.
            string startVersion = CommandLineValue("-watchVersion");
            if (!string.IsNullOrWhiteSpace(startVersion) && !string.IsNullOrWhiteSpace(runsDirectory))
            {
                LineageIndex lineage = LineageStore.Load(runsDirectory, BehaviorName);
                LineageVersion version = lineage.FindVersion(startVersion.Trim());
                if (version != null)
                {
                    OnWatchVersionRequested(version, lineage.BranchName(version.RunId));
                }
            }
        }

        private void OnDestroy()
        {
            if (hud != null)
            {
                hud.TrainingButtonClicked -= OnTrainingButton;
                hud.TrainingPowerClicked -= OnPowerButton;
            }
            if (characterPanel != null)
            {
                characterPanel.ProfileChanged -= OnProfileChanged;
            }
            if (farmPanel != null)
            {
                farmPanel.ProfileChanged -= OnProfileChanged;
            }
            if (comparePanel != null)
            {
                comparePanel.ProfileChanged -= OnProfileChanged;
            }
            if (lineagePanel != null)
            {
                lineagePanel.ProfileChanged -= OnProfileChanged;
                lineagePanel.WatchVersionRequested -= OnWatchVersionRequested;
            }
        }

        private void OnApplicationQuit()
        {
            // Auto Farm: cancel, wait up to 2 s, book the finished matches (the panel also does this itself).
            farmPanel?.ShutDown();

            // The service also stops when this process exits; asking first saves a few seconds.
            if (training != null && startedTraining && trainingSnapshot.IsActive)
            {
                training.RequestStop();
            }
        }

        /// <summary>The build of the next run: the active loadout and selected tier of the profile, fixed until the run ends.</summary>
        private CharacterBuild NextRunBuild()
        {
            CharacterProfile warrior = store.Warrior;
            runBuild = ProfileRules.ToBuild(warrior, store.Profile.SelectedTier);
            runLoadout = warrior.ActiveLoadout;
            runLoadoutName = MetaViewLogic.LoadoutName(warrior, runLoadout);
            return runBuild.Clone();
        }

        /// <summary>True when the profile's build (loadout, points or tier) differs from the run on screen.</summary>
        private bool IsBuildChangePending()
        {
            if (store == null || runBuild == null)
            {
                return false;
            }

            CharacterProfile warrior = store.Warrior;
            return warrior.ActiveLoadout != runLoadout ||
                !MetaViewLogic.SameBuild(runBuild, ProfileRules.ToBuild(warrior, store.Profile.SelectedTier));
        }

        private void OnProfileChanged()
        {
            RefreshInfo();
            characterPanel?.Refresh();
            comparePanel?.Refresh();
            if (training != null)
            {
                PollTraining();
            }

            // M6: the newest brain follows the active branch (profile BrainRunId).
            if (watchedVersion == null && !loadedIsChampion && string.IsNullOrWhiteSpace(brainFile))
            {
                PollBrain();
            }
        }

        /// <summary>"Xem ngay" in the lineage panel: watch that saved version now (fresh run), until B.</summary>
        private void OnWatchVersionRequested(LineageVersion version, string branchName)
        {
            if (!string.IsNullOrWhiteSpace(brainFile))
            {
                ShowSwitchNotice("Đang xem một bộ não cố định (-brain), không đổi được.");
                return;
            }

            if (version == null || string.IsNullOrEmpty(version.BrainPath))
            {
                return;
            }

            watchedVersion = version;
            watchedBranchName = string.IsNullOrEmpty(branchName) ? LineageStore.DefaultBranchName(version.RunId) : branchName;
            int loadsBefore = brainLoadCount;
            loadedPath = null; // force a reload even when the same file was loaded before
            PollBrain();

            if (brainLoadCount == loadsBefore || !loadedIsVersion)
            {
                // Rejected or unreadable: back to the newest (or best) brain.
                string problem = brainStatus;
                watchedVersion = null;
                watchedBranchName = null;
                loadedPath = null;
                PollBrain();
                ShowSwitchNotice("Không xem được phiên bản " + version.Name + ".\n" +
                    (string.IsNullOrEmpty(problem) ? "Không đọc được tệp não." : problem));
                return;
            }

            ShowSwitchNotice("Đang xem phiên bản " + version.Name + " — bấm B để về não mới nhất");
            switchNoticeUntil = Time.unscaledTime + ProfileNoticeSeconds;
            if (sim != null && pilot.Brain != null)
            {
                RestartNow();
            }
        }

        /// <summary>Newest brain: the active branch's latest.brain when it exists with the current schema, else the newest run's.</summary>
        private string NewestBrainPath()
        {
            CharacterProfile warrior = store != null ? store.Warrior : null;
            string branchBrain = warrior != null
                ? LineageStore.BranchLatestBrain(runsDirectory, BehaviorName, warrior.BrainRunId)
                : null;
            return branchBrain ?? BrainLocator.FindNewestBrain(runsDirectory, BehaviorName);
        }

        private void ShowProfileLoadNotice(ProfileLoadOutcome outcome)
        {
            string text;
            switch (outcome)
            {
                case ProfileLoadOutcome.RecoveredFromBackup:
                    text = "Hồ sơ bị hỏng nên đã dùng bản sao lưu gần nhất.";
                    break;
                case ProfileLoadOutcome.RecoveredNew:
                    text = "Hồ sơ bị hỏng và không có bản sao lưu:\nđã tạo hồ sơ mới (bản hỏng vẫn được giữ lại).";
                    break;
                case ProfileLoadOutcome.ReadError:
                    text = "Không đọc được hồ sơ (tệp đang bị khóa?).\nVàng kiếm được lần này sẽ không được lưu.";
                    break;
                default:
                    return;
            }

            Debug.LogWarning("Profile " + store.ProfilePath + ": " + outcome);
            switchNotice = text;
            switchNoticeUntil = Time.unscaledTime + ProfileNoticeSeconds;
        }

        private void Update()
        {
            HandleKeys();
            if (switchNotice != null && Time.unscaledTime >= switchNoticeUntil)
            {
                switchNotice = null;
                RefreshInfo();
            }
            if (Time.unscaledTime >= nextPoll)
            {
                PollBrain();
            }
            if (training != null && Time.unscaledTime >= nextTrainingPoll)
            {
                PollTraining();
            }
            if (farmPanel != null && farmPanel.IsFarming && Time.unscaledTime >= nextFarmInfoRefresh)
            {
                // The HUD's farm progress line.
                RefreshInfo();
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
                    RestartNow();
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
                ObserveStep();
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

            if (sim.IsEnded)
            {
                // Book the run in the frame it ended so the end screen shows the reward and story at once.
                RecordResultOnce();
            }

            survivorRenderer.SetInterpolationAlpha(accumulator / step);
        }

        /// <summary>After every sim step (the pick step too): spectator label, chronicle, HUD tag.</summary>
        private void ObserveStep()
        {
            brainPlayedRun = true;
            labeler.Observe(sim, sim.LastStepSeconds);
            chronicle.Observe(sim, labeler.Current);
            hud.SetHeroLabel(labeler.Current);
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
            ObserveStep();
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
            brainPlayedRun = false;
            labeler.Reset();
            chronicle.Reset();
            run++;
            hud.Bind(sim, highlight);
            hud.SetHeroLabel(SpectatorLabel.None);
            hud.SetPaused(paused);
            RefreshInfo();
            characterPanel?.Refresh();
        }

        /// <summary>New run: the profile's current build and tier take effect here (never mid-run).</summary>
        private void RestartNow()
        {
            seed++;
            sim.Config.Build = NextRunBuild();
            sim.Reset(seed);
            survivorRenderer.ResetRun();
            StartRun();
        }

        private void HandleKeys()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || MetaPanel.IsTypingInInputField())
            {
                // No hotkey acts while a text field (loadout name) has the keyboard.
                return;
            }

            if (keyboard.rKey.wasPressedThisFrame && sim != null)
            {
                RestartNow();
                return;
            }

            if (keyboard.cKey.wasPressedThisFrame)
            {
                hud.ToggleCharacterPanel();
            }

            if (keyboard.fKey.wasPressedThisFrame)
            {
                hud.ToggleFarmPanel();
            }

            if (keyboard.vKey.wasPressedThisFrame)
            {
                hud.ToggleComparePanel();
            }

            if (keyboard.lKey.wasPressedThisFrame)
            {
                hud.ToggleLineagePanel();
            }

            // Esc first closes an open full-screen panel; only a second press pauses.
            if (keyboard.escapeKey.wasPressedThisFrame && !hud.PanelHandlesEscape())
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
                hud.ToggleHistoryPanel();
            }

            if (keyboard.pKey.wasPressedThisFrame && profilePanel != null)
            {
                hud.ToggleProfilePanel();
            }

            if (keyboard.bKey.wasPressedThisFrame)
            {
                ToggleBestBrain();
            }
        }

        /// <summary>Switches between the newest training brain and the champion, then starts a fresh run with it.</summary>
        private void ToggleBestBrain()
        {
            if (!string.IsNullOrWhiteSpace(brainFile))
            {
                ShowSwitchNotice("Đang xem một bộ não cố định (-brain), không đổi được.");
                return;
            }

            if (watchedVersion != null)
            {
                // M6: leave the saved version picked in the lineage panel and go back to the newest brain.
                watchedVersion = null;
                watchedBranchName = null;
                watchBest = false;
                PlayerPrefs.SetInt(WatchBestPreference, 0);
                PlayerPrefs.Save();
                loadedPath = null;
                PollBrain();
                ShowSwitchNotice("Đang xem NÃO MỚI NHẤT");
                if (sim != null && pilot.Brain != null)
                {
                    RestartNow();
                }
                return;
            }

            // Based on what is playing, not the saved choice: a saved "best" may have fallen back to newest.
            bool wantBest = !loadedIsChampion;
            if (wantBest && BrainLocator.FindChampionBrain(runsDirectory, BehaviorName) == null)
            {
                ShowSwitchNotice("Chưa có não giỏi nhất.\nNó xuất hiện sau 2 triệu bước huấn luyện.");
                return;
            }

            watchBest = wantBest;
            PlayerPrefs.SetInt(WatchBestPreference, watchBest ? 1 : 0);
            PlayerPrefs.Save();
            PollBrain();
            if (watchBest && !loadedIsChampion)
            {
                // The champion file was rejected or unreadable; stay on the newest brain.
                watchBest = false;
                PlayerPrefs.SetInt(WatchBestPreference, 0);
                PlayerPrefs.Save();
                ShowSwitchNotice("Không nạp được não giỏi nhất.\nVẫn xem NÃO MỚI NHẤT.");
                return;
            }

            ShowSwitchNotice(watchBest ? "Đang xem NÃO GIỎI NHẤT" : "Đang xem NÃO MỚI NHẤT");
            if (sim != null && pilot.Brain != null)
            {
                RestartNow();
            }
        }

        private void FollowChampionRun()
        {
            FollowRun(loadedChampion != null ? Path.Combine(runsDirectory, loadedChampion.run_id) : null);
        }

        /// <summary>Points the history panel at the watched brain's run, unless live training owns it.</summary>
        private void FollowRun(string brainRun)
        {
            if ((training == null || !trainingSnapshot.IsActive) && brainRun != null && Directory.Exists(brainRun))
            {
                historyPanel?.SetRunDirectory(brainRun);
            }
        }

        private void ShowSwitchNotice(string text)
        {
            switchNotice = text;
            switchNoticeUntil = Time.unscaledTime + SwitchNoticeSeconds;
            RefreshInfo();
        }

        private void PollBrain()
        {
            nextPoll = Time.unscaledTime + BrainPollSeconds;
            // Order: -brain, then a version picked in the lineage panel, then the champion (B), then the newest brain.
            string versionPath = string.IsNullOrWhiteSpace(brainFile) && watchedVersion != null ? watchedVersion.BrainPath : null;
            string champion = string.IsNullOrWhiteSpace(brainFile) && versionPath == null && watchBest
                ? BrainLocator.FindChampionBrain(runsDirectory, BehaviorName)
                : null;
            string path = !string.IsNullOrWhiteSpace(brainFile)
                ? brainFile
                : versionPath ?? champion ?? NewestBrainPath();
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
                // The trainer copies champion.brain before champion.json; catch up once the json matches.
                if (loadedIsChampion && (loadedChampion == null || loadedChampion.step != loadedStep))
                {
                    loadedChampion = ChampionInfo.Load(runsDirectory, BehaviorName).Champion;
                    FollowChampionRun();
                    RefreshInfo();
                }

                return;
            }

            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                PolicyBrain brain = PolicyBrain.Load(bytes);
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
                    // Auto Farm builds its own brain instance from these bytes (never shares this one across threads).
                    loadedBrainBytes = bytes;
                    loadedBrainName = versionPath != null
                        ? watchedVersion.Name
                        : champion != null ? "não giỏi nhất" : BrainLocator.RunName(path);
                    loadedAt = DateTime.Now;
                    brainStatus = null;
                    loadedIsChampion = champion != null;
                    loadedIsVersion = versionPath != null;
                    brainLoadCount++;
                    loadedStep = brain.Step;
                    loadedChampion = loadedIsChampion ? ChampionInfo.Load(runsDirectory, BehaviorName).Champion : null;
                    if (loadedIsChampion)
                    {
                        FollowChampionRun();
                    }
                    else if (loadedIsVersion)
                    {
                        FollowRun(TrainingServiceClient.IsSafeRunId(watchedVersion.RunId)
                            ? Path.Combine(runsDirectory, watchedVersion.RunId)
                            : null);
                    }
                    else
                    {
                        FollowRun(BrainLocator.RunDirectory(path));
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
            // The owner's build and tier (once the Warrior has a level or a tier above 1) and the training focus.
            OwnerTraining owner = MetaViewLogic.OwnerTrainingFor(store.Profile);
            // M6: continue the active branch when it has a checkpoint (null: the service picks the newest run).
            CharacterProfile warrior = store.Warrior;
            string runId = LineageStore.TrainRunId(runsDirectory, BehaviorName, warrior != null ? warrior.BrainRunId : null);
            trainingNotice = training.Start(System.Diagnostics.Process.GetCurrentProcess().Id, Powers[powerIndex], BehaviorName, owner, runId);
            startedTraining = trainingNotice == null;
            if (startedTraining)
            {
                startedOwner = owner;
            }
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

            if (trainingSnapshot.State != TrainingState.Unavailable && trainingSnapshot.State != TrainingState.External)
            {
                text += "\n" + TrainingChoicesText(trainingSnapshot.State, status);
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

        /// <summary>
        /// The build line of the training panel: what the running session learns (as reported by the service),
        /// plus a note when the owner's choices changed since; otherwise what the next session will learn.
        /// </summary>
        private string TrainingChoicesText(TrainingState state, TrainingStatus status)
        {
            OwnerTraining current = MetaViewLogic.OwnerTrainingFor(store.Profile);
            bool running = state == TrainingState.Starting || state == TrainingState.Training;
            if (running && status != null)
            {
                string line = MetaViewLogic.TrainingBuildLine(status.owner_build, status.owner_tier, status.training_focus);
                if (MetaViewLogic.TrainingChoicesDiffer(status.owner_build, status.owner_tier, status.training_focus, startedOwner, current))
                {
                    line += "\nThay đổi build/trọng tâm sẽ áp dụng ở lần HUẤN LUYỆN sau";
                }

                return line;
            }

            return MetaViewLogic.TrainingBuildLine(current.Points != null, current.Tier, current.FocusId);
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
            recent.Enqueue(new RecentRun(sim.Time, sim.Level, sim.Gold));
            while (recent.Count > RecentRunCount)
            {
                recent.Dequeue();
            }

            chronicle.Finish(sim);
            hud.SetEndStory(ChronicleText.Format(chronicle.Result));
            hud.SetHeroLabel(SpectatorLabel.None);

            if (brainPlayedRun)
            {
                // A watched run the brain played pays into the wallet (Farm = false), then saves and logs.
                CharacterProfile warrior = store.Warrior;
                RunReward reward = ProfileRules.RecordRun(store.Profile, warrior, runLoadout,
                    MetaViewLogic.ToRunResult(sim, runBuild.Tier, false));
                store.Save();
                store.AppendEconomyLine(EconomyLog.Line(DateTime.UtcNow, EconomyLog.WatchMode, warrior.ClassId, runBuild.Tier,
                    runLoadoutName, runBuild, MetaViewLogic.ToRunStats(sim, seed)));
                hud.SetEndReward(MetaViewLogic.RewardText(reward), true);
                characterPanel?.Refresh();
                comparePanel?.Refresh();
            }
            else
            {
                hud.SetEndReward(MetaViewLogic.RewardText(null), false);
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
                if (loadedIsVersion && watchedVersion != null)
                {
                    text = "AI CHIẾN BINH\nNão: " + watchedVersion.Name + " (" + watchedBranchName + ")" +
                        "\nĐã học " + brain.Step.ToString("N0", culture) + " bước" +
                        "   Phiên bản đã lưu   (B: não mới nhất)";
                }
                else if (loadedIsChampion)
                {
                    text = "AI CHIẾN BINH   NÃO GIỎI NHẤT" +
                        (loadedChampion != null ? "   " + loadedChampion.run_id : string.Empty) +
                        "\nĐã học " + brain.Step.ToString("N0", culture) + " bước" +
                        (loadedChampion != null ? (loadedChampion.passes_m4a ? "   đạt M4A" : "   chưa đạt M4A") : string.Empty) +
                        "\nĐổi khi có não chấm điểm cao hơn   (B: não mới nhất)";
                }
                else
                {
                    text = "AI CHIẾN BINH   " + BrainLocator.RunName(loadedPath) +
                        "\nĐã học " + brain.Step.ToString("N0", culture) + " bước" +
                        "   (nạp lúc " + loadedAt.ToString("HH:mm", culture) + ")" +
                        "\nNão mới nhất, tự cập nhật   (B: não giỏi nhất)";
                }
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
                foreach (RecentRun result in recent)
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

            // M5: wallet, a build change waiting for the next run, and the Auto Farm progress.
            if (store != null)
            {
                text += "\n" + MetaViewLogic.WalletText(store.Profile);
                if (IsBuildChangePending())
                {
                    text += "\nThay đổi build: áp dụng từ trận sau";
                }
            }
            string farmLine = farmPanel != null ? farmPanel.StatusLine : null;
            if (!string.IsNullOrEmpty(farmLine))
            {
                text += "\n" + farmLine;
            }
            nextFarmInfoRefresh = Time.unscaledTime + FarmInfoRefreshSeconds;

            hud.SetInfoText(text);
            hud.SetNotice(switchNotice ?? (brain == null ? brainStatus : null));
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
            if (!string.IsNullOrEmpty(openPanel))
            {
                UpdatePanelScreenshot(real);
                return;
            }

            if (showProfile)
            {
                // Profile check: one shot of the open AI profile, then (optionally) quit.
                if (!highlightShotTaken && real > 6f)
                {
                    highlightShotTaken = true;
                    Capture(screenshotPath);
                    if (quitAfterScreenshot)
                    {
                        quitAt = Time.unscaledTime + 1.5f;
                    }
                }
                return;
            }

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

            // -labelShot: the spectator tag above the hero, once it has been visible for a second.
            bool labelVisible = labeler.Current != SpectatorLabel.None && !highlight.BlocksSim && !sim.IsEnded;
            labelSeenSince = labelVisible ? Mathf.Min(labelSeenSince, real) : float.PositiveInfinity;
            if (labelShot && !labelShotTaken && ((labelVisible && real - labelSeenSince >= 1f && sim.Time >= 20f) || real > 200f))
            {
                labelShotTaken = true;
                Capture(SiblingPath(screenshotPath, "_label"));
            }

            // -endShot: the end screen with the reward and the run story.
            if (endShot && !endShotTaken && sim.IsEnded && endTimer >= 1.5f)
            {
                endShotTaken = true;
                Capture(SiblingPath(screenshotPath, "_end"));
            }

            bool done = highlightShotTaken && hordeShotTaken && (!labelShot || labelShotTaken) && (!endShot || endShotTaken);
            if (done && quitAfterScreenshot && float.IsPositiveInfinity(quitAt))
            {
                quitAt = Time.unscaledTime + 1.5f;
            }
        }

        /// <summary>-openPanel character|farm|compare|lineage: open that panel (farm: start a session), capture it, then optionally quit.</summary>
        private void UpdatePanelScreenshot(float real)
        {
            string panel = openPanel.Trim().ToLowerInvariant();
            if (!panelOpened && real > PanelOpenSeconds)
            {
                panelOpened = true;
                switch (panel)
                {
                    case "farm":
                        hud.ToggleFarmPanel();
                        farmPanel.StartFarm(farmRuns);
                        break;
                    case "compare":
                        hud.ToggleComparePanel();
                        break;
                    case "lineage":
                        hud.ToggleLineagePanel();
                        break;
                    default:
                        hud.ToggleCharacterPanel();
                        break;
                }
            }

            float shotAt = panel == "farm" ? FarmShotSeconds : PanelShotSeconds;
            if (panelOpened && !highlightShotTaken && real > shotAt)
            {
                highlightShotTaken = true;
                Capture(screenshotPath);
                if (quitAfterScreenshot)
                {
                    quitAt = Time.unscaledTime + 1.5f;
                }
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

        private readonly struct RecentRun
        {
            public readonly float Seconds;
            public readonly int Level;
            public readonly float Gold;

            public RecentRun(float seconds, int level, float gold)
            {
                Seconds = seconds;
                Level = level;
                Gold = gold;
            }
        }
    }
}
