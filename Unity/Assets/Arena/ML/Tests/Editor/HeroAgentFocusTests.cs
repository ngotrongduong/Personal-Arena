using NUnit.Framework;
using PersonalArena.Core.Survivor;
using Unity.MLAgents.Policies;
using UnityEngine;

namespace PersonalArena.ML.Tests
{
    public sealed class HeroAgentFocusTests
    {
        [Test]
        public void ConfiguredFocusSetsTheRewards()
        {
            GameObject host = new GameObject("HeroAgentFocusTest");
            try
            {
                host.SetActive(false);
                BehaviorSetup.Configure(host.AddComponent<BehaviorParameters>(), ClassRegistry.WarriorId);
                HeroAgent agent = host.AddComponent<HeroAgent>();

                agent.Configure(0, ClassRegistry.WarriorId, null, TrainingFocus.Gold);
                agent.Initialize();

                Assert.AreEqual(TrainingFocus.Gold, agent.Focus);
                Assert.AreEqual(SurvivorRewardConfig.ForFocus(TrainingFocus.Gold).PerGold, agent.Config.Rewards.PerGold);
                Assert.AreNotEqual(new SurvivorRewardConfig().PerGold, agent.Config.Rewards.PerGold);

                agent.Configure(0, ClassRegistry.WarriorId, null, TrainingFocus.Balanced);

                Assert.AreEqual(new SurvivorRewardConfig().PerGold, agent.Config.Rewards.PerGold);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }
    }
}
