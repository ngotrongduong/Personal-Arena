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
        public int[] SkillUses { get; set; } = new int[4];
        public int[] FinalItemLevels { get; set; } = new int[SurvivorCatalog.CatalogSize];
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
    }

    public sealed class SurvivorEvaluator
    {
        /// <summary>The pilot's sampling Rng is seeded with seed ^ PilotSeedSalt so it is not the sim's stream.</summary>
        public const int PilotSeedSalt = 0x5EED5EED;

        public SurvivorRunStats RunOne(PolicyBrain brain, SurvivorConfig config, int seed, bool deterministic)
        {
            string problem = SurvivorPilot.Validate(brain); if (problem != null) throw new ArgumentException(problem, nameof(brain));
            SurvivorSim sim = new SurvivorSim(config, seed); SurvivorPilot pilot = new SurvivorPilot(brain, seed ^ PilotSeedSalt) { Deterministic = deterministic };
            while (!sim.IsEnded) sim.Step(pilot.NextInput(sim));
            SurvivorRunStats result = new SurvivorRunStats
            {
                Seed = seed, SurvivedSeconds = sim.Time, EndReason = sim.EndReason, DeathCause = sim.DeathCause,
                Level = sim.Level, Kills = sim.Kills, EliteKills = sim.EliteKills, Gold = sim.Gold,
                TotalXp = sim.TotalXp, DamageTaken = sim.DamageTaken, DamageDealt = sim.DamageDealtTotal,
                BossDamageFraction = sim.BossDamageFraction, MinHpRatio = sim.MinHpRatio, MinHpTime = sim.MinHpTime
            };
            Array.Copy(sim.SkillUses, result.SkillUses, 4);
            for (int i = 0; i < result.FinalItemLevels.Length; i++) result.FinalItemLevels[i] = sim.Inventory.Level(i);
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
            summary.MeanBossDamageFraction = (float)(boss / runs.Count); summary.MeanLevel = (float)(levels / runs.Count); return summary;
        }

        public bool PassesM4A(SurvivorEvalSummary summary) => summary != null && summary.MedianSurvivedSeconds >= 600f && summary.P10SurvivedSeconds >= 420f && summary.CatastrophicCount == 0;
    }
}
