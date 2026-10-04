using NUnit.Framework;
using PersonalArena.Core.Meta;
using PersonalArena.Core.Survivor;

namespace PersonalArena.View.Tests
{
    public sealed class MetaViewLogicTests
    {
        private static SurvivorSim FinishedShortRun(int tier)
        {
            CharacterBuild build = new CharacterBuild { Tier = tier };
            SurvivorSim sim = new SurvivorSim(new SurvivorConfig { RunSeconds = 60f, Build = build }, 11);
            int guard = 0;
            while (!sim.IsEnded && guard++ < 200000)
            {
                sim.Step(new SurvivorInput(0, 0, sim.IsAwaitingPick ? 1 : 0));
            }

            return sim;
        }

        [Test]
        public void SimBecomesAWatchRunResult()
        {
            SurvivorSim sim = FinishedShortRun(2);

            RunResult result = MetaViewLogic.ToRunResult(sim, 2, false);

            Assert.That(sim.IsEnded, Is.True);
            Assert.That(result.SurvivedSeconds, Is.EqualTo(sim.Time));
            Assert.That(result.End, Is.EqualTo(sim.EndReason));
            Assert.That(result.Gold, Is.EqualTo(sim.Gold));
            Assert.That(result.Tier, Is.EqualTo(2));
            Assert.That(result.Farm, Is.False);

            SurvivorRunStats stats = MetaViewLogic.ToRunStats(sim, 11);
            Assert.That(stats.Seed, Is.EqualTo(11));
            Assert.That(stats.Level, Is.EqualTo(sim.Level));
            Assert.That(stats.Kills, Is.EqualTo(sim.Kills));
        }

        [Test]
        public void WatchRunPaysIntoTheWalletAndTheLoadout()
        {
            SurvivorSim sim = FinishedShortRun(1);
            PlayerProfile profile = ProfileRules.NewProfile();
            CharacterProfile warrior = ProfileRules.FindCharacter(profile, ProfileRules.WarriorId);

            RunReward reward = ProfileRules.RecordRun(profile, warrior, 0, MetaViewLogic.ToRunResult(sim, 1, false));

            Assert.That(profile.Gold, Is.EqualTo(reward.GoldAdded));
            Assert.That(warrior.Loadouts[0].Record.Runs, Is.EqualTo(1));
            Assert.That(profile.Stats.FarmRuns, Is.EqualTo(0));
            Assert.That(MetaViewLogic.RewardText(reward), Does.StartWith("+" + MetaViewLogic.FormatGold(reward.GoldAdded) + " gold to wallet"));
        }

        [Test]
        public void RewardTextShowsTheTierUnlock()
        {
            string text = MetaViewLogic.RewardText(new RunReward { GoldAdded = 1234, TierUnlocked = true, UnlockedTier = 3 });

            Assert.That(text, Is.EqualTo("+1,234 gold to wallet\nUnlocked tier 3!"));
            Assert.That(MetaViewLogic.RewardText(null), Does.Contain("earns no gold"));
        }

        [Test]
        public void NewOwnerTrainsOnRandomBuildsButKeepsTheFocus()
        {
            PlayerProfile profile = ProfileRules.NewProfile();
            profile.TrainingFocus = "gold";

            OwnerTraining owner = MetaViewLogic.OwnerTrainingFor(profile);

            Assert.That(MetaViewLogic.UsesOwnerBuild(profile), Is.False);
            Assert.That(owner.Points, Is.Null);
            Assert.That(owner.FocusId, Is.EqualTo("gold"));
        }

        [Test]
        public void LevelledOwnerTrainsOnTheirBuild()
        {
            PlayerProfile profile = ProfileRules.NewProfile();
            CharacterProfile warrior = ProfileRules.FindCharacter(profile, ProfileRules.WarriorId);
            warrior.Level = 2;
            Assert.That(ProfileRules.TryAddPoint(warrior, 0, StatId.MaxHp), Is.True);
            Assert.That(ProfileRules.TryAddPoint(warrior, 0, StatId.MaxHp), Is.True);

            OwnerTraining owner = MetaViewLogic.OwnerTrainingFor(profile);

            Assert.That(owner.Points, Is.Not.Null);
            Assert.That(owner.Points.Length, Is.EqualTo(StatInfo.SlotCount));
            Assert.That(owner.Points[(int)StatId.MaxHp], Is.EqualTo(2));
            Assert.That(owner.Tier, Is.EqualTo(1));
            Assert.That(owner.FocusId, Is.EqualTo("balanced"));
        }

        [Test]
        public void HigherTierAloneAlsoUsesTheOwnerBuild()
        {
            PlayerProfile profile = ProfileRules.NewProfile();
            profile.UnlockedTier = 3;
            profile.SelectedTier = 3;

            OwnerTraining owner = MetaViewLogic.OwnerTrainingFor(profile);

            Assert.That(owner.Points, Is.Not.Null);
            Assert.That(owner.Tier, Is.EqualTo(3));
        }

        [Test]
        public void TrainingBuildLineTexts()
        {
            Assert.That(MetaViewLogic.TrainingBuildLine(true, 3, "gold"), Does.StartWith("Learning your build (tier 3) · Focus: "));
            Assert.That(MetaViewLogic.TrainingBuildLine(false, 0, null), Does.StartWith("Learning random builds · Focus: "));
        }

        [Test]
        public void TrainingChoicesDifferWhenTheOwnerChangesSomething()
        {
            OwnerTraining started = new OwnerTraining { Points = new int[StatInfo.SlotCount], Tier = 2, FocusId = "gold" };
            OwnerTraining same = new OwnerTraining { Points = new int[StatInfo.SlotCount], Tier = 2, FocusId = "gold" };
            Assert.That(MetaViewLogic.TrainingChoicesDiffer(true, 2, "gold", started, same), Is.False);

            OwnerTraining otherFocus = new OwnerTraining { Points = new int[StatInfo.SlotCount], Tier = 2, FocusId = "boss" };
            Assert.That(MetaViewLogic.TrainingChoicesDiffer(true, 2, "gold", started, otherFocus), Is.True);

            OwnerTraining otherTier = new OwnerTraining { Points = new int[StatInfo.SlotCount], Tier = 3, FocusId = "gold" };
            Assert.That(MetaViewLogic.TrainingChoicesDiffer(true, 2, "gold", started, otherTier), Is.True);

            OwnerTraining otherPoints = new OwnerTraining { Points = new int[StatInfo.SlotCount], Tier = 2, FocusId = "gold" };
            otherPoints.Points[0] = 1;
            Assert.That(MetaViewLogic.TrainingChoicesDiffer(true, 2, "gold", started, otherPoints), Is.True);

            OwnerTraining random = new OwnerTraining { FocusId = "gold" };
            Assert.That(MetaViewLogic.TrainingChoicesDiffer(true, 2, "gold", started, random), Is.True);
            Assert.That(MetaViewLogic.TrainingChoicesDiffer(false, 0, "gold", null, random), Is.False);
        }

        [Test]
        public void SameBuildComparesTierAndPoints()
        {
            CharacterBuild a = new CharacterBuild { Tier = 2 };
            CharacterBuild b = a.Clone();
            Assert.That(MetaViewLogic.SameBuild(a, b), Is.True);

            b.Points[1] = 1;
            Assert.That(MetaViewLogic.SameBuild(a, b), Is.False);

            CharacterBuild c = a.Clone();
            c.Tier = 3;
            Assert.That(MetaViewLogic.SameBuild(a, c), Is.False);
        }

        [Test]
        public void WalletTextHasGoldTierAndLoadout()
        {
            PlayerProfile profile = ProfileRules.NewProfile();
            profile.Gold = 12345;

            string text = MetaViewLogic.WalletText(profile);

            Assert.That(text, Does.StartWith("Wallet: 12,345 gold   Tier 1"));
            Assert.That(text, Does.Contain("Loadout: " + ProfileRules.DefaultLoadoutName(0)));
        }

        [Test]
        public void NumbersUseEnglishSeparators()
        {
            Assert.That(MetaViewLogic.FormatGold(1234567), Is.EqualTo("1,234,567"));
            Assert.That(MetaViewLogic.FormatDecimal(1.5f, "0.0"), Is.EqualTo("1.5"));
            Assert.That(MetaViewLogic.FormatDuration(200f), Is.EqualTo("3m 20s"));
            Assert.That(MetaViewLogic.TierGoldMultiplier(3), Is.EqualTo(2f));
        }
    }
}
