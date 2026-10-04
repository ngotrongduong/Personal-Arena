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
    /// <summary>Start-up: command-line options, panel wiring and the first run.</summary>
    public sealed partial class SurvivorWatchController
    {
        private void Start()
        {
            PerfTrace.Mark("controller start");
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
            enemyShot = HasArgument("-enemyShot");
            watchBest = HasArgument("-best") || PlayerPrefs.GetInt(WatchBestPreference, 0) == 1;

            // M8: automated runs never make noise (-screenshot, -quitAfterScreenshot, -smokeTest, -mute);
            // -audioLog <path> writes the cue counts so the wiring can be checked without hearing it.
            if (survivorAudio != null)
            {
                bool forceSilent = !string.IsNullOrEmpty(screenshotPath) || quitAfterScreenshot ||
                    HasArgument("-smokeTest") || HasArgument("-mute");
                survivorAudio.Configure(forceSilent, CommandLineValue("-audioLog"), "seed " + seed.ToString(CultureInfo.InvariantCulture));
                survivorAudio.SetCamera(followCamera != null ? followCamera.ViewCamera : Camera.main);
            }

            // Profile: -profile <folder or file> for checks and screenshots, else the owner's real profile.
            string profileOverride = CommandLineValue("-profile");

            // M8 -smokeTest <report.json>: refuses to run without a scratch -profile and a -brain (never the real profile).
            if (HasArgument("-smokeTest") && !BeginSmokeTest(profileOverride))
            {
                return;
            }

            store = ProfileStore.Create(profileOverride, Application.persistentDataPath,
                Path.Combine(Application.temporaryCachePath, "profile-fallback"));
            ShowProfileLoadNotice(store.Load());
            classId = store.SelectedClassId;

            // -runSeconds N (60..900) shortens runs so the end screen can be checked; only with a scratch -profile.
            if (!string.IsNullOrWhiteSpace(profileOverride) &&
                float.TryParse(CommandLineValue("-runSeconds"), NumberStyles.Float, CultureInfo.InvariantCulture, out float requestedRun))
            {
                shortRunSeconds = Mathf.Clamp(requestedRun, 60f, 900f);
            }
            if (smoke != null)
            {
                // The smoke test plays short runs at x8.
                shortRunSeconds = shortRunSeconds > 0f ? shortRunSeconds : SmokeRunSeconds;
                speedIndex = SpeedSteps.Length - 1;
            }

            hud.SetHelpText(
                "The AI plays by itself - you only watch.\n" +
                "Space   watch speed          T   pick mode (random / best)\n" +
                "Esc   pause                        R   new run\n" +
                "B   newest brain / best brain\n" +
                "C   character      F   gold farm      V   compare builds\n" +
                "G   charts      P   AI profile      L   brain history\n" +
                "M   mute      O   settings      Tab   hide the side panels\n" +
                "Mouse wheel   zoom      Hover an icon   details      H   close");

            // M8 settings: quality and window mode apply at start (automated runs keep their window as launched).
            PlayerPrefsSoundStorage prefs = new PlayerPrefsSoundStorage();
            viewerSettings = ViewerSettings.Load(prefs);
            BeginPerfLog();
            bool applyWindowMode = string.IsNullOrEmpty(screenshotPath) && !quitAfterScreenshot && smoke == null &&
                string.IsNullOrEmpty(perfLogPath);
            SettingsPanel.ApplyDisplay(viewerSettings, applyWindowMode);
            SettingsPanel settingsPanel = hud.SettingsPanel;
            if (settingsPanel != null)
            {
                settingsPanel.Bind(survivorAudio, viewerSettings, prefs, applyWindowMode);
                settingsPanel.DisplayChanged += OnDisplayChanged;
            }
            hud.SetFpsVisible(viewerSettings.ShowFps);

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
                lineageBound = true;
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
            else if (!string.IsNullOrWhiteSpace(runsDirectory) && Directory.Exists(runsDirectory))
            {
                // A fixed -brain with a runs folder: no TRAIN button, but the charts (G) and AI profile (P) still open.
                hud.EnsureAnalysisPanels();
                historyPanel = hud.HistoryPanel;
                historyPanel?.SetRunDirectory(BrainLocator.FindNewestRunDirectory(runsDirectory, BehaviorName));
                profilePanel = hud.ProfilePanel;
                profilePanel?.SetSource(runsDirectory, BehaviorName);
                if (showProfile)
                {
                    profilePanel?.SetOpen(true);
                }
            }

            SurvivorConfig config = new SurvivorConfig { Build = NextRunBuild(), ClassDef = ClassDefinition(classId) };
            if (shortRunSeconds > 0f)
            {
                config.RunSeconds = shortRunSeconds;
            }

            PerfTrace.Mark("panels bound");
            sim = new SurvivorSim(config, seed);
            followCamera.SetTarget(survivorRenderer);
            survivorRenderer.SetCamera(followCamera.ViewCamera);
            survivorRenderer.Bind(sim);
            survivorRenderer.WeaponEvolved += OnWeaponEvolved;
            hud.BindHeroLabel(survivorRenderer, followCamera.ViewCamera);
            PerfTrace.Mark("renderer bound");
            StartRun();
            PerfTrace.Mark("run started");
            PollBrain();
            PerfTrace.Mark("brain polled");

            // -watchVersion <id> (checks the build): start on a saved lineage version, as "Watch now" does.
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

        private void ShowProfileLoadNotice(ProfileLoadOutcome outcome)
        {
            string text;
            switch (outcome)
            {
                case ProfileLoadOutcome.RecoveredFromBackup:
                    text = "The profile was damaged, so the latest backup was used.";
                    break;
                case ProfileLoadOutcome.RecoveredNew:
                    text = "The profile was damaged and there is no backup:\na new profile was created (the damaged file is kept).";
                    break;
                case ProfileLoadOutcome.ReadError:
                    text = "The profile could not be read (file locked?).\nGold earned this time will not be saved.";
                    break;
                default:
                    return;
            }

            Debug.LogWarning("Profile " + store.ProfilePath + ": " + outcome);
            switchNotice = text;
            switchNoticeUntil = Time.unscaledTime + ProfileNoticeSeconds;
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
            if (survivorAudio == null)
            {
                survivorAudio = GetComponent<SurvivorAudio>();
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
    }
}
