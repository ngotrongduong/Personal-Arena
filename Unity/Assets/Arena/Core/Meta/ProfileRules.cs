using System;
using System.Collections.Generic;
using PersonalArena.Core.Survivor;

namespace PersonalArena.Core.Meta
{
    /// <summary>Input of <see cref="ProfileRules.RecordRun"/>: what one finished run gives the profile.</summary>
    public sealed class RunResult
    {
        public float SurvivedSeconds;
        public EndReason End;
        public float Gold;
        public int Tier = 1;
        /// <summary>True for an Auto Farm run (counted in FarmRuns and in Runs).</summary>
        public bool Farm;
    }

    public sealed class RunReward
    {
        public long GoldAdded;
        public bool TierUnlocked;
        public int UnlockedTier;
    }

    public sealed class LoadoutSummary
    {
        public int Runs;
        public int Wins;
        public float WinRate;
        public float MedianSeconds;
        public float GoldPerMinute;
        public float BestSeconds;
    }

    /// <summary>All out-of-run rules (GDD §3.1–3.5). The Unity UI only draws these and saves the profile.</summary>
    public static class ProfileRules
    {
        public const int LoadoutCount = 5;
        public const int RecentCap = 20;
        public const int MaxTier = 10;
        public const int MaxNameLength = 16;
        public const string WarriorId = "warrior";
        public const string MageId = "mage";
        public const string ArcherId = "archer";
        public const long MagePrice = 1500;
        public const long ArcherPrice = 3000;
        /// <summary>Level cost = floor(LevelCostBase · LevelCostGrowth^level).</summary>
        public const double LevelCostBase = 100.0;
        public const double LevelCostGrowth = 1.25;

        /// <summary>Sum of the stat caps over the used stats (190): the most stat points a character can own.</summary>
        public static readonly int MaxCharacterLevel = ComputeMaxCharacterLevel();

        private static int ComputeMaxCharacterLevel()
        {
            int total = 0;
            for (int i = 0; i < StatInfo.UsedCount; i++) total += StatInfo.Cap((StatId)i);
            return total;
        }

        public static PlayerProfile NewProfile()
        {
            PlayerProfile profile = new PlayerProfile();
            profile.Characters.Add(NewCharacter(WarriorId));
            return profile;
        }

        public static CharacterProfile NewCharacter(string classId)
        {
            CharacterProfile character = new CharacterProfile
            {
                ClassId = classId, Level = 0, ActiveLoadout = 0, BrainRunId = DefaultBrainRunId(classId),
                Loadouts = new Loadout[LoadoutCount]
            };
            for (int i = 0; i < LoadoutCount; i++) character.Loadouts[i] = NewLoadout(i);
            return character;
        }

        public static string DefaultLoadoutName(int index) => "Bộ " + (index + 1);
        public static string DefaultBrainRunId(string classId) => classId + "-s001";

        public static CharacterProfile FindCharacter(PlayerProfile p, string classId)
        {
            if (p == null || p.Characters == null) return null;
            for (int i = 0; i < p.Characters.Count; i++)
            {
                CharacterProfile c = p.Characters[i];
                if (c != null && c.ClassId == classId) return c;
            }
            return null;
        }

        /// <summary>Repairs a loaded profile so every other rule can trust it. Returns true when it changed something.</summary>
        public static bool Sanitize(PlayerProfile p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            bool changed = false;
            if (p.Version < 1) { p.Version = 1; changed = true; }
            if (p.Gold < 0) { p.Gold = 0; changed = true; }
            changed |= Clamp(ref p.UnlockedTier, 1, MaxTier);
            changed |= Clamp(ref p.SelectedTier, 1, p.UnlockedTier);
            string focusId = TrainingFocusInfo.TryParse(p.TrainingFocus, out TrainingFocus focus) ? TrainingFocusInfo.Id(focus) : TrainingFocusInfo.Id(TrainingFocus.Balanced);
            if (p.TrainingFocus != focusId) { p.TrainingFocus = focusId; changed = true; }

            if (p.Characters == null) { p.Characters = new List<CharacterProfile>(); changed = true; }
            for (int i = p.Characters.Count - 1; i >= 0; i--)
            {
                CharacterProfile c = p.Characters[i];
                bool duplicate = false;
                if (c != null && !string.IsNullOrEmpty(c.ClassId))
                    for (int j = 0; j < i; j++) duplicate |= p.Characters[j] != null && p.Characters[j].ClassId == c.ClassId;
                if (c == null || string.IsNullOrEmpty(c.ClassId) || duplicate) { p.Characters.RemoveAt(i); changed = true; }
            }
            if (FindCharacter(p, WarriorId) == null) { p.Characters.Insert(0, NewCharacter(WarriorId)); changed = true; }
            for (int i = 0; i < p.Characters.Count; i++) changed |= SanitizeCharacter(p.Characters[i]);
            if (string.IsNullOrEmpty(p.SelectedClassId) || FindCharacter(p, p.SelectedClassId) == null) { p.SelectedClassId = WarriorId; changed = true; }

            if (p.Stats == null) { p.Stats = new ProfileStats(); changed = true; }
            ProfileStats s = p.Stats;
            changed |= NonNegative(ref s.Runs); changed |= NonNegative(ref s.Wins); changed |= NonNegative(ref s.FarmSessions); changed |= NonNegative(ref s.FarmRuns);
            if (s.GoldEarned < 0) { s.GoldEarned = 0; changed = true; }
            changed |= NonNegative(ref s.BestSeconds);
            return changed;
        }

        private static bool SanitizeCharacter(CharacterProfile c)
        {
            bool changed = Clamp(ref c.Level, 0, MaxCharacterLevel);
            if (string.IsNullOrEmpty(c.BrainRunId)) { c.BrainRunId = DefaultBrainRunId(c.ClassId); changed = true; }
            if (c.Loadouts == null || c.Loadouts.Length != LoadoutCount)
            {
                Loadout[] fixedLoadouts = new Loadout[LoadoutCount];
                if (c.Loadouts != null) Array.Copy(c.Loadouts, fixedLoadouts, Math.Min(LoadoutCount, c.Loadouts.Length));
                c.Loadouts = fixedLoadouts; changed = true;
            }
            for (int i = 0; i < LoadoutCount; i++)
            {
                if (c.Loadouts[i] == null) { c.Loadouts[i] = NewLoadout(i); changed = true; continue; }
                changed |= SanitizeLoadout(c.Loadouts[i], i, c.Level);
            }
            changed |= Clamp(ref c.ActiveLoadout, 0, LoadoutCount - 1);
            return changed;
        }

        private static bool SanitizeLoadout(Loadout l, int index, int level)
        {
            bool changed = false;
            string name = CleanName(l.Name);
            if (name == null) name = DefaultLoadoutName(index);
            if (l.Name != name) { l.Name = name; changed = true; }
            if (l.Points == null || l.Points.Length != StatInfo.SlotCount)
            {
                int[] points = new int[StatInfo.SlotCount];
                if (l.Points != null) Array.Copy(l.Points, points, Math.Min(StatInfo.SlotCount, l.Points.Length));
                l.Points = points; changed = true;
            }
            for (int i = 0; i < StatInfo.SlotCount; i++) changed |= Clamp(ref l.Points[i], 0, StatInfo.Cap((StatId)i));
            int excess = SpentPoints(l) - level;
            for (int i = StatInfo.SlotCount - 1; i >= 0 && excess > 0; i--)
            {
                int removed = Math.Min(excess, l.Points[i]);
                if (removed > 0) { l.Points[i] -= removed; excess -= removed; changed = true; }
            }
            if (l.Record == null) { l.Record = new LoadoutRecord(); changed = true; }
            changed |= SanitizeRecord(l.Record);
            return changed;
        }

        private static bool SanitizeRecord(LoadoutRecord r)
        {
            bool changed = NonNegative(ref r.Runs);
            changed |= Clamp(ref r.Wins, 0, r.Runs);
            if (r.TotalGold < 0) { r.TotalGold = 0; changed = true; }
            changed |= NonNegative(ref r.TotalMinutes);
            changed |= NonNegative(ref r.BestSeconds);
            if (r.RecentSeconds == null) { r.RecentSeconds = new List<float>(); changed = true; }
            for (int i = r.RecentSeconds.Count - 1; i >= 0; i--)
            {
                float value = r.RecentSeconds[i];
                if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f) { r.RecentSeconds.RemoveAt(i); changed = true; }
            }
            if (r.RecentSeconds.Count > RecentCap) { r.RecentSeconds.RemoveRange(0, r.RecentSeconds.Count - RecentCap); changed = true; }
            return changed;
        }

        /// <summary>Gold price of the next character level: floor(100 · 1.25^currentLevel), saturating at long.MaxValue.</summary>
        public static long LevelCost(int currentLevel)
        {
            if (currentLevel < 0) throw new ArgumentOutOfRangeException(nameof(currentLevel));
            double cost = Math.Floor(LevelCostBase * Math.Pow(LevelCostGrowth, currentLevel));
            return cost >= long.MaxValue ? long.MaxValue : (long)cost;
        }

        public static bool TryBuyLevel(PlayerProfile p, CharacterProfile c)
        {
            if (p == null || c == null || c.Level < 0 || c.Level >= MaxCharacterLevel) return false;
            long cost = LevelCost(c.Level);
            if (p.Gold < cost) return false;
            p.Gold -= cost; c.Level++;
            return true;
        }

        public static int SpentPoints(Loadout l)
        {
            if (l == null || l.Points == null) return 0;
            int total = 0;
            for (int i = 0; i < l.Points.Length; i++) total += l.Points[i];
            return total;
        }

        /// <summary>Points the character owns but has not spent in that loadout (0 for an invalid index).</summary>
        public static int UnspentPoints(CharacterProfile c, int loadout)
        {
            Loadout l = GetLoadout(c, loadout);
            return l == null ? 0 : Math.Max(0, c.Level - SpentPoints(l));
        }

        public static bool TryAddPoint(CharacterProfile c, int loadout, StatId stat)
        {
            Loadout l = GetLoadout(c, loadout);
            int index = (int)stat;
            if (l == null || l.Points == null || index < 0 || index >= StatInfo.UsedCount || index >= l.Points.Length) return false;
            if (l.Points[index] >= StatInfo.Cap(stat) || UnspentPoints(c, loadout) <= 0) return false;
            l.Points[index]++;
            return true;
        }

        public static bool TryRemovePoint(CharacterProfile c, int loadout, StatId stat)
        {
            Loadout l = GetLoadout(c, loadout);
            int index = (int)stat;
            if (l == null || l.Points == null || index < 0 || index >= l.Points.Length || l.Points[index] <= 0) return false;
            l.Points[index]--;
            return true;
        }

        /// <summary>Returns every point of the loadout (free).</summary>
        public static void ResetPoints(CharacterProfile c, int loadout)
        {
            Loadout l = GetLoadout(c, loadout);
            if (l == null) return;
            if (l.Points == null || l.Points.Length != StatInfo.SlotCount) l.Points = new int[StatInfo.SlotCount];
            else Array.Clear(l.Points, 0, l.Points.Length);
        }

        public static bool TrySetActiveLoadout(CharacterProfile c, int index)
        {
            if (c == null || index < 0 || index >= LoadoutCount) return false;
            c.ActiveLoadout = index;
            return true;
        }

        /// <summary>Renames a loadout; the name is trimmed and must be 1..16 characters.</summary>
        public static bool TryRename(CharacterProfile c, int index, string name)
        {
            Loadout l = GetLoadout(c, index);
            if (l == null || name == null) return false;
            string trimmed = name.Trim();
            if (trimmed.Length < 1 || trimmed.Length > MaxNameLength) return false;
            l.Name = trimmed;
            return true;
        }

        /// <summary>The run build of the active loadout at <paramref name="tier"/> (clamped 1..10); always passes Validate.</summary>
        public static CharacterBuild ToBuild(CharacterProfile c, int tier)
        {
            if (c == null) throw new ArgumentNullException(nameof(c));
            CharacterBuild build = new CharacterBuild { Tier = Math.Max(1, Math.Min(MaxTier, tier)) };
            Loadout l = GetLoadout(c, c.ActiveLoadout);
            if (l != null && l.Points != null)
                for (int i = 0; i < StatInfo.SlotCount && i < l.Points.Length; i++)
                    build.Points[i] = Math.Max(0, Math.Min(StatInfo.Cap((StatId)i), l.Points[i]));
            build.Validate();
            return build;
        }

        /// <summary>Books a finished run: wallet, loadout record, profile stats and the tier unlock.</summary>
        public static RunReward RecordRun(PlayerProfile p, CharacterProfile c, int loadout, RunResult r)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            if (r == null) throw new ArgumentNullException(nameof(r));
            Loadout l = GetLoadout(c, loadout);
            if (l == null) throw new ArgumentOutOfRangeException(nameof(loadout));
            if (l.Record == null) l.Record = new LoadoutRecord();
            if (p.Stats == null) p.Stats = new ProfileStats();
            long gold = float.IsNaN(r.Gold) || r.Gold <= 0f ? 0L
                : r.Gold >= MaxRunGold ? MaxRunGold : (long)Math.Floor(r.Gold);
            float seconds = float.IsNaN(r.SurvivedSeconds) || r.SurvivedSeconds < 0f ? 0f : r.SurvivedSeconds;
            bool won = r.End == EndReason.Won;
            p.Gold = SaturatingAdd(p.Gold, gold);

            LoadoutRecord record = l.Record;
            record.Runs++; if (won) record.Wins++;
            record.TotalGold = SaturatingAdd(record.TotalGold, gold); record.TotalMinutes += seconds / 60f;
            if (seconds > record.BestSeconds) record.BestSeconds = seconds;
            if (record.RecentSeconds == null) record.RecentSeconds = new List<float>();
            record.RecentSeconds.Add(seconds);
            if (record.RecentSeconds.Count > RecentCap) record.RecentSeconds.RemoveRange(0, record.RecentSeconds.Count - RecentCap);

            ProfileStats stats = p.Stats;
            stats.Runs++; if (won) stats.Wins++;
            if (r.Farm) stats.FarmRuns++;
            stats.GoldEarned = SaturatingAdd(stats.GoldEarned, gold);
            if (seconds > stats.BestSeconds) stats.BestSeconds = seconds;

            RunReward reward = new RunReward { GoldAdded = gold };
            if (won && r.Tier == p.UnlockedTier && p.UnlockedTier < MaxTier) { p.UnlockedTier++; reward.TierUnlocked = true; }
            reward.UnlockedTier = p.UnlockedTier;
            return reward;
        }

        /// <summary>Upper bound for one run's gold, so a broken value (+∞, huge) can never overflow the wallet.</summary>
        public const long MaxRunGold = 1_000_000_000L;

        private static long SaturatingAdd(long a, long b) => b > 0 && a > long.MaxValue - b ? long.MaxValue : a + b;

        /// <summary>Counts one finished Auto Farm session (its runs are booked one by one with RecordRun).</summary>
        public static void RecordFarmSession(PlayerProfile p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            if (p.Stats == null) p.Stats = new ProfileStats();
            p.Stats.FarmSessions++;
        }

        public static LoadoutSummary Summarize(Loadout l)
        {
            LoadoutSummary summary = new LoadoutSummary();
            if (l == null || l.Record == null) return summary;
            LoadoutRecord r = l.Record;
            summary.Runs = r.Runs; summary.Wins = r.Wins; summary.BestSeconds = r.BestSeconds;
            summary.WinRate = r.Runs > 0 ? (float)r.Wins / r.Runs : 0f;
            summary.GoldPerMinute = r.TotalMinutes > 0f ? (float)(r.TotalGold / (double)r.TotalMinutes) : 0f;
            if (r.RecentSeconds != null && r.RecentSeconds.Count > 0)
            {
                float[] sorted = r.RecentSeconds.ToArray();
                Array.Sort(sorted);
                int mid = sorted.Length / 2;
                summary.MedianSeconds = sorted.Length % 2 == 0 ? (sorted[mid - 1] + sorted[mid]) * 0.5f : sorted[mid];
            }
            return summary;
        }

        /// <summary>Shop price of a class: warrior 0, mage 1500, archer 3000, unknown −1.</summary>
        public static long ClassPrice(string classId)
        {
            switch (classId)
            {
                case WarriorId: return 0;
                case MageId: return MagePrice;
                case ArcherId: return ArcherPrice;
                default: return -1;
            }
        }

        /// <summary>Only the Warrior is playable in M5; the UI shows the others as "sắp có".</summary>
        public static bool IsClassPlayable(string classId) => classId == WarriorId;

        private static Loadout NewLoadout(int index) => new Loadout { Name = DefaultLoadoutName(index), Points = new int[StatInfo.SlotCount], Record = new LoadoutRecord() };

        private static Loadout GetLoadout(CharacterProfile c, int index)
        {
            if (c == null || c.Loadouts == null || index < 0 || index >= c.Loadouts.Length) return null;
            return c.Loadouts[index];
        }

        /// <summary>Trimmed name cut to 16 characters, or null when empty.</summary>
        private static string CleanName(string name)
        {
            if (name == null) return null;
            string trimmed = name.Trim();
            if (trimmed.Length == 0) return null;
            return trimmed.Length > MaxNameLength ? trimmed.Substring(0, MaxNameLength).TrimEnd() : trimmed;
        }

        private static bool Clamp(ref int value, int min, int max)
        {
            int clamped = value < min ? min : value > max ? max : value;
            if (clamped == value) return false;
            value = clamped; return true;
        }

        private static bool NonNegative(ref int value) { if (value >= 0) return false; value = 0; return true; }

        private static bool NonNegative(ref float value)
        {
            if (!float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f) return false;
            value = 0f; return true;
        }
    }
}
