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
        private SurvivorEpisodeKind episodeKind;
        private int episodeCounter;

        public SurvivorSim Sim { get; private set; }
        public SurvivorConfig Config { get; private set; }
        public int AgentIndex { get; private set; }

        /// <summary>Owner build mixed into training (M5); null means random builds only.</summary>
        public CharacterBuild OwnBuild { get; set; }

        /// <summary>Reward weighting chosen by the owner for this TRAIN session (M5).</summary>
        public TrainingFocus Focus { get; private set; } = TrainingFocus.Balanced;

        public event Action<HeroAgent> EpisodeEnded;

        public void Configure(int agentIndex, string heroClassId, CharacterBuild ownBuild = null,
            TrainingFocus focus = TrainingFocus.Balanced)
        {
            AgentIndex = agentIndex;
            HeroClassId = heroClassId;
            OwnBuild = ownBuild;
            Focus = focus;
            if (Config != null)
            {
                // Already initialised (the object was active when it was configured): switch the rewards now.
                ApplyFocus();
            }
        }

        public override void Initialize()
        {
            Config = new SurvivorConfig { ClassDef = ClassRegistry.Create(HeroClassId) };
            ApplyFocus();
            MaxStep = 0;
        }

        private void ApplyFocus()
        {
            Config.Rewards = SurvivorRewardConfig.ForFocus(Focus);
            rewardCalculator = new SurvivorRewardCalculator(Config.Rewards);
        }

        public override void OnEpisodeBegin()
        {
            episodeCounter++;
            int seed = unchecked(AgentIndex * AgentSeedPrime + episodeCounter);
            EnvironmentParameters parameters = Academy.Instance.EnvironmentParameters;
            Config.RunSeconds = SurvivorEnvFactory.RunSeconds(
                parameters.GetWithDefault("run_seconds", SurvivorEnvFactory.DefaultRunSeconds));
            SurvivorEpisode episode = SurvivorEnvFactory.CreateEpisode(
                new Rng(seed ^ BuildSeedSalt),
                parameters.GetWithDefault("tier_min", 1f),
                parameters.GetWithDefault("tier_max", 1f),
                parameters.GetWithDefault("build_level_max", 0f),
                parameters.GetWithDefault("own_build_share", 0f),
                parameters.GetWithDefault("review_share", 0f),
                parameters.GetWithDefault("hard_share", 0f),
                OwnBuild);
            episodeKind = episode.Kind;
            Config.Build = episode.Build;
            Config.OpeningRing = episode.OpeningRing;

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

            bool wasAwaitingPick = Sim.IsAwaitingPick;
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
            else if (Sim.IsAwaitingPick || wasAwaitingPick)
            {
                // Paused on a level-up offer: decide on the next academy step instead of
                // repeating a stale no-op pick. Right after a pick, decide again too, so the
                // move and skill chosen while paused are not replayed for the rest of the period.
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
            recorder.Add(SurvivedStatKey(episodeKind), Sim.Time);

            EpisodeEnded?.Invoke(this);
            EndEpisode();
        }

        private static string SurvivedStatKey(SurvivorEpisodeKind kind)
        {
            switch (kind)
            {
                case SurvivorEpisodeKind.Review: return "Arena/Episode/SurvivedReview";
                case SurvivorEpisodeKind.Hard: return "Arena/Episode/SurvivedHard";
                case SurvivorEpisodeKind.Own: return "Arena/Episode/SurvivedOwn";
                default: return "Arena/Episode/SurvivedNew";
            }
        }
    }
}
