using NUnit.Framework;
using PersonalArena.Core;

namespace PersonalArena.ML.Tests
{
    public sealed class EnvConfigFactoryTests
    {
        [Test]
        public void CreateClampsTrainerValuesToCoreRanges()
        {
            ArenaConfig config = EnvConfigFactory.Create(
                EnvConfigFactory.CreateBaseConfig(),
                100.6f,
                2f,
                0.1f,
                12f,
                float.PositiveInfinity,
                123);

            Assert.That(config.ZombieCount, Is.EqualTo(64));
            Assert.That(config.Width, Is.EqualTo(8f));
            Assert.That(config.Height, Is.EqualTo(8f));
            Assert.That(config.HpMultiplier, Is.EqualTo(0.25f));
            Assert.That(config.DamageMultiplier, Is.EqualTo(4f));
            Assert.That(config.SpeedMultiplier, Is.EqualTo(1f));
            Assert.That(config.Seed, Is.EqualTo(123));
            Assert.DoesNotThrow(config.Validate);
        }

        [Test]
        public void CreateRoundsZombieCountAndUsesDefaultsForNonFiniteValues()
        {
            ArenaConfig config = EnvConfigFactory.Create(
                EnvConfigFactory.CreateBaseConfig(),
                3.5f,
                float.NaN,
                float.NaN,
                float.NegativeInfinity,
                1.25f,
                9);

            Assert.That(config.ZombieCount, Is.EqualTo(4));
            Assert.That(config.Width, Is.EqualTo(20f));
            Assert.That(config.HpMultiplier, Is.EqualTo(1f));
            Assert.That(config.DamageMultiplier, Is.EqualTo(1f));
            Assert.That(config.SpeedMultiplier, Is.EqualTo(1.25f));
        }
    }
}
