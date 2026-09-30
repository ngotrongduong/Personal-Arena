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
                Assert.AreEqual(2264, behavior.BrainParameters.VectorObservationSize);
                int[] branches = behavior.BrainParameters.ActionSpec.BranchSizes;
                CollectionAssert.AreEqual(new[] { 9, 5, 5 }, branches);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }
    }
}
