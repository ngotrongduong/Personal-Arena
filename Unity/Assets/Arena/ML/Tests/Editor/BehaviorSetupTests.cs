using NUnit.Framework;
using PersonalArena.Core.Survivor;
using Unity.MLAgents.Policies;
using UnityEngine;

namespace PersonalArena.ML.Tests
{
    public sealed class BehaviorSetupTests
    {
        [Test]
        public void WarriorUsesSchemaV4AndThreeSurvivorBranches()
        {
            var host = new GameObject("BehaviorSetupTest");
            try
            {
                host.SetActive(false);
                BehaviorParameters behavior = host.AddComponent<BehaviorParameters>();

                BehaviorSetup.Configure(behavior, ClassRegistry.WarriorId);

                Assert.AreEqual("Warrior", behavior.BehaviorName);
                Assert.AreEqual(SurvivorObservation.Size, behavior.BrainParameters.VectorObservationSize);
                Assert.AreEqual(SurvivorObservation.Size, behavior.BrainParameters.VectorObservationSize);
                int[] branches = behavior.BrainParameters.ActionSpec.BranchSizes;
                CollectionAssert.AreEqual(new[] { SurvivorInput.MoveBranchSize, SurvivorInput.SkillBranchSize, SurvivorInput.PickBranchSize }, branches);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [TestCase(ClassRegistry.MageId, "Mage")]
        [TestCase(ClassRegistry.ArcherId, "Archer")]
        [TestCase("MAGE", "Mage")]
        public void EveryClassTrainsItsOwnBehaviorWithTheSameShape(string classId, string behaviorName)
        {
            var host = new GameObject("BehaviorSetupTest");
            try
            {
                host.SetActive(false);
                BehaviorParameters behavior = host.AddComponent<BehaviorParameters>();

                BehaviorSetup.Configure(behavior, classId);

                Assert.AreEqual(behaviorName, behavior.BehaviorName);
                Assert.AreEqual(SurvivorObservation.Size, behavior.BrainParameters.VectorObservationSize);
                Assert.AreEqual(1, behavior.BrainParameters.NumStackedVectorObservations);
                CollectionAssert.AreEqual(new[] { SurvivorInput.MoveBranchSize, SurvivorInput.SkillBranchSize, SurvivorInput.PickBranchSize }, behavior.BrainParameters.ActionSpec.BranchSizes);
                Assert.AreEqual(0, behavior.BrainParameters.ActionSpec.NumContinuousActions);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void UnknownClassIsRejectedNotTrainedAsWarrior()
        {
            var host = new GameObject("BehaviorSetupTest");
            try
            {
                host.SetActive(false);
                BehaviorParameters behavior = host.AddComponent<BehaviorParameters>();

                Assert.Throws<System.ArgumentException>(() => BehaviorSetup.Configure(behavior, "paladin"));
                Assert.Throws<System.ArgumentException>(() => ClassRegistry.BehaviorName(null));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void EveryClassKitFitsTheSkillBranch()
        {
            foreach (string id in ClassRegistry.ClassIds)
            {
                SurvivorClassDef kit = ClassRegistry.Create(id);
                Assert.AreEqual(id, kit.Id);
                // Skill branch: 0 = no skill, 1..6 = the six slots (slots 4-5 are `none` until new skills arrive).
                Assert.AreEqual(SurvivorInput.SkillBranchSize - 1, kit.ActiveSkills.Length, id);
            }
        }
    }
}
