using NUnit.Framework;
using PersonalArena.Core;
using Unity.MLAgents.Actuators;

namespace PersonalArena.ML.Tests
{
    public sealed class HeroActionMapperTests
    {
        [TestCase(0, 0, 0, 0, -1, 0)]
        [TestCase(4, 1, 2, 4, 0, 2)]
        [TestCase(8, 2, 4, 8, 1, 4)]
        public void FromBranchesMapsAllThreeDiscreteBranches(
            int moveBranch,
            int turnBranch,
            int skillBranch,
            int expectedMove,
            int expectedTurn,
            int expectedSkill)
        {
            HeroInput input = HeroActionMapper.FromBranches(moveBranch, turnBranch, skillBranch);

            Assert.That(input.Move, Is.EqualTo(expectedMove));
            Assert.That(input.Turn, Is.EqualTo(expectedTurn));
            Assert.That(input.Skill, Is.EqualTo(expectedSkill));
        }

        [Test]
        public void SkillMaskAlwaysLeavesNoOpEnabled()
        {
            HeroClassDef warrior = DefaultDefs.Warrior();
            HeroState hero = ReadyHero(warrior);
            hero.Energy = 0f;

            Assert.That(HeroActionMapper.IsSkillActionEnabled(warrior, hero, 0), Is.True);
        }

        [Test]
        public void SkillMaskRejectsNullCooldownAndInsufficientEnergySlots()
        {
            HeroClassDef warrior = DefaultDefs.Warrior();
            HeroState hero = ReadyHero(warrior);

            warrior.Skills[0] = null;
            hero.CooldownRemaining[1] = 0.25f;
            hero.Energy = warrior.Skills[3].EnergyCost - 0.01f;

            Assert.That(HeroActionMapper.IsSkillActionEnabled(warrior, hero, 1), Is.False);
            Assert.That(HeroActionMapper.IsSkillActionEnabled(warrior, hero, 2), Is.False);
            Assert.That(HeroActionMapper.IsSkillActionEnabled(warrior, hero, 4), Is.False);
        }

        [Test]
        public void SkillMaskKeepsHeldBlockEnabledAtZeroEnergy()
        {
            HeroClassDef warrior = DefaultDefs.Warrior();
            HeroState hero = ReadyHero(warrior);
            hero.Energy = 0f;
            hero.IsBlocking = true;

            Assert.That(HeroActionMapper.IsSkillActionEnabled(warrior, hero, 3), Is.True);
        }

        [Test]
        public void WriteSkillMaskWritesOnlyTheSkillBranch()
        {
            HeroClassDef warrior = DefaultDefs.Warrior();
            HeroState hero = ReadyHero(warrior);
            FakeActionMask mask = new FakeActionMask();

            HeroActionMapper.WriteSkillMask(mask, warrior, hero);

            Assert.That(mask.CallCount, Is.EqualTo(HeroInput.SkillBranchSize));
            Assert.That(mask.AllCallsUsedSkillBranch, Is.True);
            Assert.That(mask.Enabled[0], Is.True);
        }

        private static HeroState ReadyHero(HeroClassDef classDef)
        {
            return new HeroState
            {
                Energy = classDef.MaxEnergy,
                Alive = true
            };
        }

        private sealed class FakeActionMask : IDiscreteActionMask
        {
            public readonly bool[] Enabled = new bool[HeroInput.SkillBranchSize];
            public int CallCount { get; private set; }
            public bool AllCallsUsedSkillBranch { get; private set; } = true;

            public void SetActionEnabled(int branch, int actionIndex, bool isEnabled)
            {
                CallCount++;
                AllCallsUsedSkillBranch &= branch == HeroActionMapper.SkillBranch;
                Enabled[actionIndex] = isEnabled;
            }
        }
    }
}
