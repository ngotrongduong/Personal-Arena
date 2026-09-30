using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace PersonalArena.View
{
    /// <summary>Behavior profile written by Tools/SurvivorEval (fractions 0..1, -1 = no data).</summary>
    [Serializable]
    public sealed class ChampionBehavior
    {
        public float Aggression = -1f;
        public float Caution = -1f;
        public float Greed = -1f;
        public float Exploration = -1f;
        public float CrowdControl = -1f;
        public float BossHunting = -1f;
        public float SkillDiscipline = -1f;
        public float PreferredRange = -1f;
        public float KeepDistance = -1f;
        public int KickUses;
        public int BlockUses;
        public int DashUses;
        public int EffectiveKicks;
        public int EffectiveBlocks;
        public int EffectiveDashes;
    }

    /// <summary>100-seed evaluation summary (PascalCase keys, as SurvivorEval writes them).</summary>
    [Serializable]
    public sealed class ChampionSummary
    {
        public int Runs;
        public float MedianSurvivedSeconds;
        public float P10SurvivedSeconds;
        public float MeanSurvivedSeconds;
        public float WinRate;
        public int CatastrophicCount;
        public float GoldPerMinute;
        public float XpPerMinute;
        public float DamageTakenPerMinute;
        public float MeanBossDamageFraction;
        public float MeanLevel;
    }

    /// <summary>One brain's evaluation: champion.json, history/*.json or latest_eval.json.</summary>
    [Serializable]
    public sealed class ChampionRecord
    {
        public string run_id;
        public long step;
        public float score;
        public bool passes_m4a;
        public bool won;
        public bool restored;
        public string evaluated_at;
        public string time;
        public ChampionSummary summary;
        public ChampionBehavior behavior;

        /// <summary>Death causes over the evaluated runs (None excluded), parsed separately because JsonUtility has no dictionaries.</summary>
        [NonSerialized] public Dictionary<string, int> DeathCauses = new Dictionary<string, int>();

        public string When => string.IsNullOrEmpty(evaluated_at) ? time : evaluated_at;

        public bool SameBrain(ChampionRecord other)
        {
            return other != null && other.step == step && string.Equals(other.run_id, run_id, StringComparison.Ordinal);
        }
    }

    /// <summary>What the trainer knows about the best brain of one behavior (Trainer/runs/champions/&lt;Behavior&gt;).</summary>
    public sealed class ChampionInfo
    {
        public const float TargetMedianSeconds = 600f;
        public const float TargetP10Seconds = 420f;

        public ChampionRecord Champion { get; private set; }
        public ChampionRecord LastEvaluation { get; private set; }
        public ChampionRecord PreviousChampion { get; private set; }

        public bool HasChampion => Champion != null;

        private static readonly string[] DeathCauseNames = { "None", "Surrounded", "Boss", "Brute", "Contact", "Projectile" };
        private static readonly Regex DeathCauseBlock = new Regex("\"DeathCauseCounts\"\\s*:\\s*\\{([^}]*)\\}", RegexOptions.Compiled);
        private static readonly Regex CountEntry = new Regex("\"([^\"]+)\"\\s*:\\s*(-?\\d+)", RegexOptions.Compiled);

        /// <summary>Loads champion.json, latest_eval.json and the previous champion from history. Never throws.</summary>
        public static ChampionInfo Load(string runsDirectory, string behavior)
        {
            ChampionInfo info = new ChampionInfo();
            string directory = BrainLocator.ChampionDirectory(runsDirectory, behavior);
            if (directory == null || !Directory.Exists(directory))
            {
                return info;
            }

            info.Champion = ReadRecord(Path.Combine(directory, "champion.json"));
            info.LastEvaluation = ReadRecord(Path.Combine(directory, "latest_eval.json"));
            info.PreviousChampion = FindPrevious(Path.Combine(directory, "history"), info.Champion);
            return info;
        }

        /// <summary>Parses one record; null when the text is missing or not a record.</summary>
        public static ChampionRecord ParseRecord(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            ChampionRecord record;
            try
            {
                record = JsonUtility.FromJson<ChampionRecord>(json);
            }
            catch (ArgumentException)
            {
                return null;
            }

            if (record == null || string.IsNullOrEmpty(record.run_id) || record.summary == null || record.summary.Runs <= 0)
            {
                return null;
            }

            record.behavior = record.behavior ?? new ChampionBehavior();
            record.DeathCauses = ParseDeathCauses(json);
            return record;
        }

        /// <summary>Death-cause counts keyed by DeathCause name; accepts names or enum numbers as keys.</summary>
        public static Dictionary<string, int> ParseDeathCauses(string json)
        {
            Dictionary<string, int> counts = new Dictionary<string, int>();
            if (string.IsNullOrEmpty(json))
            {
                return counts;
            }

            Match block = DeathCauseBlock.Match(json);
            if (!block.Success)
            {
                return counts;
            }

            foreach (Match entry in CountEntry.Matches(block.Groups[1].Value))
            {
                string name = entry.Groups[1].Value;
                if (int.TryParse(name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index))
                {
                    name = index >= 0 && index < DeathCauseNames.Length ? DeathCauseNames[index] : name;
                }

                int count = int.Parse(entry.Groups[2].Value, CultureInfo.InvariantCulture);
                if (name == "None" || count <= 0)
                {
                    continue;
                }

                counts.TryGetValue(name, out int existing);
                counts[name] = existing + count;
            }

            return counts;
        }

        private static ChampionRecord ReadRecord(string path)
        {
            try
            {
                return File.Exists(path) ? ParseRecord(File.ReadAllText(path)) : null;
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        /// <summary>Newest history entry (by write time) that is not the current champion.</summary>
        private static ChampionRecord FindPrevious(string historyDirectory, ChampionRecord current)
        {
            if (current == null || !Directory.Exists(historyDirectory))
            {
                return null;
            }

            string[] files;
            try
            {
                files = Directory.GetFiles(historyDirectory, "*.json");
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }

            ChampionRecord best = null;
            DateTime bestTime = DateTime.MinValue;
            for (int i = 0; i < files.Length; i++)
            {
                ChampionRecord record = ReadRecord(files[i]);
                if (record == null || record.SameBrain(current))
                {
                    continue;
                }

                DateTime written;
                try
                {
                    written = File.GetLastWriteTimeUtc(files[i]);
                }
                catch (IOException)
                {
                    continue;
                }

                if (best == null || written > bestTime)
                {
                    best = record;
                    bestTime = written;
                }
            }

            return best;
        }
    }
}
