using System;
using NUnit.Framework;
using PersonalArena.Core;

namespace PersonalArena.CoreTests
{
    public class ConfigAndDeterminismTests
    {
        [Test]
        public void SameSeedAndInputsProduceIdenticalState()
        {
            ArenaSim first = TestHelpers.Sim(8, true, 12345);
            ArenaSim second = TestHelpers.Sim(8, true, 12345);

            for (int tick = 0; tick < 600; tick++)
            {
                HeroInput input = new HeroInput((tick / 17) % 9, tick % 19 == 0 ? 1 : 0, tick % 53 == 0 ? 1 : 0);
                first.Step(input);
                second.Step(input);
            }

            Assert.That(first.Hero.Position, Is.EqualTo(second.Hero.Position));
            Assert.That(first.Hero.Hp, Is.EqualTo(second.Hero.Hp));
            Assert.That(first.Zombies.Count, Is.EqualTo(second.Zombies.Count));
            for (int i = 0; i < first.Zombies.Count; i++)
            {
                Assert.That(first.Zombies[i].Position, Is.EqualTo(second.Zombies[i].Position));
                Assert.That(first.Zombies[i].Hp, Is.EqualTo(second.Zombies[i].Hp));
            }
        }

        [Test]
        public void DifferentSeedsProduceDifferentSpawns()
        {
            ArenaSim first = TestHelpers.Sim(1, true, 1);
            ArenaSim second = TestHelpers.Sim(1, true, 2);

            Assert.That(first.Zombies[0].Position, Is.Not.EqualTo(second.Zombies[0].Position));
        }

        [TestCase(7f, "Width")]
        [TestCase(81f, "Width")]
        public void ValidationRejectsBadWidth(float width, string expectedText)
        {
            ArenaConfig config = new ArenaConfig { Width = width };
            ArgumentException exception = Assert.Throws<ArgumentException>(() => config.Validate());
            Assert.That(exception.Message, Does.Contain(expectedText));
        }

        [Test]
        public void ValidationRejectsBadCountsMultipliersAndSpawns()
        {
            ArenaConfig badCount = new ArenaConfig { ZombieCount = 65 };
            Assert.That(Assert.Throws<ArgumentException>(() => badCount.Validate()).Message,
                Does.Contain("ZombieCount"));

            ArenaConfig badSpeed = new ArenaConfig { SpeedMultiplier = 0.1f };
            Assert.That(Assert.Throws<ArgumentException>(() => badSpeed.Validate()).Message,
                Does.Contain("SpeedMultiplier"));

            ArenaConfig noSpawns = new ArenaConfig { ZombieCount = 1, ZombieSpawns = null };
            Assert.That(Assert.Throws<ArgumentException>(() => noSpawns.Validate()).Message,
                Does.Contain("ZombieSpawns"));
        }

        [Test]
        public void DefaultFactoriesReturnIndependentObjects()
        {
            HeroClassDef first = DefaultDefs.Warrior();
            HeroClassDef second = DefaultDefs.Warrior();
            first.Skills[0].Damage = 999f;

            Assert.That(second.Skills[0].Damage, Is.EqualTo(34f));
        }
    }
}
