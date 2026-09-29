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
            Assert.That(config.Width, Is.EqualTo(ArenaConfig.DefaultSize));
            Assert.That(config.HpMultiplier, Is.EqualTo(1f));
            Assert.That(config.DamageMultiplier, Is.EqualTo(1f));
            Assert.That(config.SpeedMultiplier, Is.EqualTo(1.25f));
        }

        [Test]
        public void CreateSpawnsDropsZeroWeightsAndKeepsTypeOrder()
        {
            ZombieSpawnEntry[] spawns = EnvConfigFactory.CreateSpawns(1f, 0.5f, 0f, float.NaN);

            Assert.That(spawns.Length, Is.EqualTo(2));
            Assert.That(spawns[0].Type.Id, Is.EqualTo("walker"));
            Assert.That(spawns[1].Type.Id, Is.EqualTo("runner"));
            Assert.That(spawns[1].Weight, Is.EqualTo(0.5f));
        }

        [Test]
        public void CreateSpawnsFallsBackToWalkersWhenEveryWeightIsZero()
        {
            ZombieSpawnEntry[] spawns = EnvConfigFactory.CreateSpawns(0f, 0f, -1f, 0f);

            Assert.That(spawns.Length, Is.EqualTo(1));
            Assert.That(spawns[0].Type.Id, Is.EqualTo("walker"));
        }

        [Test]
        public void CreateUsesExplicitMixAndSameSpawnsComparesIt()
        {
            ZombieSpawnEntry[] mix = EnvConfigFactory.CreateSpawns(1f, 0f, 0.35f, 0.35f);
            ArenaConfig config = EnvConfigFactory.Create(
                EnvConfigFactory.CreateBaseConfig(), 4f, 28f, 1f, 1f, 1f, 5, mix);

            Assert.DoesNotThrow(config.Validate);
            Assert.That(EnvConfigFactory.SameSpawns(config.ZombieSpawns, mix), Is.True);
            Assert.That(
                EnvConfigFactory.SameSpawns(config.ZombieSpawns, EnvConfigFactory.CreateSpawns(1f, 0f, 0f, 0f)),
                Is.False);
        }

        [Test]
        public void ClassRegistryKnowsAllThreeClasses()
        {
            Assert.That(ClassRegistry.Create("mage").Id, Is.EqualTo("mage"));
            Assert.That(ClassRegistry.BehaviorName("archer"), Is.EqualTo("Archer"));
            Assert.That(ClassRegistry.BehaviorName("Warrior"), Is.EqualTo("Warrior"));
            Assert.That(ClassRegistry.IsKnown("rogue"), Is.False);
        }
    }
}
