using System;
using NUnit.Framework;
using PersonalArena.Core;

namespace PersonalArena.CoreTests
{
    public class CombatTests
    {
        [Test]
        public void StrikeHitsInFrontAndMissesBehind()
        {
            ArenaSim sim = TestHelpers.Sim(2);
            sim.Hero.Position = new Vec2(10f, 10f);
            sim.Hero.Facing = 0f;
            sim.Zombies[0].Position = new Vec2(11f, 10f);
            sim.Zombies[0].Facing = MathF.PI;
            sim.Zombies[1].Position = new Vec2(9f, 10f);
            sim.Zombies[1].Facing = 0f;

            sim.Step(new HeroInput(0, 0, 1));

            Assert.That(sim.Zombies[0].Hp, Is.EqualTo(66f));
            Assert.That(sim.Zombies[1].Hp, Is.EqualTo(100f));
        }

        [Test]
        public void BackstabKillsInstantly()
        {
            ArenaSim sim = TestHelpers.Sim();
            sim.Hero.Position = new Vec2(10f, 10f);
            sim.Hero.Facing = 0f;
            sim.Zombies[0].Position = new Vec2(11f, 10f);
            sim.Zombies[0].Facing = 0f;

            sim.Step(new HeroInput(0, 0, 1));

            Assert.That(sim.Zombies[0].Alive, Is.False);
            Assert.That(TestHelpers.HasEvent(sim, SimEventType.Backstab), Is.True);
            Assert.That(TestHelpers.HasEvent(sim, SimEventType.ZombieKilled), Is.True);
        }

        [Test]
        public void KickKnocksBackStunsCancelsWindupAndEnforcesCooldown()
        {
            ArenaSim sim = TestHelpers.Sim();
            sim.Hero.Position = new Vec2(10f, 10f);
            sim.Hero.Facing = 0f;
            ZombieState zombie = sim.Zombies[0];
            zombie.Position = new Vec2(11f, 10f);
            zombie.Facing = MathF.PI;
            zombie.AttackPhase = ZombieAttackPhase.Windup;
            zombie.AttackTimer = 0.1f;

            sim.Step(new HeroInput(0, 0, 2));
            float kickedX = zombie.Position.X;
            Assert.That(kickedX, Is.GreaterThan(11f));
            Assert.That(zombie.StunRemaining, Is.GreaterThan(1.1f));
            Assert.That(zombie.AttackPhase, Is.EqualTo(ZombieAttackPhase.Idle));

            sim.Step(new HeroInput(0, 0, 2));
            Assert.That(TestHelpers.HasEvent(sim, SimEventType.SkillFailedCooldown), Is.True);

            for (int i = 0; i < 30; i++)
            {
                sim.Step(default);
            }

            Assert.That(zombie.Position.X, Is.EqualTo(kickedX).Within(1e-5f));
        }

        [Test]
        public void BlockHalvesFrontalDamage()
        {
            ArenaSim sim = TestHelpers.Sim();
            sim.Step(new HeroInput(0, 0, 3));
            for (int i = 0; i < 13; i++)
            {
                sim.Step(new HeroInput(0, 0, 3));
            }

            TestHelpers.PlaceForAttack(sim);
            sim.Step(new HeroInput(0, 0, 3));

            Assert.That(sim.Hero.Hp, Is.EqualTo(90f).Within(1e-4f));
            Assert.That(TestHelpers.HasEvent(sim, SimEventType.BlockedHit), Is.True);
        }

        [Test]
        public void BlockDoesNotProtectBackAndBackHitIsDoubleDamage()
        {
            ArenaSim sim = TestHelpers.Sim();
            sim.Step(new HeroInput(0, 0, 3));
            TestHelpers.PlaceForAttack(sim, true);
            sim.Step(new HeroInput(0, 0, 3));

            Assert.That(sim.Hero.Hp, Is.EqualTo(60f));
            Assert.That(TestHelpers.HasEvent(sim, SimEventType.HeroDamagedFromBehind), Is.True);
            Assert.That(TestHelpers.HasEvent(sim, SimEventType.BlockedHit), Is.False);
        }

        [Test]
        public void PerfectParryPreventsDamageAndStunsAttacker()
        {
            ArenaSim sim = TestHelpers.Sim();
            TestHelpers.PlaceForAttack(sim);

            sim.Step(new HeroInput(0, 0, 3));

            Assert.That(sim.Hero.Hp, Is.EqualTo(100f));
            Assert.That(sim.Zombies[0].StunRemaining, Is.EqualTo(1.5f));
            Assert.That(TestHelpers.HasEvent(sim, SimEventType.Parry), Is.True);
        }

        [Test]
        public void BlockingDrainsEnergyAndStopsAtZero()
        {
            ArenaSim sim = TestHelpers.Sim(0, true);
            sim.Hero.Energy = 10.1f;

            sim.Step(new HeroInput(0, 0, 3));

            Assert.That(sim.Hero.Energy, Is.Zero);
            Assert.That(sim.Hero.IsBlocking, Is.False);
            sim.Step(new HeroInput(0, 0, 3));
            Assert.That(TestHelpers.HasEvent(sim, SimEventType.SkillFailedEnergy), Is.True);
        }

        [Test]
        public void DashUsesMoveDirectionAndConsumesEnergy()
        {
            ArenaSim sim = TestHelpers.Sim(0);
            Vec2 start = sim.Hero.Position;

            sim.Step(new HeroInput(1, 0, 4));

            Assert.That(sim.Hero.Position.X, Is.GreaterThan(start.X + 3f));
            Assert.That(sim.Hero.Energy, Is.EqualTo(85f).Within(1e-4f));
        }
    }
}
