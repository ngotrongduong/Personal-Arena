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
        public void ZeroReviewSharesPreserveTheOldBuildRngSequence()
        {
            CharacterBuild expected = SurvivorEnvFactory.CreateBuild(new Rng(37), 2f, 6f, 20f, 0f, null);

            SurvivorEpisode episode = SurvivorEnvFactory.CreateEpisode(
                new Rng(37), 2f, 6f, 20f, 0f, 0f, 0f, null);

            Assert.AreEqual(SurvivorEpisodeKind.New, episode.Kind);
            Assert.AreEqual(expected.Tier, episode.Build.Tier);
            Assert.AreEqual(expected.Level, episode.Build.Level);
            for (int i = 0; i < expected.Points.Length; i++)
            {
                Assert.AreEqual(expected.Points[i], episode.Build.Points[i]);
            }
        }

        [Test]
        public void HardEpisodeRaisesTierAndStartsSurrounded()
        {
            SurvivorEpisode episode = SurvivorEnvFactory.CreateEpisode(
                new Rng(11), 1f, 6f, 25f, 0f, 0f, 1f, null);

            Assert.AreEqual(SurvivorEpisodeKind.Hard, episode.Kind);
            Assert.AreEqual(7, episode.Build.Tier);
            Assert.LessOrEqual(episode.Build.Level, 25);
            Assert.AreEqual(16, episode.OpeningRing);
        }

        [Test]
        public void ReviewEpisodeIsTheBlankTierOneBaseline()
        {
            SurvivorEpisode episode = SurvivorEnvFactory.CreateEpisode(
                new Rng(11), 4f, 9f, 50f, 1f, 1f, 0f, new CharacterBuild { Tier = 8 });

            Assert.AreEqual(SurvivorEpisodeKind.Review, episode.Kind);
            Assert.AreEqual(1, episode.Build.Tier);
            Assert.AreEqual(0, episode.Build.Level);
            Assert.AreEqual(0, episode.OpeningRing);
        }

        [Test]
        public void EpisodeSharesAreClampedAndScaled()
        {
            var rng = new Rng(91);
            int hard = 0;
            int review = 0;
            for (int i = 0; i < 1000; i++)
            {
                SurvivorEpisode episode = SurvivorEnvFactory.CreateEpisode(
                    rng, 1f, 1f, 0f, 0f, 5f, 5f, null);
                if (episode.Kind == SurvivorEpisodeKind.Hard) hard++;
                if (episode.Kind == SurvivorEpisodeKind.Review) review++;
                Assert.AreNotEqual(SurvivorEpisodeKind.New, episode.Kind);
            }

            Assert.Greater(hard, 400);
            Assert.Greater(review, 400);
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
