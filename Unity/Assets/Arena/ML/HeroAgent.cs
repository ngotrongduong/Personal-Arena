using System;
using PersonalArena.Core;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PersonalArena.ML
{
    /// <summary>ML-Agents adapter around one deterministic headless arena simulation.</summary>
    public sealed class HeroAgent : Agent
    {
        private const int AgentSeedPrime = 1000003;

        [SerializeField]
        public string HeroClassId = ClassRegistry.WarriorId;

        private ObservationBuilder observationBuilder;
        private RewardCalculator rewardCalculator;
        private float[] observationBuffer;
        private int episodeCounter;
        private int kills;
        private int backstabs;
        private int parries;
        private float damageTaken;
        private bool died;
        private bool fell;
        private int ringOuts;
        private int potionsPicked;

        public ArenaSim Sim { get; private set; }
        public HeroClassDef ClassDef { get; private set; }
        public ArenaConfig BaseConfig { get; private set; }
        public int AgentIndex { get; private set; }

        public event Action<HeroAgent, EpisodeStats> EpisodeEnded;

        public void Configure(int agentIndex, string heroClassId)
        {
            AgentIndex = agentIndex;
            HeroClassId = heroClassId;
        }

        public override void Initialize()
        {
            ClassDef = ClassRegistry.Create(HeroClassId);
            BaseConfig = EnvConfigFactory.CreateBaseConfig();
            observationBuilder = new ObservationBuilder();
            rewardCalculator = new RewardCalculator(ClassDef.Rewards);
            observationBuffer = new float[observationBuilder.Size];
            MaxStep = 0;
        }

        public override void OnEpisodeBegin()
        {
            episodeCounter++;
            int seed = unchecked(AgentIndex * AgentSeedPrime + episodeCounter);
            EnvironmentParameters parameters = Academy.Instance.EnvironmentParameters;
            ArenaConfig episodeConfig = EnvConfigFactory.Create(
                BaseConfig,
                parameters.GetWithDefault("zombie_count", EnvConfigFactory.DefaultZombieCount),
                parameters.GetWithDefault("arena_size", EnvConfigFactory.DefaultArenaSize),
                parameters.GetWithDefault("hp_mult", EnvConfigFactory.DefaultMultiplier),
                parameters.GetWithDefault("damage_mult", EnvConfigFactory.DefaultMultiplier),
                parameters.GetWithDefault("speed_mult", EnvConfigFactory.DefaultMultiplier),
                seed);

            if (NeedsNewSimulation(episodeConfig))
            {
                Sim = new ArenaSim(ClassDef, episodeConfig);
            }
            else
            {
                CopyEpisodeSettings(episodeConfig, Sim.Config);
                Sim.Reset(seed);
            }

            kills = 0;
            backstabs = 0;
            parries = 0;
            damageTaken = 0f;
            died = false;
            fell = false;
            ringOuts = 0;
            potionsPicked = 0;
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            observationBuilder.Write(Sim, observationBuffer);
            sensor.AddObservation(observationBuffer);
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            ActionSegment<int> discrete = actions.DiscreteActions;
            HeroInput input = HeroActionMapper.FromBranches(discrete[0], discrete[1], discrete[2]);
            Sim.Step(input);
            AddReward(rewardCalculator.Compute(Sim.Events, ArenaSim.FixedDeltaTime));
            AccumulateStats();

            if (Sim.Done)
            {
                FinishEpisode();
            }
        }

        public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
        {
            HeroActionMapper.WriteSkillMask(actionMask, ClassDef, Sim.Hero);
        }

        public override void Heuristic(in ActionBuffers actionsOut)
        {
            ActionSegment<int> discrete = actionsOut.DiscreteActions;
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                discrete[0] = 0;
                discrete[1] = HeroInput.TurnToBranch(0);
                discrete[2] = 0;
                return;
            }

            int dx = (keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0);
            int dy = (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0);
            int turn = (keyboard.qKey.isPressed ? 1 : 0) - (keyboard.eKey.isPressed ? 1 : 0);

            int skill = 0;
            if (keyboard.lKey.isPressed)
            {
                skill = 3;
            }
            else if (keyboard.jKey.isPressed)
            {
                skill = 1;
            }
            else if (keyboard.kKey.isPressed)
            {
                skill = 2;
            }
            else if (keyboard.spaceKey.isPressed)
            {
                skill = 4;
            }

            discrete[0] = HeroInput.MoveFromAxes(dx, dy);
            discrete[1] = HeroInput.TurnToBranch(turn);
            discrete[2] = skill;
        }

        private bool NeedsNewSimulation(ArenaConfig episodeConfig)
        {
            return Sim == null ||
                Sim.Config.Width != episodeConfig.Width ||
                Sim.Config.Height != episodeConfig.Height ||
                Sim.Config.ZombieCount != episodeConfig.ZombieCount;
        }

        private static void CopyEpisodeSettings(ArenaConfig source, ArenaConfig destination)
        {
            destination.HpMultiplier = source.HpMultiplier;
            destination.DamageMultiplier = source.DamageMultiplier;
            destination.SpeedMultiplier = source.SpeedMultiplier;
            destination.EpisodeSeconds = source.EpisodeSeconds;
            destination.RespawnKilledZombies = source.RespawnKilledZombies;
            destination.Seed = source.Seed;
            destination.Validate();
        }

        private void AccumulateStats()
        {
            for (int i = 0; i < Sim.Events.Count; i++)
            {
                SimEvent item = Sim.Events[i];
                switch (item.Type)
                {
                    case SimEventType.ZombieKilled:
                        kills++;
                        break;
                    case SimEventType.Backstab:
                        backstabs++;
                        break;
                    case SimEventType.Parry:
                        parries++;
                        break;
                    case SimEventType.HeroDamaged:
                        damageTaken += item.Value;
                        break;
                    case SimEventType.HeroDied:
                        died = true;
                        break;
                    case SimEventType.HeroFell:
                        fell = true;
                        break;
                    case SimEventType.ZombieFell:
                        ringOuts++;
                        break;
                    case SimEventType.PotionPicked:
                        potionsPicked++;
                        break;
                }
            }
        }

        private void FinishEpisode()
        {
            EpisodeStats stats = new EpisodeStats(
                kills,
                backstabs,
                parries,
                Sim.Time,
                died || !Sim.Hero.Alive,
                damageTaken,
                Sim.Config.ZombieCount,
                fell,
                ringOuts,
                potionsPicked);

            StatsRecorder recorder = Academy.Instance.StatsRecorder;
            recorder.Add("Arena/Kills", stats.Kills);
            recorder.Add("Arena/Backstabs", stats.Backstabs);
            recorder.Add("Arena/Parries", stats.Parries);
            recorder.Add("Arena/SurvivedSeconds", stats.SurvivedSeconds);
            recorder.Add("Arena/Died", stats.Died ? 1f : 0f);
            recorder.Add("Arena/DamageTaken", stats.DamageTaken);
            recorder.Add("Arena/ZombieCount", stats.ZombieCount);
            recorder.Add("Arena/Fell", stats.Fell ? 1f : 0f);
            recorder.Add("Arena/RingOuts", stats.RingOuts);
            recorder.Add("Arena/Potions", stats.PotionsPicked);

            EpisodeEnded?.Invoke(this, stats);
            EndEpisode();
        }
    }
}
