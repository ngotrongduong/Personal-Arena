using NUnit.Framework;
using PersonalArena.Core;
using PersonalArena.Core.Survivor;

namespace PersonalArena.ML.Tests
{
    public sealed class SurvivorEnvFactoryTests
    {
        [TestCase(180f, 180f)]
        [TestCase(10f, 60f)]
        [TestCase(5000f, 900f)]
        [TestCase(float.NaN, 900f)]
        public void RunSecondsIsClamped(float value, float expected)
        {
            Assert.AreEqual(expected, SurvivorEnvFactory.RunSeconds(value));
        }

        [Test]
        public void DefaultParametersGiveTierOneEmptyBuild()
        {
            CharacterBuild build = SurvivorEnvFactory.CreateBuild(new Rng(1), 1f, 1f, 0f, 0f, null);

            Assert.AreEqual(1, build.Tier);
            Assert.AreEqual(0, build.Level);
        }

        [Test]
        public void TiersAreClampedAndOrdered()
        {
            CharacterBuild build = SurvivorEnvFactory.CreateBuild(new Rng(3), 14f, 2f, 0f, 0f, null);

            Assert.AreEqual(10, build.Tier);
        }

        [Test]
        public void FullOwnBuildShareReturnsACopyOfTheOwnerBuild()
        {
            var own = new CharacterBuild { Tier = 4 };
            own.Points[0] = 5;
            own.Points[4] = 3;

            CharacterBuild build = SurvivorEnvFactory.CreateBuild(new Rng(5), 1f, 1f, 30f, 1f, own);

            Assert.AreNotSame(own, build);
            Assert.AreEqual(4, build.Tier);
            Assert.AreEqual(5, build.Points[0]);
            Assert.AreEqual(3, build.Points[4]);
        }

        [Test]
        public void RandomBuildsRespectLimits()
        {
            var rng = new Rng(11);
            for (int i = 0; i < 200; i++)
            {
                CharacterBuild build = SurvivorEnvFactory.CreateBuild(rng, 2f, 6f, 20f, 0f, null);

                Assert.LessOrEqual(build.Level, 20);
                Assert.GreaterOrEqual(build.Tier, 2);
                Assert.LessOrEqual(build.Tier, 6);
                build.Validate();
            }
        }

        [Test]
        public void OnlyWarriorHasASurvivorKit()
        {
            Assert.AreEqual("warrior", SurvivorEnvFactory.CreateClass("warrior").Id);
            Assert.Throws<System.ArgumentException>(() => SurvivorEnvFactory.CreateClass("mage"));
        }
    }
}
