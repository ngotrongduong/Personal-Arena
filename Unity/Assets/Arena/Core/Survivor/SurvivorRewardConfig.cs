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

        public void Validate()
        {
            Check(Win); Check(TimeUp); Check(Death); Check(SurvivePerSecond); Check(HpLostPerMaxHp);
            Check(PerLevelProgress); Check(PerGold); Check(BossDamage); Check(PerDamage);
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
                    _ => 0f
                };
            }
            return reward;
        }
    }
}
