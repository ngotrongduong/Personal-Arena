using System;
using System.Collections.Generic;

namespace PersonalArena.Core.Survivor
{
    public sealed class SurvivorRewardConfig
    {
        public float Win = 10f;
        public float TimeUp = 5f;
        public float Death = 5f;
        public float SurvivePerSecond = 0.01f;
        public float HpLostPerMaxHp = 1f;
        public float PerLevelProgress = 0.05f;
        public float PerGold = 0.001f;
        public float BossDamage = 2f;
        public float PerDamage = 0f;
        // T-027: a direct reward per kill so fighting pays now, not only through the XP gems it drops.
        // ~1500 kills in a full 15-minute run is worth ~6, close to TimeUp but below Death + HP loss.
        public float PerKill = 0.004f;
        public float PerEliteKill = 0.1f;

        /// <summary>
        /// A new reward config for a Training Focus. Balanced is exactly the defaults; each other
        /// focus changes only the fields in the T-023 table (plus the T-027 kill rows).
        /// </summary>
        public static SurvivorRewardConfig ForFocus(TrainingFocus focus)
        {
            SurvivorRewardConfig config = new SurvivorRewardConfig();
            switch (focus)
            {
                case TrainingFocus.Survival: config.HpLostPerMaxHp = 1.6f; config.Death = 6f; config.PerGold = 0.0005f; config.PerKill = 0.002f; break;
                case TrainingFocus.Gold: config.PerGold = 0.003f; break;
                case TrainingFocus.Boss: config.BossDamage = 5f; break;
                case TrainingFocus.Offense: config.PerLevelProgress = 0.1f; config.HpLostPerMaxHp = 0.7f; config.PerKill = 0.008f; config.PerEliteKill = 0.2f; break;
            }
            return config;
        }

        public void Validate()
        {
            Check(Win); Check(TimeUp); Check(Death); Check(SurvivePerSecond); Check(HpLostPerMaxHp);
            Check(PerLevelProgress); Check(PerGold); Check(BossDamage); Check(PerDamage); Check(PerKill); Check(PerEliteKill);
        }
        private static void Check(float value) { if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f) throw new ArgumentOutOfRangeException(); }
    }

    public sealed class SurvivorRewardCalculator
    {
        private readonly SurvivorRewardConfig config;
        public SurvivorRewardCalculator(SurvivorRewardConfig config) { this.config = config ?? throw new ArgumentNullException(nameof(config)); config.Validate(); }
        public float Compute(IReadOnlyList<SurvivorEvent> events, float stepSeconds)
        {
            float reward = config.SurvivePerSecond * stepSeconds;
            for (int i = 0; i < events.Count; i++)
            {
                SurvivorEvent e = events[i];
                reward += e.Type switch
                {
                    SurvivorEventType.RunWon => config.Win,
                    SurvivorEventType.RunTimeUp => config.TimeUp,
                    SurvivorEventType.HeroDied => -config.Death,
                    SurvivorEventType.HeroDamaged => -config.HpLostPerMaxHp * e.Extra,
                    SurvivorEventType.XpCollected => config.PerLevelProgress * e.Extra,
                    SurvivorEventType.GoldCollected => config.PerGold * e.Value,
                    SurvivorEventType.BossDamaged => config.BossDamage * e.Extra,
                    SurvivorEventType.DamageDealt => config.PerDamage * e.Value,
                    SurvivorEventType.EnemyKilled => config.PerKill,
                    SurvivorEventType.EliteKilled => config.PerEliteKill,
                    _ => 0f
                };
            }
            return reward;
        }
    }
}
