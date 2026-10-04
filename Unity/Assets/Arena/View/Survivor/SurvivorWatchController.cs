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
    /// "Watch AI" mode of the survivor game: the newest brain of the selected class plays 15-minute graveyard runs,
    /// new brains exported by training are hot-loaded, and each level-up shows which item the AI picked.
    /// M5: every run uses the owner's build and tier (<see cref="ProfileRules.ToBuild"/>, applied at run start),
    /// a run the brain played pays gold into the saved profile, and the character (C), Auto Farm (F) and
    /// build comparison (V) panels edit the profile. All rules run in Core; this class only steps, saves and draws.
    /// M7: the selected class (profile SelectedClassId) drives the brain, kit, panels and TRAIN; changing it
    /// restarts the watched run with that class's brain.
    /// </summary>
    public sealed partial class SurvivorWatchController : MonoBehaviour
    {
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
        [Tooltip("Optional fixed brain file; otherwise the newest brain matching the current schema and class is used.")]
        [SerializeField] private string brainFile = string.Empty;

        [Header("Scene References")]
        [SerializeField] private SurvivorRenderer survivorRenderer;
        [SerializeField] private SurvivorHud hud;
        [SerializeField] private SurvivorCamera followCamera;
        [Tooltip("M8 sound; optional (a scene without it stays silent).")]
        [SerializeField] private SurvivorAudio survivorAudio;

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

        // M7: the watched and trained class, and a TRAIN press that stops another class's training first.
        private string classId = ProfileRules.WarriorId;
        private bool lineageBound;
        private bool pendingClassStart;
        private string switchFromBehavior;

        // M8 settings (O / gear): window mode, graphics quality and the FPS counter, saved in PlayerPrefs.
        private ViewerSettings viewerSettings;

        // Screenshot mode (-screenshot <png> [-quitAfterScreenshot] [-showProfile] [-openPanel character|farm|compare|lineage]
        // [-farmRuns N] [-endShot] [-labelShot] [-enemyShot]) used to check the build. Pass -profile <scratch path> with it.
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
        // -enemyShot: one more shot once the M7 enemies (exploder, ghost, necromancer) are in the run.
        private bool enemyShot;
        private bool enemyShotTaken;
        private const float EnemyShotGiveUpSeconds = 300f;
        private float labelSeenSince = float.PositiveInfinity;
        private float quitAt = float.PositiveInfinity;

        public SurvivorSim Sim => sim;
        public SurvivorPilot Pilot => pilot;
        /// <summary>The class on screen ("warrior", "mage", "archer").</summary>
        public string ClassId => classId;
        /// <summary>ML-Agents behavior name of the class on screen ("Warrior", "Mage", "Archer").</summary>
        public string BehaviorName => ClassViewLogic.BehaviorName(classId);

        /// <summary>The character record of the class on screen.</summary>
        private CharacterProfile CurrentCharacter =>
            store != null ? ProfileRules.FindCharacter(store.Profile, classId) ?? store.Selected : null;

        /// <summary>The behavior the training service trains now (from its status), or null.</summary>
        private string RunningBehavior => ClassViewLogic.StatusBehavior(trainingSnapshot.Status != null, trainingSnapshot.Status?.behavior);

        /// <summary>True while the service trains a class other than the one on screen.</summary>
        private bool TrainingOtherClass => trainingSnapshot.IsActive && ClassViewLogic.IsOtherClass(RunningBehavior, BehaviorName);

        /// <summary>HUD toast "TIẾN HÓA: &lt;tên&gt;" when the hero's weapon evolves.</summary>
        private void OnWeaponEvolved(int evolutionIndex)
        {
            if (hud != null)
            {
                hud.ShowToast(SurvivorViewLogic.EvolvedToast(evolutionIndex), SurvivorViewLogic.EvolutionGold);
            }
        }

        private void OnDestroy()
        {
            if (survivorRenderer != null)
            {
                survivorRenderer.WeaponEvolved -= OnWeaponEvolved;
            }
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
            if (hud != null && hud.SettingsPanel != null)
            {
                hud.SettingsPanel.DisplayChanged -= OnDisplayChanged;
            }
            EndSmokeLogCapture();
        }

        /// <summary>A display setting changed in the settings panel: show or hide the FPS counter.</summary>
        private void OnDisplayChanged()
        {
            if (hud != null && viewerSettings != null)
            {
                hud.SetFpsVisible(viewerSettings.ShowFps);
            }
        }

        private void OnApplicationQuit()
        {
            AbortSmokeTestOnQuit();

            // Auto Farm: cancel, wait up to 2 s, book the finished matches (the panel also does this itself).
            farmPanel?.ShutDown();

            // The service also stops when this process exits; asking first saves a few seconds.
            if (training != null && startedTraining && trainingSnapshot.IsActive)
            {
                training.RequestStop();
            }
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
            UpdateSmokeTest();
            UpdatePerfLog();

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

        /// <summary>After every sim step (the pick step too): spectator label, chronicle, HUD tag, sounds.</summary>
        private void ObserveStep()
        {
            brainPlayedRun = true;
            labeler.Observe(sim, sim.LastStepSeconds);
            chronicle.Observe(sim, labeler.Current);
            hud.SetHeroLabel(labeler.Current);
            if (survivorAudio != null)
            {
                survivorAudio.OnStep(sim);
            }
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
            if (survivorAudio != null && highlight.BlocksSim)
            {
                survivorAudio.OnCardsShown();
            }
            if (smoke != null && highlight.BlocksSim)
            {
                SmokeOfferShown();
            }
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
            if (survivorAudio != null)
            {
                survivorAudio.OnPickHighlighted();
            }
            if (smoke != null)
            {
                SmokePicked();
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
            if (survivorAudio != null)
            {
                survivorAudio.OnRunStarted();
            }
            if (smoke != null)
            {
                SmokeRunStarted();
            }
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

            if (keyboard.oKey.wasPressedThisFrame)
            {
                hud.ToggleSettingsPanel();
            }

            // Esc first closes an open full-screen panel; only a second press pauses.
            if (keyboard.escapeKey.wasPressedThisFrame && !hud.PanelHandlesEscape())
            {
                paused = !paused;
                accumulator = 0f;
                hud.SetPaused(paused);
                if (survivorAudio != null)
                {
                    survivorAudio.SetPaused(paused);
                }
            }

            if (keyboard.mKey.wasPressedThisFrame && survivorAudio != null)
            {
                ShowSwitchNotice(survivorAudio.ToggleMute() ? "Đã tắt tiếng" : "Đã bật tiếng");
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
