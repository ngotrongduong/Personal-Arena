using System;
using System.Collections.Generic;
using PersonalArena.Core;
using PersonalArena.Core.Survivor;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PersonalArena.ML
{
    /// <summary>ML-Agents adapter around one deterministic headless Survivor run.</summary>
    public sealed class HeroAgent : Agent
    {
        private const int AgentSeedPrime = 1000003;
        private const int BuildSeedSalt = 0x5bd1e995;

        [SerializeField]
        public string HeroClassId = ClassRegistry.WarriorId;

        private readonly SurvivorObservation observation = new SurvivorObservation();
        private readonly float[] observationBuffer = new float[SurvivorObservation.Size];
        private readonly bool[] moveMask = new bool[SurvivorInput.MoveBranchSize];
        private readonly bool[] skillMask = new bool[SurvivorInput.SkillBranchSize];
        private readonly bool[] pickMask = new bool[SurvivorInput.PickBranchSize];
        private readonly List<KeyValuePair<string, float>> stats = new List<KeyValuePair<string, float>>();
        private SurvivorRewardCalculator rewardCalculator;
        private int episodeCounter;

        public SurvivorSim Sim { get; private set; }
        public SurvivorConfig Config { get; private set; }
        public int AgentIndex { get; private set; }

        /// <summary>Owner build mixed into training (M5); null means random builds only.</summary>
        public CharacterBuild OwnBuild { get; set; }

        public event Action<HeroAgent> EpisodeEnded;

        public void Configure(int agentIndex, string heroClassId)
        {
            AgentIndex = agentIndex;
            HeroClassId = heroClassId;
        }

        public override void Initialize()
        {
            Config = new SurvivorConfig { ClassDef = ClassRegistry.Create(HeroClassId) };
            rewardCalculator = new SurvivorRewardCalculator(Config.Rewards);
            MaxStep = 0;
        }

        public override void OnEpisodeBegin()
        {
            episodeCounter++;
            int seed = unchecked(AgentIndex * AgentSeedPrime + episodeCounter);
            EnvironmentParameters parameters = Academy.Instance.EnvironmentParameters;
            Config.RunSeconds = SurvivorEnvFactory.RunSeconds(
                parameters.GetWithDefault("run_seconds", SurvivorEnvFactory.DefaultRunSeconds));
            Config.Build = SurvivorEnvFactory.CreateBuild(
                new Rng(seed ^ BuildSeedSalt),
                parameters.GetWithDefault("tier_min", 1f),
                parameters.GetWithDefault("tier_max", 1f),
                parameters.GetWithDefault("build_level_max", 0f),
                parameters.GetWithDefault("own_build_share", 0f),
                OwnBuild);

            if (Sim == null)
            {
                Sim = new SurvivorSim(Config, seed);
            }
            else
            {
                Sim.Reset(seed);
            }
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            observation.Write(Sim, observationBuffer);
            sensor.AddObservation(observationBuffer);
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            if (Sim.IsEnded)
            {
                return;
            }

            ActionSegment<int> discrete = actions.DiscreteActions;
            Sim.Step(SurvivorActionMapper.FromBranches(
                discrete[SurvivorActionMapper.MoveBranch],
                discrete[SurvivorActionMapper.SkillBranch],
                discrete[SurvivorActionMapper.PickBranch]));
            AddReward(rewardCalculator.Compute(Sim.Events, Sim.LastStepSeconds));

            if (Sim.IsEnded)
            {
                FinishEpisode();
            }
            else if (Sim.IsAwaitingPick)
            {
                // The run is paused on a level-up offer: decide on the next academy step
                // instead of repeating a stale no-op pick for the rest of the decision period.
                RequestDecision();
            }
        }

        public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
        {
            SurvivorActionMapper.WriteMask(actionMask, Sim, moveMask, skillMask, pickMask);
        }

        public override void Heuristic(in ActionBuffers actionsOut)
        {
            ActionSegment<int> discrete = actionsOut.DiscreteActions;
            discrete[SurvivorActionMapper.MoveBranch] = 0;
            discrete[SurvivorActionMapper.SkillBranch] = 0;
            discrete[SurvivorActionMapper.PickBranch] = 0;
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            int dx = (keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0);
            int dy = (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0);
            discrete[SurvivorActionMapper.MoveBranch] = SurvivorActionMapper.MoveFromAxes(dx, dy);

            if (keyboard.jKey.isPressed)
            {
                discrete[SurvivorActionMapper.SkillBranch] = 1;
            }
            else if (keyboard.lKey.isPressed)
            {
                discrete[SurvivorActionMapper.SkillBranch] = 2;
            }
            else if (keyboard.spaceKey.isPressed)
            {
                discrete[SurvivorActionMapper.SkillBranch] = 3;
            }

            if (keyboard.digit1Key.isPressed)
            {
                discrete[SurvivorActionMapper.PickBranch] = 1;
            }
            else if (keyboard.digit2Key.isPressed)
            {
                discrete[SurvivorActionMapper.PickBranch] = 2;
            }
            else if (keyboard.digit3Key.isPressed)
            {
                discrete[SurvivorActionMapper.PickBranch] = 3;
            }
            else if (keyboard.digit4Key.isPressed)
            {
                discrete[SurvivorActionMapper.PickBranch] = 4;
            }
        }

        private void FinishEpisode()
        {
            stats.Clear();
            SurvivorEpisodeStats.Collect(Sim, stats);
            StatsRecorder recorder = Academy.Instance.StatsRecorder;
            for (int i = 0; i < stats.Count; i++)
            {
                recorder.Add(stats[i].Key, stats[i].Value);
            }

            EpisodeEnded?.Invoke(this);
            EndEpisode();
        }
    }
}
