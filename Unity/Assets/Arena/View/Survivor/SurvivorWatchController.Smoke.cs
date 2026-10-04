using System;
using System.IO;
using System.Text;
using PersonalArena.Core.Meta;
using UnityEngine;

namespace PersonalArena.View
{
    /// <summary>
    /// M8 -smokeTest &lt;report.json&gt;: an end-to-end check of the built viewer. Needs a scratch -profile and a
    /// -brain (a sibling "&lt;class&gt;.brain" next to it is used for that class). It starts a 2-run Auto Farm, then
    /// buys (granting gold) and selects Warrior, Mage and Archer in turn and watches one short run of each at x8:
    /// the run starts, a level-up offer is shown and picked, the run ends with a reason, its gold is booked and
    /// saved, and the next run starts. Then the C, F, V, L, G, P and O panels open and close, and the farm must be
    /// booked. Any Error/Exception log or a timeout fails it. Writes the report and quits with 0 (passed) or 1.
    /// </summary>
    public sealed partial class SurvivorWatchController
    {
        // 120 s (not 60): a ranged class often kills far from its gems and is still level 1 after 60 s
        // (Archer champion: 5 of 30 seeds); at 120 s none of 50 seeds stayed level 1.
        private const float SmokeRunSeconds = 120f;
        private const float SmokeTimeLimitSeconds = 540f;
        private const float SmokeClassTimeoutSeconds = 120f;
        private const float SmokeBrainWaitSeconds = 30f;
        private const float SmokePanelSettleSeconds = 0.4f;
        private const int SmokeFarmRuns = 2;
        private static readonly string[] SmokeClasses = { ProfileRules.WarriorId, ProfileRules.MageId, ProfileRules.ArcherId };
        private static readonly string[] SmokePanels = { "C", "F", "V", "L", "G", "P", "O", "K" };

        private enum SmokeStage
        {
            WaitBrain,
            StartClass,
            PlayRun,
            WaitNextRun,
            Panels,
            WaitFarm,
            Finished
        }

        private readonly object smokeLock = new object();
        private SmokeTestReport smoke;
        private string smokeReportPath;
        private bool smokeLogging;
        private float smokeStartedAt;
        private SmokeStage smokeStage;
        private float smokeStageSince;
        private int smokeClassIndex = -1;
        private SmokeClassResult smokeCurrent;
        private bool smokeAwaitingStart;
        private int smokeFarmBaseline;
        private int smokePanelIndex;
        private bool smokePanelOpened;
        private bool smokePanelToggled;
        private bool smokePanelFailed;

        /// <summary>Sets up the smoke test; false (after writing a failing report and quitting) when it must not run.</summary>
        private bool BeginSmokeTest(string profileOverride)
        {
            smoke = new SmokeTestReport();
            smokeStartedAt = Time.realtimeSinceStartup;
            smokeReportPath = CommandLineValue("-smokeTest");
            if (smokeReportPath != null && smokeReportPath.StartsWith("-", StringComparison.Ordinal))
            {
                smokeReportPath = null;
            }

            lock (smokeLock)
            {
                Application.logMessageReceivedThreaded += OnSmokeLog;
                smokeLogging = true;
            }

            string refused = string.IsNullOrWhiteSpace(smokeReportPath)
                ? "-smokeTest needs a report path: -smokeTest <report.json>."
                : SmokeTestReport.RefusalReason(profileOverride, CommandLineValue("-brain"), Application.persistentDataPath);
            if (refused != null)
            {
                smoke.Refused = refused;
                Debug.LogWarning("Smoke test refused: " + refused);
                FinishSmokeTest();
                enabled = false;
                return false;
            }

            Debug.Log("Smoke test started: report " + smokeReportPath + ", profile " + profileOverride + ", version " +
                Application.version + ", persistentDataPath " + Application.persistentDataPath + ".");
            SetSmokeStage(SmokeStage.WaitBrain);
            return true;
        }

        private void EndSmokeLogCapture()
        {
            lock (smokeLock)
            {
                if (smokeLogging)
                {
                    Application.logMessageReceivedThreaded -= OnSmokeLog;
                    smokeLogging = false;
                }
            }
        }

        /// <summary>Every Error, Assert or Exception log line (any thread) fails the smoke test.</summary>
        private void OnSmokeLog(string message, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)
            {
                return;
            }

            lock (smokeLock)
            {
                if (smoke != null && smokeStage != SmokeStage.Finished)
                {
                    string where = string.IsNullOrEmpty(stackTrace) ? string.Empty : " | " + FirstLine(stackTrace);
                    smoke.AddError(type + ": " + message + where);
                }
            }
        }

        private static string FirstLine(string text)
        {
            int end = text.IndexOf('\n');
            return (end >= 0 ? text.Substring(0, end) : text).Trim();
        }

        /// <summary>The fixed brain for the class on screen: in the smoke test "&lt;class&gt;.brain" beside -brain when it exists.</summary>
        private string FixedBrainPath()
        {
            if (smoke == null || string.IsNullOrWhiteSpace(brainFile))
            {
                return brainFile;
            }

            try
            {
                string directory = Path.GetDirectoryName(Path.GetFullPath(brainFile));
                string perClass = string.IsNullOrEmpty(directory) ? null : Path.Combine(directory, classId + ".brain");
                return perClass != null && File.Exists(perClass) ? perClass : brainFile;
            }
            catch (Exception exception) when (exception is ArgumentException || exception is IOException ||
                exception is NotSupportedException || exception is System.Security.SecurityException)
            {
                return brainFile;
            }
        }

        private void SetSmokeStage(SmokeStage stage)
        {
            smokeStage = stage;
            smokeStageSince = Time.realtimeSinceStartup;
        }

        private float SmokeStageSeconds => Time.realtimeSinceStartup - smokeStageSince;

        private void SmokeError(string message)
        {
            Debug.LogWarning("Smoke test: " + message);
            lock (smokeLock)
            {
                smoke.AddError(message);
            }
        }

        /// <summary>One step of the smoke test per frame (called from Update while a run exists).</summary>
        private void UpdateSmokeTest()
        {
            if (smoke == null || smokeStage == SmokeStage.Finished)
            {
                return;
            }

            if (Time.realtimeSinceStartup - smokeStartedAt > SmokeTimeLimitSeconds)
            {
                smoke.TimedOut = true;
                SmokeError("timed out in stage " + smokeStage + " after " + SmokeTimeLimitSeconds + " s");
                FinishSmokeTest();
                return;
            }

            switch (smokeStage)
            {
                case SmokeStage.WaitBrain:
                    UpdateSmokeWaitBrain();
                    break;
                case SmokeStage.StartClass:
                    StartSmokeClass();
                    break;
                case SmokeStage.PlayRun:
                case SmokeStage.WaitNextRun:
                    if (SmokeStageSeconds > SmokeClassTimeoutSeconds)
                    {
                        SmokeError(smokeCurrent.ClassId + ": " + (smokeCurrent.Problem ?? "stuck") + " after " +
                            SmokeClassTimeoutSeconds + " s");
                        SetSmokeStage(SmokeStage.StartClass);
                    }
                    else if (smokeStage == SmokeStage.PlayRun && pilot.Brain == null && SmokeStageSeconds > SmokeBrainWaitSeconds)
                    {
                        SmokeError(smokeCurrent.ClassId + ": no brain loaded (" + (brainStatus ?? "unknown") + ")");
                        SetSmokeStage(SmokeStage.StartClass);
                    }
                    break;
                case SmokeStage.Panels:
                    UpdateSmokePanels();
                    break;
                case SmokeStage.WaitFarm:
                    UpdateSmokeWaitFarm();
                    break;
            }
        }

        /// <summary>Once the first brain is loaded: start the 2-run Auto Farm, then the classes.</summary>
        private void UpdateSmokeWaitBrain()
        {
            if (loadedBrainBytes == null)
            {
                if (SmokeStageSeconds > SmokeBrainWaitSeconds)
                {
                    SmokeError("no brain loaded at start (" + (brainStatus ?? "unknown") + ")");
                    FinishSmokeTest();
                }
                return;
            }

            ProfileStats stats = store.Profile.Stats;
            smokeFarmBaseline = stats != null ? stats.FarmRuns : 0;
            smoke.FarmStarted = farmPanel != null && farmPanel.StartFarm(SmokeFarmRuns);
            if (!smoke.FarmStarted)
            {
                SmokeError("Auto Farm did not start");
            }

            SetSmokeStage(SmokeStage.StartClass);
        }

        /// <summary>Buys (granting the price in gold), selects and starts the next class; after the last one, the panels.</summary>
        private void StartSmokeClass()
        {
            smokeClassIndex++;
            if (smokeClassIndex >= SmokeClasses.Length)
            {
                smokeCurrent = null;
                smokePanelIndex = 0;
                smokePanelOpened = false;
                smokePanelToggled = false;
                SetSmokeStage(SmokeStage.Panels);
                return;
            }

            string id = SmokeClasses[smokeClassIndex];
            smokeCurrent = new SmokeClassResult { ClassId = id };
            smoke.Classes.Add(smokeCurrent);

            PlayerProfile profile = store.Profile;
            if (!ProfileRules.OwnsClass(profile, id))
            {
                long price = ProfileRules.ClassPrice(id);
                if (profile.Gold < price)
                {
                    profile.Gold = price;
                }
                if (!ProfileRules.TryBuyClass(profile, id))
                {
                    SmokeError(id + ": TryBuyClass failed");
                }
            }

            if (!ProfileRules.TrySelectClass(profile, id))
            {
                SmokeError(id + ": TrySelectClass failed");
            }
            if (!store.Save())
            {
                SmokeError(id + ": saving the profile after buying failed");
            }

            smokeCurrent.Selected = ProfileRules.OwnsClass(profile, id) && string.Equals(store.SelectedClassId, id, StringComparison.Ordinal);
            Debug.Log("Smoke test: class " + id + " selected=" + smokeCurrent.Selected + ", gold " + profile.Gold + ".");

            SetSmokeStage(SmokeStage.PlayRun);
            smokeAwaitingStart = true;
            paused = false;
            if (!string.Equals(classId, id, StringComparison.Ordinal))
            {
                SwitchClass(id);
            }
            else
            {
                RestartNow();
            }
        }

        /// <summary>StartRun hook: the class's run, or the run after it.</summary>
        private void SmokeRunStarted()
        {
            if (smokeCurrent == null)
            {
                return;
            }

            if (smokeStage == SmokeStage.PlayRun && smokeAwaitingStart)
            {
                smokeAwaitingStart = false;
                smokeCurrent.RunStarted = string.Equals(classId, smokeCurrent.ClassId, StringComparison.Ordinal);
            }
            else if (smokeStage == SmokeStage.WaitNextRun)
            {
                smokeCurrent.NextRunStarted = true;
                Debug.Log("Smoke test: class " + smokeCurrent.ClassId + " passed=" + smokeCurrent.Passed + ".");
                SetSmokeStage(SmokeStage.StartClass);
            }
        }

        /// <summary>BeginOffer hook: level-up cards are on screen.</summary>
        private void SmokeOfferShown()
        {
            if (smokeCurrent != null && smokeStage == SmokeStage.PlayRun && !smokeAwaitingStart)
            {
                smokeCurrent.OffersShown++;
            }
        }

        /// <summary>ResolvePick hook: the brain picked a card.</summary>
        private void SmokePicked()
        {
            if (smokeCurrent != null && smokeStage == SmokeStage.PlayRun && !smokeAwaitingStart)
            {
                smokeCurrent.Picks++;
            }
        }

        /// <summary>RecordResultOnce hook: the run ended and its gold was booked; checks the wallet and the file on disk.</summary>
        private void SmokeRunBooked(string bookedClassId, long goldBefore, RunReward reward, bool saved)
        {
            if (smokeCurrent == null || smokeStage != SmokeStage.PlayRun || smokeAwaitingStart)
            {
                return;
            }

            smokeCurrent.EndReason = sim.EndReason.ToString();
            smokeCurrent.GoldAdded = reward != null ? reward.GoldAdded : 0L;
            bool rightClass = string.Equals(bookedClassId, smokeCurrent.ClassId, StringComparison.Ordinal);
            smokeCurrent.GoldBooked = rightClass && reward != null && store.Profile.Gold == goldBefore + reward.GoldAdded;
            smokeCurrent.Saved = saved && SavedProfileMatches();
            Debug.Log("Smoke test: class " + smokeCurrent.ClassId + " run ended (" + smokeCurrent.EndReason + "), +" +
                smokeCurrent.GoldAdded + " gold, offers " + smokeCurrent.OffersShown + ", picks " + smokeCurrent.Picks +
                ", saved " + smokeCurrent.Saved + ".");
            SetSmokeStage(SmokeStage.WaitNextRun);
        }

        /// <summary>Reads the profile file back and compares the wallet and run count with the profile in memory.</summary>
        private bool SavedProfileMatches()
        {
            try
            {
                ProfileStore check = new ProfileStore(store.DirectoryPath, Path.GetFileName(store.ProfilePath));
                if (check.Load() != ProfileLoadOutcome.Loaded)
                {
                    return false;
                }

                PlayerProfile disk = check.Profile;
                PlayerProfile memory = store.Profile;
                return disk.Gold == memory.Gold && disk.Stats != null && memory.Stats != null && disk.Stats.Runs == memory.Stats.Runs;
            }
            catch (Exception exception) when (exception is IOException || exception is ArgumentException ||
                exception is UnauthorizedAccessException)
            {
                SmokeError("reading the saved profile failed: " + exception.Message);
                return false;
            }
        }

        /// <summary>Opens and closes each panel in turn (a short settle after each toggle).</summary>
        private void UpdateSmokePanels()
        {
            if (smokePanelIndex >= SmokePanels.Length)
            {
                smoke.PanelsPassed = !smokePanelFailed;
                SetSmokeStage(SmokeStage.WaitFarm);
                return;
            }

            string key = SmokePanels[smokePanelIndex];
            if (!smokePanelToggled)
            {
                ToggleSmokePanel(key);
                smokePanelToggled = true;
                smokeStageSince = Time.realtimeSinceStartup;
                return;
            }

            if (SmokeStageSeconds < SmokePanelSettleSeconds)
            {
                return;
            }

            bool open = IsSmokePanelOpen(key);
            if (!smokePanelOpened)
            {
                if (!open)
                {
                    smokePanelFailed = true;
                    SmokeError("panel " + key + " did not open");
                    smoke.PanelsChecked.Add(key + ": did not open");
                    NextSmokePanel();
                    return;
                }

                smokePanelOpened = true;
                smokePanelToggled = false;
                return;
            }

            if (open)
            {
                smokePanelFailed = true;
                SmokeError("panel " + key + " did not close");
                smoke.PanelsChecked.Add(key + ": did not close");
            }
            else
            {
                smoke.PanelsChecked.Add(key + ": ok");
            }

            NextSmokePanel();
        }

        private void NextSmokePanel()
        {
            smokePanelIndex++;
            smokePanelOpened = false;
            smokePanelToggled = false;
            hud.SettingsPanel?.SetOpen(false);
        }

        private void ToggleSmokePanel(string key)
        {
            switch (key)
            {
                case "C": hud.ToggleCharacterPanel(); break;
                case "F": hud.ToggleFarmPanel(); break;
                case "V": hud.ToggleComparePanel(); break;
                case "L": hud.ToggleLineagePanel(); break;
                case "K": hud.ToggleCodexPanel(); break;
                case "G": hud.ToggleHistoryPanel(); break;
                case "P": hud.ToggleProfilePanel(); break;
                default: hud.ToggleSettingsPanel(); break;
            }
        }

        private bool IsSmokePanelOpen(string key)
        {
            switch (key)
            {
                case "C": return hud.CharacterPanel != null && hud.CharacterPanel.IsOpen;
                case "F": return hud.FarmPanel != null && hud.FarmPanel.IsOpen;
                case "V": return hud.ComparePanel != null && hud.ComparePanel.IsOpen;
                case "L": return hud.LineagePanel != null && hud.LineagePanel.IsOpen;
                case "K": return hud.CodexPanel != null && hud.CodexPanel.IsOpen;
                case "G": return hud.HistoryPanel != null && hud.HistoryPanel.IsOpen;
                case "P": return hud.ProfilePanel != null && hud.ProfilePanel.IsOpen;
                default: return hud.SettingsPanel != null && hud.SettingsPanel.IsOpen;
            }
        }

        /// <summary>The Auto Farm must finish and book both runs (FarmRuns +2).</summary>
        private void UpdateSmokeWaitFarm()
        {
            if (!smoke.FarmStarted)
            {
                FinishSmokeTest();
                return;
            }

            if (farmPanel != null && farmPanel.IsFarming)
            {
                return;
            }

            ProfileStats stats = store.Profile.Stats;
            int booked = (stats != null ? stats.FarmRuns : 0) - smokeFarmBaseline;
            smoke.FarmBooked = booked >= SmokeFarmRuns;
            if (!smoke.FarmBooked)
            {
                SmokeError("Auto Farm booked " + booked + " of " + SmokeFarmRuns + " runs");
            }

            FinishSmokeTest();
        }

        /// <summary>The window closed (or the app quit) mid-test: write a failing report instead of none.</summary>
        private void AbortSmokeTestOnQuit()
        {
            if (smoke == null || smokeStage == SmokeStage.Finished)
            {
                return;
            }

            SmokeError("the viewer quit in stage " + smokeStage + " before the smoke test finished");
            FinishSmokeTest();
        }

        /// <summary>Writes the report and quits with 0 (passed) or 1.</summary>
        private void FinishSmokeTest()
        {
            string json;
            bool passed;
            lock (smokeLock)
            {
                smoke.Seconds = Time.realtimeSinceStartup - smokeStartedAt;
                passed = smoke.Passed;
                json = smoke.ToJson();
                smokeStage = SmokeStage.Finished;
            }

            if (!string.IsNullOrWhiteSpace(smokeReportPath))
            {
                try
                {
                    string full = Path.GetFullPath(smokeReportPath);
                    string directory = Path.GetDirectoryName(full);
                    if (!string.IsNullOrEmpty(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }
                    File.WriteAllText(full, json, new UTF8Encoding(false));
                }
                catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException ||
                    exception is ArgumentException || exception is NotSupportedException)
                {
                    Debug.LogWarning("Smoke test: could not write the report " + smokeReportPath + ": " + exception.Message);
                    passed = false;
                }
            }

            Debug.Log("Smoke test finished: passed=" + passed + "\n" + json);
            EndSmokeLogCapture();
            Application.Quit(passed ? 0 : 1);
        }
    }
}
