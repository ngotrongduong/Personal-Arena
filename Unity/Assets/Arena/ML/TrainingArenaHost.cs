using System;
using PersonalArena.Core.Survivor;
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
            Time.fixedDeltaTime = SurvivorSim.FixedDeltaTime;
            Application.targetFrameRate = -1;

            int count = CommandLineAgentCount(agentCount);
            string classId = CommandLineHeroClass(ClassRegistry.WarriorId);
            if (classId == null)
            {
                // Never train a Warrior inside another class's run: quit so the trainer sees the failure.
                Application.Quit(2);
                enabled = false;
                return;
            }

            OwnerTrainingArgs owner = OwnerTrainingArgs.Parse(Environment.GetCommandLineArgs());
            foreach (string warning in owner.Warnings)
            {
                Debug.LogWarning(warning);
            }
            Debug.Log(owner.Describe());

            for (int i = 0; i < count; i++)
            {
                SpawnAgent(i, classId, owner);
            }
        }

        private void SpawnAgent(int index, string classId, OwnerTrainingArgs owner)
        {
            GameObject agentObject = new GameObject($"HeroAgent_{index:D2}");
            agentObject.SetActive(false);
            agentObject.transform.SetParent(transform, false);

            BehaviorParameters behavior = agentObject.AddComponent<BehaviorParameters>();
            BehaviorSetup.Configure(behavior, classId);

            HeroAgent agent = agentObject.AddComponent<HeroAgent>();
            // Each agent gets its own copy: the factory only reads it, but a shared mutable build is a trap.
            agent.Configure(index, classId, owner.OwnBuild?.Clone(), owner.Focus);

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

        private static string CommandLineHeroClass(string fallback)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], "--hero-class", StringComparison.OrdinalIgnoreCase))
                {
                    string value = args[i + 1].Trim().ToLowerInvariant();
                    if (ClassRegistry.HasSurvivorKit(value))
                    {
                        return value;
                    }

                    Debug.LogError($"--hero-class '{value}' is not a known hero class; refusing to train.");
                    return null;
                }
            }

            return fallback;
        }
    }
}
