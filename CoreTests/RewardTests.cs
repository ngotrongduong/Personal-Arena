using System;
using NUnit.Framework;
using PersonalArena.Core;

namespace PersonalArena.CoreTests
{
    public class RewardTests
    {
        private RewardCalculator calculator;

        [SetUp]
        public void SetUp()
        {
            calculator = new RewardCalculator(new RewardConfig());
        }

        [TestCase(SimEventType.HeroDealtDamage, 10f)]
        [TestCase(SimEventType.ZombieKilled, 1f)]
        [TestCase(SimEventType.Backstab, 1f)]
        [TestCase(SimEventType.Parry, 1f)]
        [TestCase(SimEventType.Kick, 1f)]
        public void PositiveRewardTermsArePositive(SimEventType type, float value)
        {
            float reward = calculator.Compute(new[] { new SimEvent(type, value) }, 0f);
            Assert.That(reward, Is.GreaterThan(0f));
        }

        [TestCase(SimEventType.HeroDamaged, 10f)]
        [TestCase(SimEventType.HeroDamagedFromBehind, 10f)]
        [TestCase(SimEventType.HeroDied, 1f)]
        [TestCase(SimEventType.HitWall, 1f)]
        [TestCase(SimEventType.SkillFailedCooldown, 1f)]
        [TestCase(SimEventType.SkillFailedEnergy, 1f)]
        public void PenaltyRewardTermsAreNegative(SimEventType type, float value)
        {
            float reward = calculator.Compute(new[] { new SimEvent(type, value) }, 0f);
            Assert.That(reward, Is.LessThan(0f));
        }

        [Test]
        public void SurvivalRewardIsPositive()
        {
            Assert.That(calculator.Compute(Array.Empty<SimEvent>(), 1f), Is.GreaterThan(0f));
        }

        [Test]
        public void DamageAndKillCanNeverHaveNegativeDefaultRewards()
        {
            SimEvent[] events =
            {
                new SimEvent(SimEventType.HeroDealtDamage, 34f),
                new SimEvent(SimEventType.ZombieKilled, 1f)
            };

            Assert.That(calculator.Compute(events, 0f), Is.GreaterThanOrEqualTo(0f));
        }
    }
}
