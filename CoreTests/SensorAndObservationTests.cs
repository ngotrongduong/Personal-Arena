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

            Assert.That(sensor.SizePerRay, Is.EqualTo(10));
            Assert.That(sensor.TotalSize, Is.EqualTo(80));
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
            Assert.That(buffer[6], Is.EqualTo(4.55f / 20f).Within(1e-5f));
            Assert.That(buffer[7], Is.EqualTo(1f));
            Assert.That(buffer[8], Is.EqualTo(1f));
            Assert.That(buffer[9], Is.EqualTo(1f));
        }

        [Test]
        public void NothingRayHasDistanceOneAndNearbyWallIsDetected()
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
            Assert.That(nothing[6], Is.EqualTo(1f));

            ArenaSim small = TestHelpers.Sim(0);
            float[] wall = new float[shortSensor.TotalSize];
            shortSensor.Write(small, wall, 0);
            Assert.That(wall[1], Is.EqualTo(1f));
            Assert.That(wall[6], Is.EqualTo(1f));
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
            Assert.That(builder.Size, Is.EqualTo(ObservationBuilder.HeroObservationSize + 12 * 10));
        }
    }
}
