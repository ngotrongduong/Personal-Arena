using System.Collections.Generic;
using NUnit.Framework;
using PersonalArena.Core.Survivor;
using Unity.MLAgents.Actuators;

namespace PersonalArena.ML.Tests
{
    public sealed class SurvivorActionMapperTests
    {
        [Test]
        public void FromBranchesKeepsValidIndices()
        {
            SurvivorInput input = SurvivorActionMapper.FromBranches(8, 3, 2);

            Assert.AreEqual(8, input.Move);
            Assert.AreEqual(3, input.Skill);
            Assert.AreEqual(2, input.Pick);
        }

        [Test]
        public void FromBranchesTurnsOutOfRangeIntoNoOp()
        {
            SurvivorInput input = SurvivorActionMapper.FromBranches(9, -1, 5);

            Assert.AreEqual(0, input.Move);
            Assert.AreEqual(0, input.Skill);
            Assert.AreEqual(0, input.Pick);
        }

        [TestCase(0, 0, 0)]
        [TestCase(1, 0, 1)]
        [TestCase(1, 1, 2)]
        [TestCase(0, 1, 3)]
        [TestCase(-1, 0, 5)]
        [TestCase(0, -1, 7)]
        [TestCase(1, -1, 8)]
        public void MoveFromAxesMatchesSimDirections(int dx, int dy, int expected)
        {
            Assert.AreEqual(expected, SurvivorActionMapper.MoveFromAxes(dx, dy));
        }

        [Test]
        public void MaskDuringCombatAllowsEveryMoveAndOnlyPickZero()
        {
            var sim = new SurvivorSim(new SurvivorConfig(), 7);
            var mask = new FakeActionMask();

            SurvivorActionMapper.WriteMask(mask, sim, new bool[SurvivorInput.MoveBranchSize], new bool[SurvivorInput.SkillBranchSize], new bool[SurvivorInput.PickBranchSize]);

            Assert.IsFalse(sim.IsAwaitingPick);
            for (int i = 0; i < SurvivorInput.MoveBranchSize; i++)
            {
                Assert.IsTrue(mask.IsEnabled(SurvivorActionMapper.MoveBranch, i), "move " + i);
            }

            Assert.IsTrue(mask.IsEnabled(SurvivorActionMapper.SkillBranch, 0));
            Assert.IsTrue(mask.IsEnabled(SurvivorActionMapper.PickBranch, 0));
            for (int i = 1; i < SurvivorInput.PickBranchSize; i++)
            {
                Assert.IsFalse(mask.IsEnabled(SurvivorActionMapper.PickBranch, i), "pick " + i);
            }
        }

        [Test]
        public void ApplyMaskKeepsNoOpWhenBranchIsFullyDisabled()
        {
            var mask = new FakeActionMask();

            SurvivorActionMapper.ApplyMask(mask, new bool[SurvivorInput.MoveBranchSize], new bool[SurvivorInput.SkillBranchSize], new[] { false, true, true, false, false });

            Assert.IsTrue(mask.IsEnabled(SurvivorActionMapper.MoveBranch, 0));
            Assert.IsFalse(mask.IsEnabled(SurvivorActionMapper.MoveBranch, 1));
            Assert.IsTrue(mask.IsEnabled(SurvivorActionMapper.SkillBranch, 0));
            Assert.IsFalse(mask.IsEnabled(SurvivorActionMapper.PickBranch, 0));
            Assert.IsTrue(mask.IsEnabled(SurvivorActionMapper.PickBranch, 1));
            Assert.IsTrue(mask.IsEnabled(SurvivorActionMapper.PickBranch, 2));
        }

        private sealed class FakeActionMask : IDiscreteActionMask
        {
            private readonly Dictionary<(int, int), bool> values = new Dictionary<(int, int), bool>();

            public void SetActionEnabled(int branch, int actionIndex, bool isEnabled)
            {
                values[(branch, actionIndex)] = isEnabled;
            }

            public bool IsEnabled(int branch, int actionIndex)
            {
                return values.TryGetValue((branch, actionIndex), out bool enabled) ? enabled : true;
            }
        }
    }
}
