using System;
using NUnit.Framework;
using PersonalArena.Core;

namespace PersonalArena.CoreTests
{
    public class M3CoreTests
    {
        [Test]
        public void Runner_IsFasterThanWalker()
        {
            ArenaSim walker = Sim(DefaultDefs.Warrior(), DefaultDefs.Walker());
            ArenaSim runner = Sim(DefaultDefs.Warrior(), DefaultDefs.Runner());
            PlaceChaser(walker);
            PlaceChaser(runner);
            float walkerStart = walker.Zombies[0].Position.X;
            float runnerStart = runner.Zombies[0].Position.X;

            Step(walker, 60);
            Step(runner, 60);

            Assert.That(walker.Zombies[0].Position.X - walkerStart, Is.GreaterThan(2f));
            Assert.That(runner.Zombies[0].Position.X - runnerStart,
                Is.GreaterThan(walker.Zombies[0].Position.X - walkerStart));
        }

        [Test]
        public void Brute_ResistsKnockback()
        {
            ArenaSim walker = Sim(DefaultDefs.Warrior(), DefaultDefs.Walker());
            ArenaSim brute = Sim(DefaultDefs.Warrior(), DefaultDefs.Brute());
            PlaceKickTarget(walker);
            PlaceKickTarget(brute);
            float walkerStart = walker.Zombies[0].Position.X;
            float bruteStart = brute.Zombies[0].Position.X;

            walker.Step(new HeroInput(0, 0, 2));
            brute.Step(new HeroInput(0, 0, 2));
            Step(walker, 50);
            Step(brute, 50);

            float walkerDistance = walker.Zombies[0].Position.X - walkerStart;
            float bruteDistance = brute.Zombies[0].Position.X - bruteStart;
            Assert.That(bruteDistance / walkerDistance, Is.EqualTo(0.4f).Within(0.06f));
        }

        [Test]
        public void Spitter_KeepsDistance()
        {
            ArenaSim sim = Sim(DefaultDefs.Warrior(), DefaultDefs.Spitter());
            PlaceSpitter(sim);

            for (int i = 0; i < 300; i++)
            {
                sim.Step(default);
                Assert.That(sim.Zombies[0].FellOff, Is.False);
            }

            float distance = Vec2.Distance(sim.Zombies[0].Position, sim.Hero.Position);
            Assert.That(distance, Is.InRange(5f, 9f));
            Assert.That(sim.Zombies[0].Alive, Is.True);
        }

        [Test]
        public void Spitter_RetreatsButNeverWalksOffTheRim()
        {
            ArenaSim sim = Sim(DefaultDefs.Warrior(), DefaultDefs.Spitter());
            Vec2 center = sim.Config.Center;
            float radius = sim.Config.Radius;
            ZombieState spitter = sim.Zombies[0];
            spitter.Position = center + new Vec2(radius - 1f, 0f);
            spitter.Facing = MathF.PI;
            sim.Hero.Position = center + new Vec2(radius - 4f, 0f);

            for (int i = 0; i < 300; i++)
            {
                sim.Step(default);
                Assert.That(spitter.Alive, Is.True);
                Assert.That(spitter.FellOff, Is.False);
            }

            Assert.That(Vec2.Distance(spitter.Position, center),
                Is.LessThanOrEqualTo(radius - spitter.Def.Radius + 1e-4f));
        }

        [Test]
        public void Spitter_DoesNotWalkWhileKnockedBack()
        {
            ArenaSim sim = Sim(DefaultDefs.Warrior(), DefaultDefs.Spitter());
            PlaceSpitter(sim);
            ZombieState spitter = sim.Zombies[0];
            spitter.Position = sim.Hero.Position + new Vec2(3f, 0f);
            spitter.KnockbackVelocity = new Vec2(0f, 4f);
            Vec2 start = spitter.Position;

            sim.Step(default);

            // Too close to the hero, so it would retreat along +X; while knocked back it only drifts.
            Assert.That(spitter.Position.X, Is.EqualTo(start.X).Within(1e-4f));
            Assert.That(spitter.Position.Y, Is.GreaterThan(start.Y));
        }

        [Test]
        public void Mage_ManaShieldBlocksProjectilesFromBehind()
        {
            ArenaSim sim = Sim(DefaultDefs.Mage(), DefaultDefs.Spitter());
            PlaceSpitter(sim);
            sim.Hero.Facing = MathF.PI;

            bool blocked = StepUntilEvent(sim, SimEventType.ProjectileBlocked, 180, new HeroInput(0, 0, 3));

            Assert.That(blocked, Is.True);
            Assert.That(sim.Hero.Hp, Is.EqualTo(sim.HeroDef.MaxHp));
        }

        [Test]
        public void Spitter_ProjectileHitsHero()
        {
            ArenaSim sim = Sim(DefaultDefs.Warrior(), DefaultDefs.Spitter());
            PlaceSpitter(sim);

            bool damaged = StepUntilEvent(sim, SimEventType.HeroDamaged, 180, default);

            Assert.That(damaged, Is.True);
            Assert.That(sim.Hero.Hp, Is.EqualTo(85f));
        }

        [Test]
        public void Spitter_ProjectileBlockedByFrontalShield()
        {
            ArenaSim sim = Sim(DefaultDefs.Warrior(), DefaultDefs.Spitter());
            PlaceSpitter(sim);
            sim.Hero.Facing = 0f;

            bool blocked = StepUntilEvent(sim, SimEventType.ProjectileBlocked, 180, new HeroInput(0, 0, 3));

            Assert.That(blocked, Is.True);
            Assert.That(sim.Hero.Hp, Is.EqualTo(sim.HeroDef.MaxHp));
        }

        [Test]
        public void Spitter_ProjectileHitsWhenBlockingAway()
        {
            ArenaSim sim = Sim(DefaultDefs.Warrior(), DefaultDefs.Spitter());
            PlaceSpitter(sim);
            sim.Hero.Facing = MathF.PI;

            bool damaged = StepUntilEvent(sim, SimEventType.HeroDamaged, 180, new HeroInput(0, 0, 3));

            Assert.That(damaged, Is.True);
            Assert.That(sim.Hero.Hp, Is.EqualTo(85f));
            Assert.That(TestHelpers.HasEvent(sim, SimEventType.ProjectileBlocked), Is.False);
        }

        [Test]
        public void Respawn_RerollsZombieType()
        {
            ArenaConfig config = Config(1, true);
            config.PotionDropChance = 0f;
            config.ZombieSpawns = new[]
            {
                new ZombieSpawnEntry(DefaultDefs.Walker(), 1f),
                new ZombieSpawnEntry(DefaultDefs.Runner(), 1f)
            };
            ArenaSim sim = new ArenaSim(DefaultDefs.Warrior(), config);
            bool sawWalker = false;
            bool sawRunner = false;

            for (int kill = 0; kill < 20; kill++)
            {
                ZombieState zombie = sim.Zombies[0];
                sawWalker |= zombie.Def.TypeIndex == 0;
                sawRunner |= zombie.Def.TypeIndex == 1;
                sim.Hero.Position = new Vec2(20f, 20f);
                sim.Hero.Facing = 0f;
                zombie.Position = new Vec2(21f, 20f);
                zombie.Facing = 0f;
                zombie.StunRemaining = 0f;
                sim.Step(new HeroInput(0, 0, 1));
                Assert.That(zombie.Alive, Is.False);
                for (int i = 0; i < 100 && !zombie.Alive; i++)
                {
                    sim.Step(default);
                }

                Assert.That(zombie.Alive, Is.True);
            }

            Assert.That(sawWalker, Is.True);
            Assert.That(sawRunner, Is.True);
        }

        [Test]
        public void Mage_FireballDamagesAndSplashes()
        {
            ArenaSim sim = Sim(DefaultDefs.Mage(), DefaultDefs.Walker(), 2);
            sim.Hero.Position = new Vec2(10f, 20f);
            sim.Hero.Facing = 0f;
            sim.Zombies[0].Position = new Vec2(15f, 20f);
            sim.Zombies[1].Position = new Vec2(15f, 21.2f);
            FreezeZombies(sim);

            sim.Step(new HeroInput(0, 0, 1));
            Step(sim, 30);

            Assert.That(sim.Zombies[0].Hp, Is.EqualTo(60f));
            Assert.That(sim.Zombies[1].Hp, Is.EqualTo(80f));
        }

        [Test]
        public void Mage_FrostNovaSlowsAndPushes()
        {
            ArenaSim sim = Sim(DefaultDefs.Mage(), DefaultDefs.Walker(), 2);
            sim.Hero.Position = new Vec2(20f, 20f);
            sim.Zombies[0].Position = new Vec2(22f, 20f);
            sim.Zombies[1].Position = new Vec2(26f, 20f);
            float nearStart = sim.Zombies[0].Position.X;

            sim.Step(new HeroInput(0, 0, 2));

            Assert.That(sim.Zombies[0].Hp, Is.EqualTo(90f));
            Assert.That(sim.Zombies[0].SlowFactor, Is.EqualTo(0.4f));
            Assert.That(sim.Zombies[0].Position.X, Is.GreaterThan(nearStart));
            Assert.That(sim.Zombies[0].KnockbackVelocity.X, Is.GreaterThan(0f));
            Assert.That(sim.Zombies[1].Hp, Is.EqualTo(100f));
            Assert.That(sim.Zombies[1].SlowFactor, Is.EqualTo(1f));
            Assert.That(sim.Zombies[1].KnockbackVelocity, Is.EqualTo(Vec2.Zero));
        }

        [Test]
        public void Mage_BlinkStaysOnPlatform()
        {
            ArenaSim sim = Sim(DefaultDefs.Mage(), DefaultDefs.Walker(), 0);
            sim.Hero.Position = new Vec2(29f, 20f);
            sim.Hero.Facing = 0f;

            sim.Step(new HeroInput(0, 0, 4));

            Assert.That(sim.IsOverAbyss(sim.Hero.Position), Is.False);
            Assert.That(Vec2.Distance(sim.Hero.Position, sim.Config.Center),
                Is.LessThanOrEqualTo(sim.Config.Radius - sim.HeroDef.Radius - 0.3f + 1e-4f));
            Assert.That(TestHelpers.HasEvent(sim, SimEventType.HeroTeleported), Is.True);
        }

        [Test]
        public void Mage_ManaShieldReducesFromBehindAndNeverParries()
        {
            ArenaSim sim = Sim(DefaultDefs.Mage(), DefaultDefs.Walker());
            sim.Hero.Position = new Vec2(20f, 20f);
            sim.Hero.Facing = MathF.PI * 0.5f;
            ZombieState zombie = sim.Zombies[0];
            zombie.Position = new Vec2(20f, 19f);
            zombie.Facing = MathF.PI * 0.5f;
            zombie.AttackPhase = ZombieAttackPhase.Windup;
            zombie.AttackTimer = 0f;

            sim.Step(new HeroInput(0, 0, 3));

            Assert.That(sim.Hero.Hp, Is.EqualTo(75f));
            Assert.That(TestHelpers.HasEvent(sim, SimEventType.Parry), Is.False);
            Assert.That(TestHelpers.HasEvent(sim, SimEventType.BlockedHit), Is.True);
        }

        [Test]
        public void Archer_ArrowHitsDistantZombie()
        {
            ArenaSim sim = Sim(DefaultDefs.Archer(), DefaultDefs.Walker());
            PlaceRangedTarget(sim, 15f);
            FreezeZombies(sim);

            sim.Step(new HeroInput(0, 0, 1));
            Step(sim, 50);

            Assert.That(sim.Zombies[0].Hp, Is.EqualTo(78f));
        }

        [Test]
        public void Archer_PiercingArrowHitsTwoInLine()
        {
            ArenaSim sim = Sim(DefaultDefs.Archer(), DefaultDefs.Walker(), 2);
            sim.Hero.Position = new Vec2(5f, 20f);
            sim.Hero.Facing = 0f;
            sim.Zombies[0].Position = new Vec2(10f, 20f);
            sim.Zombies[1].Position = new Vec2(15f, 20f);
            FreezeZombies(sim);

            sim.Step(new HeroInput(0, 0, 2));
            Step(sim, 50);

            Assert.That(sim.Zombies[0].Hp, Is.EqualTo(55f));
            Assert.That(sim.Zombies[1].Hp, Is.EqualTo(55f));
        }

        [Test]
        public void Archer_LeapBackMovesOppositeFacing()
        {
            ArenaSim sim = new ArenaSim(DefaultDefs.Archer(), Config(0, true));
            Vec2 start = sim.Hero.Position;
            sim.Hero.Facing = 0f;

            sim.Step(new HeroInput(1, 0, 3));
            Step(sim, 20, new HeroInput(1, 0, 0));

            Assert.That(sim.Hero.Position.X, Is.LessThan(start.X - 3.5f));
            Assert.That(sim.Hero.Position.Y, Is.EqualTo(start.Y).Within(1e-4f));
        }

        [Test]
        public void Archer_ConcussiveArrowStunsAndPushes()
        {
            ArenaSim sim = Sim(DefaultDefs.Archer(), DefaultDefs.Walker());
            PlaceRangedTarget(sim, 5f);

            sim.Step(new HeroInput(0, 0, 4));
            bool hit = StepUntilEvent(sim, SimEventType.ProjectileHit, 40, default);

            Assert.That(hit, Is.True);
            Assert.That(sim.Zombies[0].Hp, Is.EqualTo(90f));
            Assert.That(sim.Zombies[0].StunRemaining, Is.GreaterThan(0.9f));
            Assert.That(sim.Zombies[0].KnockbackVelocity.X, Is.GreaterThan(0f));
        }

        [Test]
        public void FastProjectile_DoesNotTunnel()
        {
            ZombieTypeDef tiny = DefaultDefs.Walker();
            tiny.Radius = 0.01f;
            tiny.MoveSpeed = 0f;
            ArenaSim sim = Sim(DefaultDefs.Archer(), tiny);
            sim.Hero.Position = new Vec2(20f, 20f);
            sim.Hero.Facing = 0f;
            sim.Zombies[0].Position = new Vec2(21f, 20f);

            sim.Step(new HeroInput(0, 0, 2));

            Assert.That(sim.Zombies[0].Hp, Is.EqualTo(55f));
            Assert.That(TestHelpers.HasEvent(sim, SimEventType.ProjectileHit), Is.True);
        }

        [Test]
        public void Determinism_MixedZombiesAndProjectiles()
        {
            ArenaSim first = MixedSim(1234);
            ArenaSim second = MixedSim(1234);
            int fired = 0;
            int hits = 0;

            for (int tick = 0; tick < 600; tick++)
            {
                int skill = tick % 120 == 0 ? 2 : tick % 31 == 0 ? 1 : 0;
                HeroInput input = new HeroInput((tick / 23) % 9, tick % 29 == 0 ? 1 : 0, skill);
                first.Step(input);
                second.Step(input);
                Assert.That(second.Events.Count, Is.EqualTo(first.Events.Count), "events at tick " + tick);
                fired += TestHelpers.HasEvent(first, SimEventType.ProjectileFired) ? 1 : 0;
                hits += TestHelpers.HasEvent(first, SimEventType.ProjectileHit) ? 1 : 0;
            }

            Assert.That(fired, Is.GreaterThan(0));
            Assert.That(hits, Is.GreaterThan(0));
            AssertSimStateEqual(first, second);
        }

        private static ArenaSim Sim(HeroClassDef hero, ZombieTypeDef zombie, int count = 1)
        {
            ArenaConfig config = Config(count, false);
            config.ZombieSpawns = new[] { new ZombieSpawnEntry(zombie) };
            return new ArenaSim(hero, config);
        }

        private static ArenaConfig Config(int count, bool respawn)
        {
            return new ArenaConfig
            {
                Width = 40f,
                Height = 40f,
                ZombieCount = count,
                RespawnKilledZombies = respawn,
                PotionDropChance = 0f,
                EpisodeSeconds = 120f,
                Seed = 17
            };
        }

        private static void PlaceChaser(ArenaSim sim)
        {
            sim.Hero.Position = new Vec2(20f, 20f);
            sim.Zombies[0].Position = new Vec2(12f, 20f);
            sim.Zombies[0].Facing = 0f;
        }

        private static void PlaceKickTarget(ArenaSim sim)
        {
            sim.Hero.Position = new Vec2(20f, 20f);
            sim.Hero.Facing = 0f;
            sim.Zombies[0].Position = new Vec2(21.2f, 20f);
            sim.Zombies[0].Facing = MathF.PI;
        }

        private static void PlaceSpitter(ArenaSim sim)
        {
            sim.Hero.Position = new Vec2(20f, 20f);
            sim.Zombies[0].Position = new Vec2(27f, 20f);
            sim.Zombies[0].Facing = MathF.PI;
            sim.Zombies[0].AttackPhase = ZombieAttackPhase.Idle;
            sim.Zombies[0].AttackTimer = 0f;
        }

        private static void PlaceRangedTarget(ArenaSim sim, float distance)
        {
            sim.Hero.Position = new Vec2(5f, 20f);
            sim.Hero.Facing = 0f;
            sim.Zombies[0].Position = new Vec2(5f + distance, 20f);
            sim.Zombies[0].Facing = MathF.PI;
        }

        private static void FreezeZombies(ArenaSim sim)
        {
            for (int i = 0; i < sim.Zombies.Count; i++)
            {
                sim.Zombies[i].StunRemaining = 100f;
            }
        }

        private static void Step(ArenaSim sim, int count, HeroInput input = default)
        {
            for (int i = 0; i < count; i++)
            {
                sim.Step(input);
            }
        }

        private static bool StepUntilEvent(ArenaSim sim, SimEventType type, int count, HeroInput input)
        {
            for (int i = 0; i < count; i++)
            {
                sim.Step(input);
                if (TestHelpers.HasEvent(sim, type))
                {
                    return true;
                }
            }

            return false;
        }

        private static ArenaSim MixedSim(int seed)
        {
            ZombieTypeDef[] types = DefaultDefs.ZombieTypes();
            ArenaConfig config = Config(4, true);
            config.Seed = seed;
            config.ZombieSpawns = new[]
            {
                new ZombieSpawnEntry(types[0]),
                new ZombieSpawnEntry(types[1]),
                new ZombieSpawnEntry(types[2]),
                new ZombieSpawnEntry(types[3])
            };
            ArenaSim sim = new ArenaSim(DefaultDefs.Archer(), config);
            for (int i = 0; i < types.Length; i++)
            {
                sim.Zombies[i].Def = types[i];
                sim.Zombies[i].Hp = types[i].MaxHp;
            }

            return sim;
        }

        private static void AssertSimStateEqual(ArenaSim first, ArenaSim second)
        {
            Assert.That(first.Hero.Position, Is.EqualTo(second.Hero.Position));
            Assert.That(first.Hero.Velocity, Is.EqualTo(second.Hero.Velocity));
            Assert.That(first.Hero.Hp, Is.EqualTo(second.Hero.Hp));
            Assert.That(first.Hero.Energy, Is.EqualTo(second.Hero.Energy));
            Assert.That(first.Done, Is.EqualTo(second.Done));
            for (int i = 0; i < first.Zombies.Count; i++)
            {
                Assert.That(first.Zombies[i].Def.TypeIndex, Is.EqualTo(second.Zombies[i].Def.TypeIndex));
                Assert.That(first.Zombies[i].Position, Is.EqualTo(second.Zombies[i].Position));
                Assert.That(first.Zombies[i].Hp, Is.EqualTo(second.Zombies[i].Hp));
                Assert.That(first.Zombies[i].AttackPhase, Is.EqualTo(second.Zombies[i].AttackPhase));
                Assert.That(first.Zombies[i].AttackTimer, Is.EqualTo(second.Zombies[i].AttackTimer));
            }

            for (int i = 0; i < first.Projectiles.Count; i++)
            {
                Assert.That(first.Projectiles[i].Active, Is.EqualTo(second.Projectiles[i].Active));
                Assert.That(first.Projectiles[i].Id, Is.EqualTo(second.Projectiles[i].Id));
                Assert.That(first.Projectiles[i].Position, Is.EqualTo(second.Projectiles[i].Position));
                Assert.That(first.Projectiles[i].RemainingSeconds,
                    Is.EqualTo(second.Projectiles[i].RemainingSeconds));
                Assert.That(first.Projectiles[i].HitMask, Is.EqualTo(second.Projectiles[i].HitMask));
            }
        }
    }
}
