using NUnit.Framework;
using PersonalArena.Core;

namespace PersonalArena.CoreTests
{
    public class ArenaEdgeAndPotionTests
    {
        [Test]
        public void HeroWalkingOffThePlatformFallsAndEndsTheEpisode()
        {
            ArenaSim sim = TestHelpers.Sim(0, true);
            sim.Hero.Position = new Vec2(19.5f, 10f);

            bool fell = false;
            for (int i = 0; i < 60 && !sim.Done; i++)
            {
                sim.Step(new HeroInput(1, 0, 0));
                fell |= TestHelpers.HasEvent(sim, SimEventType.HeroFell);
            }

            Assert.That(fell, Is.True);
            Assert.That(sim.Hero.FellOff, Is.True);
            Assert.That(sim.Hero.Alive, Is.False);
            Assert.That(sim.Done, Is.True);
        }

        [Test]
        public void ZombiesDoNotWalkIntoTheAbyssOnTheirOwn()
        {
            ArenaSim sim = TestHelpers.Sim();
            ZombieState zombie = sim.Zombies[0];
            sim.Hero.Position = new Vec2(18.6f, 10f);
            zombie.Position = new Vec2(19.3f, 10f);

            for (int i = 0; i < 30; i++)
            {
                sim.Step(default);
            }

            Assert.That(zombie.Alive, Is.True);
            float limit = sim.Config.Radius - zombie.Def.Radius;
            Assert.That(Vec2.Distance(zombie.Position, sim.Config.Center), Is.LessThanOrEqualTo(limit + 1e-4f));
        }

        [Test]
        public void KilledZombieCanDropPotionThatHealsOnPickup()
        {
            ArenaSim sim = TestHelpers.Sim();
            sim.Config.PotionDropChance = 1f;
            sim.Hero.Position = new Vec2(10f, 10f);
            sim.Hero.Facing = 0f;
            sim.Hero.Hp = 50f;
            sim.Zombies[0].Position = new Vec2(11.5f, 10f);
            sim.Zombies[0].Facing = 0f;

            sim.Step(new HeroInput(0, 0, 1));

            Assert.That(TestHelpers.HasEvent(sim, SimEventType.PotionDropped), Is.True);
            Assert.That(sim.Potions[0].Active, Is.True);
        }

        [Test]
        public void PotionPickupHealsAndRewardsTheHealedAmount()
        {
            ArenaSim sim = TestHelpers.Sim(0, true);
            sim.Hero.Position = new Vec2(10f, 10f);
            sim.Hero.Hp = 90f;
            PotionState potion = sim.Potions[0];
            potion.Active = true;
            potion.Position = new Vec2(10.5f, 10f);
            potion.Remaining = 5f;

            sim.Step(default);

            Assert.That(sim.Hero.Hp, Is.EqualTo(sim.HeroDef.MaxHp));
            Assert.That(potion.Active, Is.False);
            Assert.That(TestHelpers.HasEvent(sim, SimEventType.PotionPicked), Is.True);
            float healed = 0f;
            for (int i = 0; i < sim.Events.Count; i++)
            {
                if (sim.Events[i].Type == SimEventType.PotionPicked)
                {
                    healed = sim.Events[i].Value;
                }
            }

            Assert.That(healed, Is.EqualTo(10f).Within(1e-4f));
        }

        [Test]
        public void FullHealthHeroLeavesPotionAndItExpires()
        {
            ArenaSim sim = TestHelpers.Sim(0, true);
            sim.Hero.Position = new Vec2(10f, 10f);
            PotionState potion = sim.Potions[0];
            potion.Active = true;
            potion.Position = new Vec2(10.2f, 10f);
            potion.Remaining = 0.05f;

            sim.Step(default);
            Assert.That(potion.Active, Is.True);
            for (int i = 0; i < 5; i++)
            {
                sim.Step(default);
            }

            Assert.That(potion.Active, Is.False);
        }

        [Test]
        public void SpawnsAreInsideThePlatformAndAwayFromTheHero()
        {
            ArenaSim sim = new ArenaSim(DefaultDefs.Warrior(), new ArenaConfig { ZombieCount = 16, Seed = 3 });
            for (int i = 0; i < sim.Zombies.Count; i++)
            {
                ZombieState zombie = sim.Zombies[i];
                Assert.That(sim.IsOverAbyss(zombie.Position), Is.False);
                Assert.That(Vec2.Distance(zombie.Position, sim.Config.Center), Is.LessThan(sim.Config.Radius));
            }

            Assert.That(sim.Hero.Position, Is.EqualTo(sim.Config.Center));
        }

        [Test]
        public void RespawnedZombiesClimbInAtTheRim()
        {
            ArenaSim sim = TestHelpers.Sim(1, true);
            sim.Hero.Position = new Vec2(10f, 10f);
            sim.Hero.Facing = 0f;
            sim.Zombies[0].Position = new Vec2(11f, 10f);
            sim.Zombies[0].Facing = 0f;
            sim.Step(new HeroInput(0, 0, 1));
            Assert.That(sim.Zombies[0].Alive, Is.False);

            for (int i = 0; i < 90 && !sim.Zombies[0].Alive; i++)
            {
                sim.Step(default);
            }

            ZombieState zombie = sim.Zombies[0];
            Assert.That(zombie.Alive, Is.True);
            float expected = sim.Config.Radius - zombie.Def.Radius - 0.2f;
            Assert.That(Vec2.Distance(zombie.Position, sim.Config.Center), Is.EqualTo(expected).Within(0.3f));
        }
    }
}
