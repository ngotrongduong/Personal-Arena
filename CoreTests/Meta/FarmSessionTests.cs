using System;
using NUnit.Framework;
using PersonalArena.Core.Meta;
using PersonalArena.Core.Survivor;
using PersonalArena.Core.Tests.Survivor;

namespace PersonalArena.Core.Tests.Meta
{
    public sealed class FarmSessionTests
    {
        [Test]
        public void TwoRuns_CountsResultsAndGold()
        {
            CharacterBuild build = new CharacterBuild { Tier = 2 };
            build.Points[(int)StatId.Greed] = 3;
            FarmSession session = new FarmSession(SurvivorTestHelpers.Brain(favouredMove: 1), build, 2, 100, 60f);
            build.Points[(int)StatId.Greed] = 0; // the session keeps its own copy
            Assert.That(session.RunCount, Is.EqualTo(2)); Assert.That(session.BaseSeed, Is.EqualTo(100));
            Assert.That(session.Completed, Is.EqualTo(0)); Assert.That(session.IsDone, Is.False);

            Assert.That(session.RunNext(), Is.True);
            Assert.That(session.Completed, Is.EqualTo(1)); Assert.That(session.IsDone, Is.False);
            Assert.That(session.RunNext(), Is.True);
            Assert.That(session.Completed, Is.EqualTo(2)); Assert.That(session.IsDone, Is.True);
            Assert.That(session.RunNext(), Is.False); Assert.That(session.Completed, Is.EqualTo(2));

            Assert.That(session.Results, Has.Count.EqualTo(2));
            float sum = 0f;
            foreach (SurvivorRunStats stats in session.Results)
            {
                // A run that reaches the time limit ends on the first tick past it (float time adds up to 60.016).
                Assert.That(stats.SurvivedSeconds, Is.GreaterThan(0f).And.LessThanOrEqualTo(60f + 0.05f));
                Assert.That(stats.EndReason, Is.Not.EqualTo(EndReason.None));
                sum += stats.Gold;
            }
            Assert.That(session.TotalGold, Is.EqualTo(sum).Within(1e-3f));
            Assert.That(session.IsCancelled, Is.False);
        }

        [Test]
        public void SameSeeds_SameResults()
        {
            FarmSession a = new FarmSession(SurvivorTestHelpers.Brain(favouredMove: 1), new CharacterBuild { Tier = 1 }, 1, 7, 60f);
            FarmSession b = new FarmSession(SurvivorTestHelpers.Brain(favouredMove: 1), new CharacterBuild { Tier = 1 }, 1, 7, 60f);
            while (a.RunNext()) { }
            while (b.RunNext()) { }
            Assert.That(a.Results[0].SurvivedSeconds, Is.EqualTo(b.Results[0].SurvivedSeconds));
            Assert.That(a.Results[0].Gold, Is.EqualTo(b.Results[0].Gold));
            Assert.That(a.TotalGold, Is.EqualTo(b.TotalGold));
        }

        [Test]
        public void Cancel_StopsBeforeTheNextRun()
        {
            FarmSession session = new FarmSession(SurvivorTestHelpers.Brain(favouredMove: 1), new CharacterBuild { Tier = 1 }, 5, 1, 60f);
            Assert.That(session.RunNext(), Is.True);
            session.Cancel();
            Assert.That(session.IsCancelled, Is.True);
            Assert.That(session.IsDone, Is.False, "only the worker sets done");
            Assert.That(session.RunNext(), Is.False);
            Assert.That(session.IsDone, Is.True); Assert.That(session.Completed, Is.EqualTo(1)); Assert.That(session.Results, Has.Count.EqualTo(1));
            Assert.That(session.RunNext(), Is.False);

            FarmSession early = new FarmSession(SurvivorTestHelpers.Brain(), new CharacterBuild { Tier = 1 }, 3, 1, 60f);
            early.Cancel();
            Assert.That(early.RunNext(), Is.False); Assert.That(early.IsDone, Is.True); Assert.That(early.Completed, Is.EqualTo(0));
            Assert.That(early.TotalGold, Is.EqualTo(0f));
        }

        [Test]
        public void ClassId_PicksThatClassKit()
        {
            FarmSession warrior = new FarmSession(SurvivorTestHelpers.Brain(favouredMove: 1), new CharacterBuild { Tier = 1 }, 1, 7, 60f);
            Assert.That(warrior.ClassId, Is.EqualTo(ProfileRules.WarriorId));
            while (warrior.RunNext()) { }
            Assert.That(warrior.Results[0].FinalItemLevels[SurvivorCatalog.SweepIndex], Is.GreaterThanOrEqualTo(1));

            FarmSession mage = new FarmSession(SurvivorTestHelpers.Brain(favouredMove: 1), new CharacterBuild { Tier = 1 }, 1, 7, 60f, ProfileRules.MageId);
            Assert.That(mage.ClassId, Is.EqualTo(ProfileRules.MageId));
            while (mage.RunNext()) { }
            Assert.That(mage.Error, Is.Null);
            Assert.That(mage.Results[0].FinalItemLevels[SurvivorCatalog.MagicBoltIndex], Is.GreaterThanOrEqualTo(1), "the mage starts with its own weapon");
            Assert.That(mage.Results[0].FinalItemLevels[SurvivorCatalog.SweepIndex], Is.EqualTo(0), "never the Warrior kit");

            FarmSession archer = new FarmSession(SurvivorTestHelpers.Brain(favouredMove: 1), new CharacterBuild { Tier = 1 }, 1, 7, 60f, ProfileRules.ArcherId);
            while (archer.RunNext()) { }
            Assert.That(archer.Results[0].FinalItemLevels[SurvivorCatalog.ArrowIndex], Is.GreaterThanOrEqualTo(1));
            Assert.That(archer.Results[0].FinalItemLevels[SurvivorCatalog.SweepIndex], Is.EqualTo(0));

            Assert.Throws<ArgumentException>(() => new FarmSession(SurvivorTestHelpers.Brain(), new CharacterBuild { Tier = 1 }, 1, 1, 60f, "paladin"));
        }

        [Test]
        public void Constructor_ChecksArguments()
        {
            PolicyBrain brain = SurvivorTestHelpers.Brain();
            CharacterBuild build = new CharacterBuild { Tier = 1 };
            Assert.Throws<ArgumentOutOfRangeException>(() => new FarmSession(brain, build, 0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FarmSession(brain, build, FarmSession.MaxRuns + 1, 1));
            Assert.DoesNotThrow(() => new FarmSession(brain, build, FarmSession.MinRuns, 1));
            Assert.DoesNotThrow(() => new FarmSession(brain, build, FarmSession.MaxRuns, 1));
            Assert.Throws<ArgumentNullException>(() => new FarmSession(null, build, 1, 1));
            Assert.Throws<ArgumentNullException>(() => new FarmSession(brain, null, 1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FarmSession(brain, new CharacterBuild { Tier = 11 }, 1, 1));
            CharacterBuild overCap = new CharacterBuild { Tier = 1 };
            overCap.Points[(int)StatId.Might] = StatInfo.Cap(StatId.Might) + 1;
            Assert.That(() => new FarmSession(brain, overCap, 1, 1), Throws.InstanceOf<ArgumentException>());
        }
    }
}
