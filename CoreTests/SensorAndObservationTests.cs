using System;
using NUnit.Framework;
using PersonalArena.Core;

namespace PersonalArena.CoreTests
{
    public class SensorAndObservationTests
    {
        [Test]
        public void SizeFormulaIncludesCategoriesDistanceAndFlags()
        {
            RaySensor sensor = new RaySensor(new RaySensorConfig { RayCount = 8, ZombieTypeCount = 4 });

            Assert.That(sensor.CategoryCount, Is.EqualTo(7));
            Assert.That(sensor.SizePerRay, Is.EqualTo(11));
            Assert.That(sensor.TotalSize, Is.EqualTo(88));
        }

        [Test]
        public void DefaultObservationSizeIs811()
        {
            Assert.That(new ObservationBuilder().Size, Is.EqualTo(19 + 72 * 11));
        }

        [Test]
        public void RayZeroDetectsZombieSurfaceAndFlags()
        {
            ArenaSim sim = TestHelpers.Sim();
            sim.Hero.Position = new Vec2(10f, 10f);
            sim.Hero.Facing = 0f;
            ZombieState zombie = sim.Zombies[0];
            zombie.Position = new Vec2(15f, 10f);
            zombie.Facing = 0f;
            zombie.StunRemaining = 1f;
            zombie.AttackPhase = ZombieAttackPhase.Windup;

            RaySensor sensor = new RaySensor(new RaySensorConfig
            {
                RayCount = 4,
                MaxDistance = 20f,
                ZombieTypeCount = 4
            });
            float[] buffer = new float[sensor.TotalSize];
            sensor.Write(sim, buffer, 0);

            Assert.That(buffer[2], Is.EqualTo(1f));
            Assert.That(buffer[7], Is.EqualTo(4.55f / 20f).Within(1e-5f));
            Assert.That(buffer[8], Is.EqualTo(1f));
            Assert.That(buffer[9], Is.EqualTo(1f));
            Assert.That(buffer[10], Is.EqualTo(1f));
        }

        [Test]
        public void NothingRayHasDistanceOneAndNearbyEdgeIsDetected()
        {
            ArenaConfig large = new ArenaConfig { Width = 80f, Height = 80f, ZombieCount = 0 };
            ArenaSim empty = new ArenaSim(DefaultDefs.Warrior(), large);
            RaySensor shortSensor = new RaySensor(new RaySensorConfig
            {
                RayCount = 4,
                MaxDistance = 10f,
                ZombieTypeCount = 4
            });
            float[] nothing = new float[shortSensor.TotalSize];
            shortSensor.Write(empty, nothing, 0);
            Assert.That(nothing[0], Is.EqualTo(1f));
            Assert.That(nothing[7], Is.EqualTo(1f));

            ArenaSim small = TestHelpers.Sim(0);
            small.Hero.Position = new Vec2(16f, 10f);
            small.Hero.Facing = 0f;
            float[] edge = new float[shortSensor.TotalSize];
            shortSensor.Write(small, edge, 0);
            Assert.That(edge[shortSensor.EdgeCategory], Is.EqualTo(1f));
            Assert.That(edge[7], Is.EqualTo(0.4f).Within(1e-4f));
        }

        [Test]
        public void EdgeDistanceFollowsTheCircle()
        {
            Vec2 centre = new Vec2(10f, 10f);
            Assert.That(RaySensor.EdgeDistance(centre, new Vec2(1f, 0f), centre, 10f), Is.EqualTo(10f).Within(1e-4f));
            Assert.That(RaySensor.EdgeDistance(new Vec2(16f, 10f), new Vec2(-1f, 0f), centre, 10f),
                Is.EqualTo(16f).Within(1e-4f));
            Assert.That(RaySensor.EdgeDistance(new Vec2(16f, 10f), new Vec2(0f, 1f), centre, 10f),
                Is.EqualTo(8f).Within(1e-4f));
            Assert.That(RaySensor.EdgeDistance(new Vec2(25f, 10f), new Vec2(1f, 0f), centre, 10f), Is.Zero);
        }

        [Test]
        public void RaysSeePotions()
        {
            ArenaSim sim = TestHelpers.Sim(0);
            sim.Hero.Position = new Vec2(10f, 10f);
            sim.Hero.Facing = 0f;
            PotionState potion = sim.Potions[0];
            potion.Active = true;
            potion.Position = new Vec2(13f, 10f);
            potion.Remaining = 5f;

            RaySensor sensor = new RaySensor(new RaySensorConfig { RayCount = 4, MaxDistance = 20f, ZombieTypeCount = 4 });
            float[] buffer = new float[sensor.TotalSize];
            sensor.Write(sim, buffer, 0);

            Assert.That(buffer[sensor.PotionCategory], Is.EqualTo(1f));
            Assert.That(buffer[7], Is.EqualTo((3f - ArenaSim.PotionRadius) / 20f).Within(1e-5f));
        }

        [Test]
        public void HeroBlockDescribesCentreAndDash()
        {
            ArenaSim sim = TestHelpers.Sim(0);
            sim.Hero.Position = new Vec2(15f, 10f);
            sim.Hero.Facing = MathF.PI;
            ObservationBuilder builder = new ObservationBuilder();
            float[] buffer = new float[builder.Size];

            builder.Write(sim, buffer);

            Assert.That(buffer[10], Is.EqualTo(10f / 40f).Within(1e-5f));
            Assert.That(buffer[11], Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(buffer[12], Is.EqualTo(15f / 20f).Within(1e-4f));
            Assert.That(buffer[13], Is.EqualTo(5f / 20f).Within(1e-4f));
            Assert.That(buffer[16], Is.Zero);
            Assert.That(buffer[17], Is.EqualTo(1f).Within(1e-5f));
            Assert.That(buffer[18], Is.EqualTo(0f).Within(1e-5f));

            sim.Step(new HeroInput(0, 0, 4));
            builder.Write(sim, buffer);
            Assert.That(buffer[16], Is.EqualTo(1f));
        }

        [Test]
        public void ObservationSizeDoesNotDependOnZombieCount()
        {
            ObservationBuilder builder = new ObservationBuilder(new RaySensorConfig
            {
                RayCount = 12,
                ZombieTypeCount = 4
            });
            ArenaSim one = TestHelpers.Sim(1);
            ArenaSim sixteen = TestHelpers.Sim(16);
            float[] first = new float[builder.Size];
            float[] second = new float[builder.Size];

            builder.Write(one, first);
            builder.Write(sixteen, second);

            Assert.That(first.Length, Is.EqualTo(second.Length));
            Assert.That(builder.Size, Is.EqualTo(ObservationBuilder.HeroObservationSize + 12 * 11));
        }
    }
}
