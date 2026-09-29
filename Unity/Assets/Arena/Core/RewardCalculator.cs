using System;
using System.Collections.Generic;

namespace PersonalArena.Core
{
    /// <summary>Computes one reward exclusively from events and elapsed time.</summary>
    public sealed class RewardCalculator
    {
        private readonly RewardConfig config;

        public RewardCalculator(RewardConfig config)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public float Compute(IReadOnlyList<SimEvent> events, float dt)
        {
            float reward = config.SurvivalPerSecond * dt;
            for (int i = 0; i < events.Count; i++)
            {
                SimEvent item = events[i];
                switch (item.Type)
                {
                    case SimEventType.HeroDealtDamage:
                        reward += item.Value * config.DamageDealtPerHp;
                        break;
                    case SimEventType.ZombieKilled:
                        reward += config.Kill;
                        break;
                    case SimEventType.Backstab:
                        reward += config.BackstabBonus;
                        break;
                    case SimEventType.Parry:
                        reward += config.ParryBonus;
                        break;
                    case SimEventType.Kick:
                        reward += config.KickHitBonus;
                        break;
                    case SimEventType.HeroDamaged:
                        reward += item.Value * config.HeroDamagedPerHp;
                        break;
                    case SimEventType.HeroDamagedFromBehind:
                        reward += item.Value * config.FromBehindExtraPerHp;
                        break;
                    case SimEventType.HeroDied:
                        reward += config.Death;
                        break;
                    case SimEventType.HeroFell:
                        reward += config.FallExtra;
                        break;
                    case SimEventType.ZombieFell:
                        reward += config.KnockOffBonus;
                        break;
                    case SimEventType.PotionPicked:
                        reward += item.Value * config.HealPerHp;
                        break;
                    case SimEventType.SkillFailedCooldown:
                    case SimEventType.SkillFailedEnergy:
                        reward += config.SkillFailedPenalty;
                        break;
                }
            }

            return reward;
        }
    }
}
