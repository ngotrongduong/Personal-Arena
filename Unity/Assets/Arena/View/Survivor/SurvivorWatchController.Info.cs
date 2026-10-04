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
    /// <summary>Run results into the profile, and the info text.</summary>
    public sealed partial class SurvivorWatchController
    {
        private void RecordResultOnce()
        {
            if (resultRecorded)
            {
                return;
            }

            resultRecorded = true;
            if (survivorAudio != null)
            {
                survivorAudio.OnRunEnded(sim.EndReason);
                if (!string.IsNullOrEmpty(screenshotPath))
                {
                    // Screenshot mode may quit soon after; keep the counts of the finished run.
                    survivorAudio.WriteAudioLog();
                }
            }

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
                CharacterProfile warrior = CurrentCharacter;
                long goldBefore = store.Profile.Gold;
                RunReward reward = ProfileRules.RecordRun(store.Profile, warrior, runLoadout,
                    MetaViewLogic.ToRunResult(sim, runBuild.Tier, false));
                bool saved = store.Save();
                if (smoke != null)
                {
                    SmokeRunBooked(warrior != null ? warrior.ClassId : classId, goldBefore, reward, saved);
                }
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
            string title = ClassViewLogic.AiTitle(classId);
            if (brain == null)
            {
                text = title + "\nNo brain yet";
            }
            else
            {
                if (loadedIsVersion && watchedVersion != null)
                {
                    text = title + "\nBrain: " + watchedVersion.Name + " (" + watchedBranchName + ")" +
                        "\nTrained " + brain.Step.ToString("N0", culture) + " steps" +
                        "   Saved version   (B: newest brain)";
                }
                else if (loadedIsChampion)
                {
                    text = title + "   BEST BRAIN" +
                        (loadedChampion != null ? "   " + loadedChampion.run_id : string.Empty) +
                        "\nTrained " + brain.Step.ToString("N0", culture) + " steps" +
                        (loadedChampion != null ? (loadedChampion.passes_m4a ? "   M4A met" : "   M4A not met") : string.Empty) +
                        "\nReplaced when a brain scores higher   (B: newest brain)";
                }
                else
                {
                    text = title + "   " + BrainLocator.RunName(loadedPath) +
                        "\nTrained " + brain.Step.ToString("N0", culture) + " steps" +
                        "   (loaded at " + loadedAt.ToString("HH:mm", culture) + ")" +
                        "\nNewest brain, updates itself   (B: best brain)";
                }
                if (!string.IsNullOrEmpty(brainStatus))
                {
                    text += "\n" + brainStatus;
                }
            }

            text += "\nRun " + run + "   Speed x" + SpeedSteps[speedIndex] +
                "   Picks: " + (pilot.Deterministic ? "best" : "random");
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

                text += "\n" + recent.Count + " latest runs (avg): survival " + SurvivorViewLogic.FormatClock(time / recent.Count) +
                    ", level " + (level / recent.Count).ToString("0.0", culture) +
                    ", gold " + (gold / recent.Count).ToString("0", culture);
            }
            else
            {
                text += "\nAverage of the last 10 runs: no run finished yet";
            }

            // M5: wallet, a build change waiting for the next run, and the Auto Farm progress.
            if (store != null)
            {
                text += "\n" + MetaViewLogic.WalletText(store.Profile);
                if (IsBuildChangePending())
                {
                    text += "\nBuild change: applies from the next run";
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
    }
}
