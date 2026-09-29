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
            float firstTickX = zombie.Position.X;
            Assert.That(firstTickX, Is.GreaterThan(11f));
            Assert.That(firstTickX, Is.LessThan(12f), "knockback slides over several ticks, no teleport");
            Assert.That(zombie.KnockbackVelocity.X, Is.GreaterThan(0f));
            Assert.That(zombie.StunRemaining, Is.GreaterThan(1.1f));
            Assert.That(zombie.AttackPhase, Is.EqualTo(ZombieAttackPhase.Idle));

            sim.Step(new HeroInput(0, 0, 2));
            Assert.That(TestHelpers.HasEvent(sim, SimEventType.SkillFailedCooldown), Is.True);

            for (int i = 0; i < 50; i++)
            {
                sim.Step(default);
            }

            float settledX = zombie.Position.X;
            Assert.That(settledX, Is.EqualTo(11f + DefaultDefs.Warrior().Skills[1].Knockback).Within(0.05f));
            Assert.That(zombie.KnockbackVelocity, Is.EqualTo(Vec2.Zero));

            for (int i = 0; i < 10; i++)
            {
                sim.Step(default);
            }

            Assert.That(zombie.Position.X, Is.EqualTo(settledX).Within(1e-5f), "still stunned, so it stays put");
        }

        [Test]
        public void KickCanThrowZombieIntoTheAbyssWithoutPotionDrop()
        {
            ArenaSim sim = TestHelpers.Sim();
            sim.Config.PotionDropChance = 1f;
            sim.Hero.Position = new Vec2(17f, 10f);
            sim.Hero.Facing = 0f;
            ZombieState zombie = sim.Zombies[0];
            zombie.Position = new Vec2(18f, 10f);
            zombie.Facing = MathF.PI;

            sim.Step(new HeroInput(0, 0, 2));
            bool fell = false;
            for (int i = 0; i < 60 && !fell; i++)
            {
                sim.Step(default);
                fell = TestHelpers.HasEvent(sim, SimEventType.ZombieFell);
                if (fell)
                {
                    Assert.That(TestHelpers.HasEvent(sim, SimEventType.ZombieKilled), Is.True);
                }
            }

            Assert.That(fell, Is.True);
            Assert.That(zombie.Alive, Is.False);
            Assert.That(zombie.FellOff, Is.True);
            Assert.That(sim.Potions[0].Active, Is.False);
        }

        [Test]
        public void BlockStaggersAndPushesTheAttacker()
        {
            ArenaSim sim = TestHelpers.Sim();
            for (int i = 0; i < 14; i++)
            {
                sim.Step(new HeroInput(0, 0, 3));
            }

            TestHelpers.PlaceForAttack(sim);
            sim.Step(new HeroInput(0, 0, 3));

            ZombieState zombie = sim.Zombies[0];
            Assert.That(TestHelpers.HasEvent(sim, SimEventType.Stagger), Is.True);
            Assert.That(zombie.StunRemaining, Is.GreaterThanOrEqualTo(0.5f));
            Assert.That(zombie.KnockbackVelocity.Y, Is.GreaterThan(0f), "pushed away from the hero");
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
        public void DashGlidesAlongMoveDirectionAndConsumesEnergy()
        {
            ArenaSim sim = TestHelpers.Sim(0, true);
            Vec2 start = sim.Hero.Position;

            sim.Step(new HeroInput(1, 0, 4));

            Assert.That(sim.Hero.DashRemaining, Is.GreaterThan(0f));
            Assert.That(sim.Hero.Position.X, Is.GreaterThan(start.X));
            Assert.That(sim.Hero.Position.X, Is.LessThan(start.X + 1f), "a dash is a glide, not a teleport");
            Assert.That(sim.Hero.Energy, Is.EqualTo(85f).Within(1e-4f));

            for (int i = 0; i < 12 && sim.Hero.DashRemaining > 0f; i++)
            {
                sim.Step(new HeroInput(3, 0, 0));
            }

            Assert.That(sim.Hero.DashRemaining, Is.Zero);
            Assert.That(sim.Hero.Position.X, Is.EqualTo(start.X + 3f).Within(0.35f));
            Assert.That(sim.Hero.Position.Y, Is.EqualTo(start.Y).Within(1e-4f), "move input ignored mid-dash");
        }

        [Test]
        public void MovementAcceleratesSmoothly()
        {
            ArenaSim sim = TestHelpers.Sim(0, true);
            float moveSpeed = sim.HeroDef.MoveSpeed;

            sim.Step(new HeroInput(1, 0, 0));
            Assert.That(sim.Hero.Velocity.X, Is.GreaterThan(0f));
            Assert.That(sim.Hero.Velocity.X, Is.LessThan(moveSpeed));

            for (int i = 0; i < 30; i++)
            {
                sim.Step(new HeroInput(1, 0, 0));
            }

            Assert.That(sim.Hero.Velocity.X, Is.EqualTo(moveSpeed).Within(1e-4f));
        }
    }
}
