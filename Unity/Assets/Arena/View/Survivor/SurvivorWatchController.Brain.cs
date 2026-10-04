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
    /// <summary>Which brain plays: the newest or the best, a saved lineage version, and reloading it while training saves.</summary>
    public sealed partial class SurvivorWatchController
    {
        /// <summary>"Watch now" in the lineage panel: watch that saved version now (fresh run), until B.</summary>
        private void OnWatchVersionRequested(LineageVersion version, string branchName)
        {
            if (!string.IsNullOrWhiteSpace(brainFile))
            {
                ShowSwitchNotice("Watching a fixed brain (-brain); it cannot be switched.");
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
                ShowSwitchNotice("Cannot watch version " + version.Name + ".\n" +
                    (string.IsNullOrEmpty(problem) ? "The brain file could not be read." : problem));
                return;
            }

            ShowSwitchNotice("Watching version " + version.Name + " — press B for the newest brain");
            switchNoticeUntil = Time.unscaledTime + ProfileNoticeSeconds;
            if (sim != null && pilot.Brain != null)
            {
                RestartNow();
            }
        }

        /// <summary>Newest brain: the active branch's latest.brain when it exists with the current schema, else the newest run's.</summary>
        private string NewestBrainPath()
        {
            CharacterProfile warrior = CurrentCharacter;
            string branchBrain = warrior != null
                ? LineageStore.BranchLatestBrain(runsDirectory, BehaviorName, warrior.BrainRunId)
                : null;
            return branchBrain ?? BrainLocator.FindNewestBrain(runsDirectory, BehaviorName);
        }

        /// <summary>Switches between the newest training brain and the champion, then starts a fresh run with it.</summary>
        private void ToggleBestBrain()
        {
            if (!string.IsNullOrWhiteSpace(brainFile))
            {
                ShowSwitchNotice("Watching a fixed brain (-brain); it cannot be switched.");
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
                ShowSwitchNotice("Watching the NEWEST BRAIN");
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
                ShowSwitchNotice("No best brain yet.\nIt appears after 2M training steps.");
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
                ShowSwitchNotice("The best brain could not be loaded.\nStill watching the NEWEST BRAIN.");
                return;
            }

            ShowSwitchNotice(watchBest ? "Watching the BEST BRAIN" : "Watching the NEWEST BRAIN");
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
                ? FixedBrainPath()
                : versionPath ?? champion ?? NewestBrainPath();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                if (pilot.Brain == null)
                {
                    if (runsDirectory == null && string.IsNullOrWhiteSpace(brainFile))
                    {
                        brainStatus = "Folder Trainer/runs not found.";
                    }
                    else if (string.IsNullOrWhiteSpace(brainFile) && classId == ProfileRules.WarriorId &&
                        BrainLocator.FindNewestBrain(runsDirectory, BehaviorName, false) != null)
                    {
                        brainStatus = "The rules changed to Survivor mode.\nThe old brain does not know them, so the warrior stands idle.\n" +
                            "Press TRAIN AI to teach it the new rules.";
                    }
                    else
                    {
                        // M7: a bought class starts without a brain ("Chưa có não Mage — bấm HUẤN LUYỆN AI");
                        // its pre-Survivor runs (mage-001, archer-001) are never loaded.
                        brainStatus = ClassViewLogic.NoBrainText(classId, training != null);
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
                    problem = "The brain failed its self-check.";
                }

                loadedPath = path;
                loadedWriteTime = written;
                if (problem != null)
                {
                    brainStatus = "Brain rejected: " + problem;
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
                        : champion != null ? "best brain" : BrainLocator.RunName(path);
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
                brainStatus = "Could not read the brain: " + exception.Message;
                Debug.LogWarning("Could not load brain " + path + ": " + exception.Message);
            }

            RefreshInfo();
        }
    }
}
