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
                text = title + "\nChưa có bộ não";
            }
            else
            {
                if (loadedIsVersion && watchedVersion != null)
                {
                    text = title + "\nNão: " + watchedVersion.Name + " (" + watchedBranchName + ")" +
                        "\nĐã học " + brain.Step.ToString("N0", culture) + " bước" +
                        "   Phiên bản đã lưu   (B: não mới nhất)";
                }
                else if (loadedIsChampion)
                {
                    text = title + "   NÃO GIỎI NHẤT" +
                        (loadedChampion != null ? "   " + loadedChampion.run_id : string.Empty) +
                        "\nĐã học " + brain.Step.ToString("N0", culture) + " bước" +
                        (loadedChampion != null ? (loadedChampion.passes_m4a ? "   đạt M4A" : "   chưa đạt M4A") : string.Empty) +
                        "\nĐổi khi có não chấm điểm cao hơn   (B: não mới nhất)";
                }
                else
                {
                    text = title + "   " + BrainLocator.RunName(loadedPath) +
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
    }
}
