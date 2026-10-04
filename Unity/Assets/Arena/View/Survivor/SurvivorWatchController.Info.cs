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
            // The card's heading in gold; the lines under it stay short so the card stays small.
            string title = "<b><color=#FFDB73>" + ClassViewLogic.AiTitle(classId) + "</color></b>";
            const string dot = "  ·  ";
            if (brain == null)
            {
                text = title + "\nNo brain yet";
            }
            else
            {
                string steps = LineageStore.FormatStep(brain.Step) + " steps";
                if (loadedIsVersion && watchedVersion != null)
                {
                    text = title + "   " + watchedVersion.Name + " (" + watchedBranchName + ")" +
                        "\n" + steps + dot + "saved version" + dot + "B: newest brain";
                }
                else if (loadedIsChampion)
                {
                    text = title + "   BEST BRAIN" +
                        (loadedChampion != null ? "   " + loadedChampion.run_id : string.Empty) +
                        "\n" + steps +
                        (loadedChampion != null ? dot + (loadedChampion.passes_m4a ? "M4A met" : "M4A not met") : string.Empty) +
                        dot + "B: newest brain";
                }
                else
                {
                    text = title + "   " + BrainLocator.RunName(loadedPath) +
                        "\n" + steps + dot + "newest brain, loaded " + loadedAt.ToString("HH:mm", culture) + dot + "B: best brain";
                }
                if (!string.IsNullOrEmpty(brainStatus))
                {
                    text += "\n" + brainStatus;
                }
            }

            text += "\nRun " + run + dot + "Speed x" + SpeedSteps[speedIndex] +
                dot + "Picks: " + (pilot.Deterministic ? "best" : "random");
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

                text += "\nLast " + recent.Count + (recent.Count == 1 ? " run: " : " runs: ") + SurvivorViewLogic.FormatClock(time / recent.Count) +
                    dot + "Lv " + (level / recent.Count).ToString("0.0", culture) +
                    dot + (gold / recent.Count).ToString("0", culture) + " gold";
            }
            else
            {
                text += "\nNo run finished yet";
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
