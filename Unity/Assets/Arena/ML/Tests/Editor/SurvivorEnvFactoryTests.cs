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
                new Rng(11), 4f, 9f, 50f, 0f, 1f, 0f, new CharacterBuild { Tier = 8 });

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
        public void FullOwnBuildShareReturnsAJitteredCopyOfTheOwnerBuild()
        {
            var own = new CharacterBuild { Tier = 4 };
            own.Points[0] = 5;
            own.Points[4] = 3;

            var rng = new Rng(5);
            for (int i = 0; i < 200; i++)
            {
                CharacterBuild build = SurvivorEnvFactory.CreateBuild(rng, 1f, 1f, 30f, 1f, own);

                Assert.AreNotSame(own, build);
                Assert.AreEqual(4, build.Tier);
                Assert.AreEqual(8, build.Level);
                Assert.LessOrEqual(MovedPoints(own, build), SurvivorEnvFactory.MaximumJitterMoves);
                build.Validate();
            }

            Assert.AreEqual(5, own.Points[0], "The owner's build itself is never changed.");
            Assert.AreEqual(3, own.Points[4]);
        }

        [Test]
        public void JitterKeepsCapsAndSometimesMovesPoints()
        {
            var own = new CharacterBuild { Tier = 2 };
            own.Points[(int)StatId.MaxHp] = StatInfo.Cap(StatId.MaxHp);
            own.Points[(int)StatId.Armor] = StatInfo.Cap(StatId.Armor);
            int changed = 0;
            var rng = new Rng(17);
            for (int i = 0; i < 300; i++)
            {
                CharacterBuild build = SurvivorEnvFactory.Jitter(rng, own);

                build.Validate();
                Assert.AreEqual(own.Level, build.Level);
                for (int slot = StatInfo.UsedCount; slot < StatInfo.SlotCount; slot++)
                {
                    Assert.AreEqual(0, build.Points[slot]);
                }
                if (MovedPoints(own, build) > 0) changed++;
            }

            Assert.Greater(changed, 100);
            Assert.Less(changed, 300);
        }

        [Test]
        public void JitterOfAnEmptyBuildStaysEmpty()
        {
            var own = new CharacterBuild { Tier = 3 };

            CharacterBuild build = SurvivorEnvFactory.Jitter(new Rng(8), own);

            Assert.AreEqual(0, build.Level);
            Assert.AreEqual(3, build.Tier);
        }

        [Test]
        public void JitterIsDeterministicPerSeed()
        {
            var own = new CharacterBuild { Tier = 5 };
            own.Points[3] = 7;
            own.Points[5] = 4;
            own.Points[11] = 2;

            CharacterBuild first = SurvivorEnvFactory.Jitter(new Rng(99), own);
            CharacterBuild second = SurvivorEnvFactory.Jitter(new Rng(99), own);

            CollectionAssert.AreEqual(first.Points, second.Points);
        }

        [Test]
        public void OwnerEpisodesAreMarkedOwn()
        {
            var own = new CharacterBuild { Tier = 6 };
            own.Points[1] = 4;

            SurvivorEpisode episode = SurvivorEnvFactory.CreateEpisode(new Rng(4), 1f, 1f, 0f, 1f, 0f, 0f, own);
            SurvivorEpisode random = SurvivorEnvFactory.CreateEpisode(new Rng(4), 1f, 1f, 0f, 0f, 0f, 0f, own);

            Assert.AreEqual(SurvivorEpisodeKind.Own, episode.Kind);
            Assert.AreEqual(6, episode.Build.Tier);
            Assert.AreEqual(SurvivorEpisodeKind.New, random.Kind);
            Assert.AreEqual(1, random.Build.Tier);
        }

        [Test]
        public void OwnBuildShareIsRoughlyRespected()
        {
            var own = new CharacterBuild { Tier = 2 };
            own.Points[0] = 3;
            var rng = new Rng(123);
            int owner = 0;
            for (int i = 0; i < 2000; i++)
            {
                if (SurvivorEnvFactory.CreateEpisode(rng, 1f, 1f, 10f, 0.7f, 0f, 0f, own).Kind == SurvivorEpisodeKind.Own)
                {
                    owner++;
                }
            }

            Assert.That(owner, Is.InRange(1300, 1500));
        }

        [Test]
        public void OwnBuildShareCountsAllEpisodesWithReviewAndHard()
        {
            var own = new CharacterBuild { Tier = 3 };
            own.Points[2] = 5;
            var rng = new Rng(321);
            int owner = 0;
            int review = 0;
            int hard = 0;
            int random = 0;
            for (int i = 0; i < 4000; i++)
            {
                switch (SurvivorEnvFactory.CreateEpisode(rng, 1f, 1f, 10f, 0.7f, 0.2f, 0.1f, own).Kind)
                {
                    case SurvivorEpisodeKind.Own: owner++; break;
                    case SurvivorEpisodeKind.Review: review++; break;
                    case SurvivorEpisodeKind.Hard: hard++; break;
                    default: random++; break;
                }
            }

            // Owner first (70 %), then the other 30 % split 20/10/70: review 6 %, hard 3 %, random 21 %.
            Assert.That(owner, Is.InRange(2640, 2960));
            Assert.That(review, Is.InRange(160, 320));
            Assert.That(hard, Is.InRange(60, 180));
            Assert.That(random, Is.InRange(700, 980));
        }

        private static int MovedPoints(CharacterBuild before, CharacterBuild after)
        {
            int moved = 0;
            for (int i = 0; i < before.Points.Length; i++)
            {
                moved += System.Math.Max(0, after.Points[i] - before.Points[i]);
            }
            return moved;
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
        public void EveryClassHasASurvivorKit()
        {
            Assert.AreEqual("warrior", SurvivorEnvFactory.CreateClass("warrior").Id);
            Assert.AreEqual("mage", SurvivorEnvFactory.CreateClass("Mage").Id);
            Assert.AreEqual("archer", SurvivorEnvFactory.CreateClass("archer").Id);
            Assert.Throws<System.ArgumentException>(() => SurvivorEnvFactory.CreateClass("rogue"));
            foreach (string id in ClassRegistry.ClassIds) Assert.IsTrue(ClassRegistry.HasSurvivorKit(id), id);
            Assert.IsFalse(ClassRegistry.HasSurvivorKit("rogue"));
        }
    }
}
