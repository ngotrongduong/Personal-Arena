using System;
using PersonalArena.Core;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using UnityEngine;

namespace PersonalArena.ML
{
    /// <summary>Creates independent headless simulations for parallel experience collection.</summary>
    public sealed class TrainingArenaHost : MonoBehaviour
    {
        private const int DefaultAgentCount = 16;
        private const int MaximumAgentCount = 256;

        [SerializeField, Min(1)]
        private int agentCount = DefaultAgentCount;

        private void Awake()
        {
            Time.fixedDeltaTime = ArenaSim.FixedDeltaTime;
            Application.targetFrameRate = -1;

            int count = CommandLineAgentCount(agentCount);
            for (int i = 0; i < count; i++)
            {
                SpawnAgent(i);
            }
        }

        private void SpawnAgent(int index)
        {
            GameObject agentObject = new GameObject($"HeroAgent_{index:D2}");
            agentObject.SetActive(false);
            agentObject.transform.SetParent(transform, false);

            BehaviorParameters behavior = agentObject.AddComponent<BehaviorParameters>();
            BehaviorSetup.Configure(behavior, ClassRegistry.WarriorId);

            HeroAgent agent = agentObject.AddComponent<HeroAgent>();
            agent.Configure(index, ClassRegistry.WarriorId);

            DecisionRequester requester = agentObject.AddComponent<DecisionRequester>();
            requester.DecisionPeriod = 5;
            requester.DecisionStep = 0;
            requester.TakeActionsBetweenDecisions = true;

            agentObject.SetActive(true);
        }

        private static int CommandLineAgentCount(int fallback)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], "--arena-agents", StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(args[i + 1], out int parsed))
                {
                    return Mathf.Clamp(parsed, 1, MaximumAgentCount);
                }
            }

            return Mathf.Clamp(fallback, 1, MaximumAgentCount);
        }
    }
}
