using NUnit.Framework;
using PersonalArena.Core;

namespace PersonalArena.CoreTests
{
    public class EpisodeTests
    {
        [Test]
        public void EpisodeEndsOnHeroDeath()
        {
            ArenaSim sim = TestHelpers.Sim();
            sim.Hero.Hp = 1f;
            TestHelpers.PlaceForAttack(sim);

            sim.Step(default);

            Assert.That(sim.Done, Is.True);
            Assert.That(TestHelpers.HasEvent(sim, SimEventType.HeroDied), Is.True);
        }

        [Test]
        public void EpisodeEndsAtTimeout()
        {
            ArenaConfig config = new ArenaConfig
            {
                ZombieCount = 0,
                EpisodeSeconds = 0.05f,
                RespawnKilledZombies = true
            };
            ArenaSim sim = new ArenaSim(DefaultDefs.Warrior(), config);

            sim.Step(default);
            sim.Step(default);
            sim.Step(default);

            Assert.That(sim.Done, Is.True);
            Assert.That(TestHelpers.HasEvent(sim, SimEventType.EpisodeTimeout), Is.True);
        }

        [Test]
        public void NoRespawnEndsWhenAllZombiesDie()
        {
            ArenaSim sim = TestHelpers.Sim();
            sim.Hero.Position = new Vec2(10f, 10f);
            sim.Hero.Facing = 0f;
            sim.Zombies[0].Position = new Vec2(11f, 10f);
            sim.Zombies[0].Facing = 0f;

            sim.Step(new HeroInput(0, 0, 1));

            Assert.That(sim.Done, Is.True);
        }

        [Test]
        public void RespawnKeepsStableCountAndRevivesAfterDelay()
        {
            ArenaSim sim = TestHelpers.Sim(1, true);
            sim.Hero.Position = new Vec2(10f, 10f);
            sim.Hero.Facing = 0f;
            sim.Zombies[0].Position = new Vec2(11f, 10f);
            sim.Zombies[0].Facing = 0f;
            sim.Step(new HeroInput(0, 0, 1));
            Assert.That(sim.Zombies[0].Alive, Is.False);

            for (int i = 0; i < 90; i++)
            {
                sim.Step(default);
            }

            Assert.That(sim.Zombies.Count, Is.EqualTo(1));
            Assert.That(sim.Zombies[0].Alive, Is.True);
            Assert.That(Vec2.Distance(sim.Zombies[0].Position, sim.Hero.Position), Is.GreaterThanOrEqualTo(4f));
        }
    }
}
