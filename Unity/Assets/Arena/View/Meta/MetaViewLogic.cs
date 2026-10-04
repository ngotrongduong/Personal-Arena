using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using PersonalArena.Core.Meta;
using PersonalArena.Core.Survivor;

namespace PersonalArena.View
{
    /// <summary>Summary of one finished Auto Farm session, booked into the profile.</summary>
    public sealed class FarmSummary
    {
        public int Runs;
        public int Wins;
        public long Gold;
        public float AverageSeconds;
        public bool Cancelled;
        /// <summary>Message of the error that stopped the session, or null.</summary>
        public string Error;
        public bool TierUnlocked;
        public int UnlockedTier;
    }

    /// <summary>
    /// Pure text and glue helpers of the out-of-run (M5) screens. All rules come from Core
    /// (<see cref="ProfileRules"/>, <see cref="StatInfo"/>, <see cref="TierModifiers"/>); this only formats and maps.
    /// </summary>
    public static class MetaViewLogic
    {
        private static readonly string[] StatNames =
        {
            "Max HP", "Armor", "Regen", "Might", "Crit", "Crit damage", "Cooldown",
            "Area", "Speed", "Magnet", "Luck", "Greed", "Growth"
        };

        private static readonly string[] StatShortNames =
        {
            "HP", "Armor", "Regen", "Might", "Crit", "Crit damage", "Cooldown",
            "Area", "Speed", "Magnet", "Luck", "Greed", "Growth"
        };

        /// <summary>What one point changes; "{0}" is the signed amount.</summary>
        private static readonly string[] StatEffects =
        {
            "{0}% max HP / point", "{0} armor / point", "{0} HP per second / point", "{0}% damage / point",
            "{0}% crit chance / point", "{0}% crit damage / point", "{0}% cooldown / point",
            "{0}% attack area / point", "{0}% move speed / point", "{0}% pickup radius / point", "{0} luck / point",
            "{0}% gold / point", "{0}% EXP / point"
        };

        /// <summary>Stats whose per-point value is a fraction shown as a percentage.</summary>
        private static readonly bool[] StatIsPercent =
        {
            true, false, false, true, true, true, true, true, true, true, false, true, true
        };

        // ------------------------------------------------------------------ run results

        /// <summary>Stats of a finished (or stopped) viewer run, in the same shape as the evaluator's.</summary>
        public static SurvivorRunStats ToRunStats(SurvivorSim sim, int seed)
        {
            if (sim == null)
            {
                throw new ArgumentNullException(nameof(sim));
            }

            SurvivorRunStats stats = new SurvivorRunStats
            {
                Seed = seed,
                SurvivedSeconds = sim.Time,
                EndReason = sim.EndReason,
                DeathCause = sim.DeathCause,
                Level = sim.Level,
                Kills = sim.Kills,
                EliteKills = sim.EliteKills,
                Gold = sim.Gold,
                TotalXp = sim.TotalXp,
                DamageTaken = sim.DamageTaken,
                DamageDealt = sim.DamageDealtTotal,
                BossDamageFraction = sim.BossDamageFraction,
                MinHpRatio = sim.MinHpRatio,
                MinHpTime = sim.MinHpTime
            };
            Array.Copy(sim.SkillUses, stats.SkillUses, Math.Min(sim.SkillUses.Length, stats.SkillUses.Length));
            for (int i = 0; i < stats.FinalItemLevels.Length; i++)
            {
                stats.FinalItemLevels[i] = sim.Inventory.Level(i);
            }
            for (int i = 0; i < stats.GoldBySource.Length; i++)
            {
                stats.GoldBySource[i] = sim.GetGold((GoldSource)i);
            }

            return stats;
        }

        /// <summary>The watch reward input of a finished viewer run played at <paramref name="tier"/>.</summary>
        public static RunResult ToRunResult(SurvivorSim sim, int tier, bool farm)
        {
            if (sim == null)
            {
                throw new ArgumentNullException(nameof(sim));
            }

            return new RunResult { SurvivedSeconds = sim.Time, End = sim.EndReason, Gold = sim.Gold, Tier = tier, Farm = farm };
        }

        /// <summary>The reward input of a finished Auto Farm match.</summary>
        public static RunResult ToRunResult(SurvivorRunStats stats, int tier, bool farm)
        {
            if (stats == null)
            {
                throw new ArgumentNullException(nameof(stats));
            }

            return new RunResult { SurvivedSeconds = stats.SurvivedSeconds, End = stats.EndReason, Gold = stats.Gold, Tier = tier, Farm = farm };
        }

        /// <summary>End-screen reward text: "+N vàng vào ví" and "Mở khóa bậc N!".</summary>
        public static string RewardText(RunReward reward)
        {
            if (reward == null)
            {
                return "This run earns no gold (the AI did not play).";
            }

            string text = "+" + FormatGold(reward.GoldAdded) + " gold to wallet";
            if (reward.TierUnlocked)
            {
                text += "\nUnlocked tier " + reward.UnlockedTier + "!";
            }

            return text;
        }

        // ------------------------------------------------------------------ training with the owner's choices

        /// <summary>
        /// TRAIN uses the owner's build only once it means something: the selected class's character has a level
        /// or a tier above 1 is selected. A level-0, tier-1 owner keeps the random-build curriculum.
        /// </summary>
        public static bool UsesOwnerBuild(PlayerProfile profile)
        {
            if (profile == null)
            {
                return false;
            }

            CharacterProfile character = ClassViewLogic.SelectedCharacter(profile);
            return (character != null && character.Level >= 1) || profile.SelectedTier > 1;
        }

        /// <summary>What StartTraining passes: points and tier per <see cref="UsesOwnerBuild"/>, the focus id always.</summary>
        public static OwnerTraining OwnerTrainingFor(PlayerProfile profile)
        {
            OwnerTraining owner = new OwnerTraining { FocusId = FocusId(profile != null ? profile.TrainingFocus : null) };
            if (!UsesOwnerBuild(profile))
            {
                return owner;
            }

            CharacterProfile character = ClassViewLogic.SelectedCharacter(profile);
            if (character != null)
            {
                CharacterBuild build = ProfileRules.ToBuild(character, profile.SelectedTier);
                owner.Points = (int[])build.Points.Clone();
                owner.Tier = build.Tier;
            }
            else
            {
                owner.Points = new int[StatInfo.SlotCount];
                owner.Tier = Math.Max(1, Math.Min(ProfileRules.MaxTier, profile.SelectedTier));
            }

            return owner;
        }

        /// <summary>"Học theo build của bạn (bậc N) · Trọng tâm: X" or "Học build ngẫu nhiên · Trọng tâm: X".</summary>
        public static string TrainingBuildLine(bool ownerBuild, int tier, string focusId)
        {
            string focus = " · Focus: " + FocusName(focusId);
            return ownerBuild
                ? "Learning your build (tier " + Math.Max(1, tier) + ")" + focus
                : "Learning random builds" + focus;
        }

        /// <summary>
        /// Whether the owner's current choices differ from the running training: the service reports owner build,
        /// tier and focus; the points are compared with what this viewer passed when it started the session.
        /// </summary>
        public static bool TrainingChoicesDiffer(bool reportedOwnerBuild, int reportedTier, string reportedFocus,
            OwnerTraining started, OwnerTraining current)
        {
            if (current == null)
            {
                return false;
            }

            bool currentOwner = current.Points != null;
            if (currentOwner != reportedOwnerBuild)
            {
                return true;
            }
            if (currentOwner && Math.Max(1, reportedTier) != current.Tier)
            {
                return true;
            }
            if (FocusId(reportedFocus) != FocusId(current.FocusId))
            {
                return true;
            }
            if (currentOwner && started != null && started.Points != null && !SamePoints(started.Points, current.Points))
            {
                return true;
            }

            return false;
        }

        private static bool SamePoints(int[] a, int[] b)
        {
            if (a.Length != b.Length)
            {
                return false;
            }
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }
            return true;
        }

        // ------------------------------------------------------------------ stats, tiers, focus

        public static string StatName(StatId stat)
        {
            int index = (int)stat;
            return index >= 0 && index < StatNames.Length ? StatNames[index] : string.Empty;
        }

        public static string StatShortName(StatId stat)
        {
            int index = (int)stat;
            return index >= 0 && index < StatShortNames.Length ? StatShortNames[index] : string.Empty;
        }

        /// <summary>Effect of one point from <see cref="StatInfo.PerPoint"/>, e.g. "+5% máu tối đa / điểm".</summary>
        public static string StatEffect(StatId stat)
        {
            int index = (int)stat;
            if (index < 0 || index >= StatEffects.Length)
            {
                return string.Empty;
            }

            float perPoint = StatInfo.PerPoint(stat);
            float shown = StatIsPercent[index] ? perPoint * 100f : perPoint;
            string amount = (shown < 0f ? "-" : "+") + FormatDecimal(Math.Abs(shown), "0.##");
            return string.Format(CultureInfo.InvariantCulture, StatEffects[index], amount);
        }

        /// <summary>Points per main stat, largest first, e.g. "Máu 10 · Mạnh 5 · Chí mạng 5".</summary>
        public static string CompactPoints(int[] points, int maxParts = 4)
        {
            if (points == null)
            {
                return "no points spent";
            }

            List<int> stats = new List<int>();
            for (int i = 0; i < StatInfo.UsedCount && i < points.Length; i++)
            {
                if (points[i] > 0)
                {
                    stats.Add(i);
                }
            }
            if (stats.Count == 0)
            {
                return "no points spent";
            }

            stats.Sort((a, b) => points[a] != points[b] ? points[b].CompareTo(points[a]) : a.CompareTo(b));
            StringBuilder text = new StringBuilder();
            int shown = Math.Min(Math.Max(1, maxParts), stats.Count);
            for (int i = 0; i < shown; i++)
            {
                if (i > 0)
                {
                    text.Append(" · ");
                }
                text.Append(StatShortName((StatId)stats[i])).Append(' ').Append(points[stats[i]]);
            }
            if (stats.Count > shown)
            {
                text.Append(" · +").Append(stats.Count - shown).Append(" stats");
            }

            return text.ToString();
        }

        /// <summary>Gold multiplier of a tier (GDD §3.5): 1 + 0,5·(N−1).</summary>
        public static float TierGoldMultiplier(int tier)
        {
            return 1f + 0.5f * (Math.Max(1, Math.Min(ProfileRules.MaxTier, tier)) - 1);
        }

        /// <summary>"Vàng ×1,5  (1 + 0,5·(2−1))".</summary>
        public static string TierGoldText(int tier)
        {
            int clamped = Math.Max(1, Math.Min(ProfileRules.MaxTier, tier));
            return "Gold ×" + FormatDecimal(TierGoldMultiplier(clamped), "0.0") + "  (1 + 0,5·(" + clamped + "−1))";
        }

        /// <summary>The rule changes of a tier, one per line ("Base rules, no changes" at tier 1).</summary>
        public static string TierRulesText(int tier)
        {
            StringBuilder text = new StringBuilder();
            foreach (TierModifier modifier in TierModifiers.For(tier))
            {
                if (text.Length > 0)
                {
                    text.Append('\n');
                }
                text.Append("• ").Append(TierModifiers.DisplayName(modifier));
            }

            return text.Length > 0 ? text.ToString() : "• Base rules, no changes";
        }

        /// <summary>The normalized focus id ("balanced" for null or unknown).</summary>
        public static string FocusId(string id)
        {
            TrainingFocusInfo.TryParse(id, out TrainingFocus focus);
            return TrainingFocusInfo.Id(focus);
        }

        public static string FocusName(string id)
        {
            TrainingFocusInfo.TryParse(id, out TrainingFocus focus);
            return TrainingFocusInfo.DisplayName(focus);
        }

        /// <summary>One line on what the focus changes during training (GDD §3.4).</summary>
        public static string FocusHint(TrainingFocus focus)
        {
            switch (focus)
            {
                case TrainingFocus.Survival: return "The AI is punished more for losing HP and plays safer";
                case TrainingFocus.Gold: return "The AI is rewarded more for picking up gold";
                case TrainingFocus.Boss: return "The AI is rewarded more for damaging the Boss";
                case TrainingFocus.Offense: return "The AI is rewarded more for kills and level-ups, and accepts losing HP";
                default: return "Default rewards, no goal favoured";
            }
        }

        // ------------------------------------------------------------------ wallet, loadouts

        /// <summary>Two wallet lines: "Ví: N vàng   Bậc N" and "Bộ: &lt;name&gt;   Trọng tâm: &lt;focus&gt;".</summary>
        public static string WalletText(PlayerProfile profile)
        {
            if (profile == null)
            {
                return string.Empty;
            }

            CharacterProfile character = ClassViewLogic.SelectedCharacter(profile);
            string loadout = LoadoutName(character, character != null ? character.ActiveLoadout : 0);
            return "Wallet: " + FormatGold(profile.Gold) + " gold   Tier " + profile.SelectedTier +
                (character != null ? "   " + ClassViewLogic.DisplayName(character.ClassId) + " level " + character.Level : string.Empty) +
                "\nLoadout: " + loadout + "   Focus: " + FocusName(profile.TrainingFocus);
        }

        public static string LoadoutName(CharacterProfile character, int index)
        {
            if (character == null || character.Loadouts == null || index < 0 || index >= character.Loadouts.Length ||
                character.Loadouts[index] == null || string.IsNullOrEmpty(character.Loadouts[index].Name))
            {
                return ProfileRules.DefaultLoadoutName(Math.Max(0, index));
            }

            return character.Loadouts[index].Name;
        }

        /// <summary>True when both builds have the same tier and stat points (the run would play the same).</summary>
        public static bool SameBuild(CharacterBuild a, CharacterBuild b)
        {
            if (a == null || b == null)
            {
                return a == b;
            }

            return a.Tier == b.Tier && SamePoints(a.Points, b.Points);
        }

        // ------------------------------------------------------------------ numbers

        /// <summary>Whole gold with dot thousands separators, e.g. "12.345".</summary>
        public static string FormatGold(long gold)
        {
            return gold.ToString("N0", CultureInfo.InvariantCulture);
        }

        /// <summary>A decimal number with a decimal point, whatever the machine's culture.</summary>
        public static string FormatDecimal(float value, string format)
        {
            return value.ToString(format, CultureInfo.InvariantCulture);
        }

        public static string Percent(float ratio)
        {
            return Math.Round(ratio * 100f).ToString("0", CultureInfo.InvariantCulture) + "%";
        }

        /// <summary>"3 phút 20 giây" / "45 giây".</summary>
        public static string FormatDuration(float seconds)
        {
            int whole = Math.Max(0, (int)Math.Round(seconds));
            if (whole >= 3600)
            {
                return (whole / 3600) + "h " + (whole % 3600 / 60).ToString("00", CultureInfo.InvariantCulture) + "m";
            }

            return whole >= 60 ? (whole / 60) + "m " + (whole % 60).ToString("00", CultureInfo.InvariantCulture) + "s" : whole + "s";
        }

        /// <summary>Summary shown when an Auto Farm session ends.</summary>
        public static string FarmSummaryText(FarmSummary summary)
        {
            if (summary == null)
            {
                return string.Empty;
            }

            string text = summary.Runs > 0
                ? "Finished " + summary.Runs + " runs: won " + summary.Wins + " · +" + FormatGold(summary.Gold) + " gold to wallet" +
                  " · avg survival " + SurvivorViewLogic.FormatClock(summary.AverageSeconds)
                : "No run finished, no gold earned.";
            if (summary.TierUnlocked)
            {
                text += "\nUnlocked tier " + summary.UnlockedTier + "!";
            }
            if (summary.Cancelled && string.IsNullOrEmpty(summary.Error))
            {
                text += "\nStopped early; finished runs still count.";
            }
            if (!string.IsNullOrEmpty(summary.Error))
            {
                text += "\nFarm stopped: " + summary.Error;
            }

            return text;
        }
    }
}
