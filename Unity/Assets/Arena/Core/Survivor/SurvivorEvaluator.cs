using System;
using System.Collections.Generic;
using PersonalArena.Core;

namespace PersonalArena.Core.Survivor
{
    public sealed class SurvivorRunStats
    {
        public int Seed { get; set; }
        public float SurvivedSeconds { get; set; }
        public EndReason EndReason { get; set; }
        public DeathCause DeathCause { get; set; }
        public int Level { get; set; }
        public int Kills { get; set; }
        public int EliteKills { get; set; }
        public float Gold { get; set; }
        public float TotalXp { get; set; }
        public float DamageTaken { get; set; }
        public float DamageDealt { get; set; }
        public float BossDamageFraction { get; set; }
        public float MinHpRatio { get; set; }
        public float MinHpTime { get; set; }
        public int[] SkillUses { get; set; } = new int[SurvivorInput.SkillSlotCount];
        public int[] FinalItemLevels { get; set; } = new int[SurvivorCatalog.CatalogSize];
        /// <summary>Run gold per <see cref="GoldSource"/> (length <see cref="SurvivorSim.GoldSourceCount"/>).</summary>
        public float[] GoldBySource { get; set; } = new float[SurvivorSim.GoldSourceCount];
        public SurvivorBehaviorStats Behavior { get; set; } = new SurvivorBehaviorStats();
    }

    public sealed class SurvivorEvalSummary
    {
        public int Runs { get; set; }
        public float MedianSurvivedSeconds { get; set; }
        public float P10SurvivedSeconds { get; set; }
        public float MeanSurvivedSeconds { get; set; }
        public float WinRate { get; set; }
        public int CatastrophicCount { get; set; }
        public float GoldPerMinute { get; set; }
        public float XpPerMinute { get; set; }
        public float DamageTakenPerMinute { get; set; }
        public float MeanBossDamageFraction { get; set; }
        public float MeanLevel { get; set; }
        public Dictionary<EndReason, int> EndReasonCounts { get; set; } = new Dictionary<EndReason, int>();
        public Dictionary<DeathCause, int> DeathCauseCounts { get; set; } = new Dictionary<DeathCause, int>();
        public SurvivorBehaviorStats Behavior { get; set; } = new SurvivorBehaviorStats();
    }

    public sealed class SurvivorEvaluator
    {
        /// <summary>The pilot's sampling Rng is seeded with seed ^ PilotSeedSalt so it is not the sim's stream.</summary>
        public const int PilotSeedSalt = 0x5EED5EED;

        public SurvivorRunStats RunOne(PolicyBrain brain, SurvivorConfig config, int seed, bool deterministic)
        {
            string problem = SurvivorPilot.Validate(brain); if (problem != null) throw new ArgumentException(problem, nameof(brain));
            SurvivorSim sim = new SurvivorSim(config, seed); SurvivorPilot pilot = new SurvivorPilot(brain, seed ^ PilotSeedSalt) { Deterministic = deterministic };
            SurvivorBehaviorTracker tracker = new SurvivorBehaviorTracker(); tracker.Reset(sim);
            while (!sim.IsEnded) { sim.Step(pilot.NextInput(sim)); tracker.Observe(sim); }
            SurvivorRunStats result = new SurvivorRunStats
            {
                Seed = seed, SurvivedSeconds = sim.Time, EndReason = sim.EndReason, DeathCause = sim.DeathCause,
                Level = sim.Level, Kills = sim.Kills, EliteKills = sim.EliteKills, Gold = sim.Gold,
                TotalXp = sim.TotalXp, DamageTaken = sim.DamageTaken, DamageDealt = sim.DamageDealtTotal,
                BossDamageFraction = sim.BossDamageFraction, MinHpRatio = sim.MinHpRatio, MinHpTime = sim.MinHpTime,
                Behavior = tracker.Finish(sim)
            };
            Array.Copy(sim.SkillUses, result.SkillUses, SurvivorInput.SkillSlotCount);
            for (int i = 0; i < result.FinalItemLevels.Length; i++) result.FinalItemLevels[i] = sim.Inventory.Level(i);
            for (int i = 0; i < result.GoldBySource.Length; i++) result.GoldBySource[i] = sim.GetGold((GoldSource)i);
            return result;
        }

        public SurvivorEvalSummary Summarize(IReadOnlyList<SurvivorRunStats> runs)
        {
            if (runs == null) throw new ArgumentNullException(nameof(runs));
            SurvivorEvalSummary summary = new SurvivorEvalSummary { Runs = runs.Count };
            if (runs.Count == 0) return summary;
            float[] times = new float[runs.Count]; double totalSeconds = 0, gold = 0, xp = 0, damage = 0, boss = 0, levels = 0; int wins = 0;
            foreach (EndReason reason in Enum.GetValues(typeof(EndReason))) summary.EndReasonCounts[reason] = 0;
            foreach (DeathCause cause in Enum.GetValues(typeof(DeathCause))) summary.DeathCauseCounts[cause] = 0;
            for (int i = 0; i < runs.Count; i++)
            {
                SurvivorRunStats run = runs[i]; times[i] = run.SurvivedSeconds; totalSeconds += run.SurvivedSeconds; gold += run.Gold; xp += run.TotalXp; damage += run.DamageTaken; boss += run.BossDamageFraction; levels += run.Level;
                if (run.EndReason == EndReason.Won) wins++; if (run.EndReason == EndReason.Died && run.SurvivedSeconds < 180f) summary.CatastrophicCount++;
                summary.EndReasonCounts[run.EndReason]++; summary.DeathCauseCounts[run.DeathCause]++;
            }
            Array.Sort(times); int medianIndex = times.Length / 2;
            summary.MedianSurvivedSeconds = times.Length % 2 == 0 ? (times[medianIndex - 1] + times[medianIndex]) * 0.5f : times[medianIndex];
            summary.P10SurvivedSeconds = times[Math.Max(0, (int)Math.Ceiling(0.1 * times.Length) - 1)]; summary.MeanSurvivedSeconds = (float)(totalSeconds / runs.Count);
            summary.WinRate = (float)wins / runs.Count; double minutes = totalSeconds / 60.0;
            if (minutes > 0) { summary.GoldPerMinute = (float)(gold / minutes); summary.XpPerMinute = (float)(xp / minutes); summary.DamageTakenPerMinute = (float)(damage / minutes); }
            summary.MeanBossDamageFraction = (float)(boss / runs.Count); summary.MeanLevel = (float)(levels / runs.Count);
            summary.Behavior = SummarizeBehavior(runs); return summary;
        }

        public bool PassesM4A(SurvivorEvalSummary summary) => summary != null && summary.MedianSurvivedSeconds >= 600f && summary.P10SurvivedSeconds >= 420f && summary.CatastrophicCount == 0;

        private static SurvivorBehaviorStats SummarizeBehavior(IReadOnlyList<SurvivorRunStats> runs)
        {
            SurvivorBehaviorStats result = new SurvivorBehaviorStats();
            double aggression = 0, caution = 0, greed = 0, exploration = 0, crowd = 0, boss = 0, discipline = 0, range = 0, distance = 0;
            int aggressionCount = 0, cautionCount = 0, greedCount = 0, explorationCount = 0, crowdCount = 0, bossCount = 0, disciplineCount = 0, rangeCount = 0, distanceCount = 0;
            for (int i = 0; i < runs.Count; i++)
            {
                SurvivorBehaviorStats b = runs[i].Behavior;
                if (b == null) continue;
                AddMetric(b.Aggression, ref aggression, ref aggressionCount); AddMetric(b.Caution, ref caution, ref cautionCount);
                AddMetric(b.Greed, ref greed, ref greedCount); AddMetric(b.Exploration, ref exploration, ref explorationCount);
                AddMetric(b.CrowdControl, ref crowd, ref crowdCount); AddMetric(b.BossHunting, ref boss, ref bossCount);
                AddMetric(b.SkillDiscipline, ref discipline, ref disciplineCount); AddMetric(b.PreferredRange, ref range, ref rangeCount);
                AddMetric(b.KeepDistance, ref distance, ref distanceCount);
                result.KickUses += b.KickUses; result.BlockUses += b.BlockUses; result.DashUses += b.DashUses;
                result.EffectiveKicks += b.EffectiveKicks; result.EffectiveBlocks += b.EffectiveBlocks; result.EffectiveDashes += b.EffectiveDashes;
            }
            result.Aggression = MeanMetric(aggression, aggressionCount); result.Caution = MeanMetric(caution, cautionCount);
            result.Greed = MeanMetric(greed, greedCount); result.Exploration = MeanMetric(exploration, explorationCount);
            result.CrowdControl = MeanMetric(crowd, crowdCount); result.BossHunting = MeanMetric(boss, bossCount);
            result.SkillDiscipline = MeanMetric(discipline, disciplineCount); result.PreferredRange = MeanMetric(range, rangeCount);
            result.KeepDistance = MeanMetric(distance, distanceCount); return result;
        }

        private static void AddMetric(float value, ref double total, ref int count) { if (value >= 0f) { total += value; count++; } }
        private static float MeanMetric(double total, int count) => count > 0 ? (float)(total / count) : -1f;
    }
}
