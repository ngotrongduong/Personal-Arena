using System;

namespace PersonalArena.Core
{
    /// <summary>
    /// Drives a hero with a <see cref="PolicyBrain"/> using the same cadence as training:
    /// observe and decide every <see cref="DecisionPeriod"/> ticks, repeat that input in between.
    /// </summary>
    public sealed class BrainPilot
    {
        public const int DecisionPeriod = 5;

        private readonly ObservationBuilder observationBuilder = new ObservationBuilder();
        private readonly float[] observation;
        private readonly bool[] skillMask = new bool[HeroInput.SkillBranchSize];
        private readonly Rng rng;
        private PolicyBrain brain;
        private float[] logits;
        private HeroInput currentInput;
        private int ticksUntilDecision;

        public BrainPilot(int seed = 12345)
        {
            observation = new float[observationBuilder.Size];
            rng = new Rng(seed);
        }

        public PolicyBrain Brain => brain;

        /// <summary>When true the most likely action is always taken; otherwise actions are sampled like in training.</summary>
        public bool Deterministic { get; set; }

        public int ObservationSize => observationBuilder.Size;

        public HeroInput LastInput => currentInput;

        /// <summary>Checks that a brain fits this game's observation and action layout.</summary>
        public static string Validate(PolicyBrain candidate)
        {
            if (candidate == null)
            {
                return "No brain.";
            }

            int expectedObservation = new ObservationBuilder().Size;
            if (candidate.ObservationSize != expectedObservation)
            {
                return "Brain expects " + candidate.ObservationSize + " observations, game provides " + expectedObservation + ".";
            }

            if (candidate.BranchCount != 3 ||
                candidate.BranchSize(0) != HeroInput.MoveBranchSize ||
                candidate.BranchSize(1) != HeroInput.TurnBranchSize ||
                candidate.BranchSize(2) != HeroInput.SkillBranchSize)
            {
                return "Brain action branches do not match move/turn/skill.";
            }

            return null;
        }

        public void SetBrain(PolicyBrain newBrain)
        {
            string problem = Validate(newBrain);
            if (problem != null)
            {
                throw new ArgumentException(problem, nameof(newBrain));
            }

            brain = newBrain;
            logits = new float[HeroInput.MoveBranchSize + HeroInput.TurnBranchSize + HeroInput.SkillBranchSize];
            ticksUntilDecision = 0;
        }

        /// <summary>Drops the current brain (e.g. when the viewer switches to another hero class).</summary>
        public void ClearBrain()
        {
            brain = null;
            ResetEpisode();
        }

        /// <summary>Call when a new episode starts so the first tick makes a fresh decision.</summary>
        public void ResetEpisode()
        {
            ticksUntilDecision = 0;
            currentInput = default;
        }

        /// <summary>Input for the next <see cref="ArenaSim.Step"/> call.</summary>
        public HeroInput NextInput(ArenaSim sim)
        {
            if (sim == null)
            {
                throw new ArgumentNullException(nameof(sim));
            }

            if (brain == null)
            {
                return default;
            }

            if (ticksUntilDecision <= 0)
            {
                currentInput = Decide(sim);
                ticksUntilDecision = DecisionPeriod;
            }

            ticksUntilDecision--;
            return currentInput;
        }

        private HeroInput Decide(ArenaSim sim)
        {
            observationBuilder.Write(sim, observation);
            HeroActionMask.WriteSkillMask(sim.HeroDef, sim.Hero, skillMask);
            brain.Evaluate(observation, logits);

            Rng sampler = Deterministic ? null : rng;
            int move = PolicyBrain.ChooseAction(logits, 0, HeroInput.MoveBranchSize, null, sampler);
            int turn = PolicyBrain.ChooseAction(logits, HeroInput.MoveBranchSize, HeroInput.TurnBranchSize, null, sampler);
            int skill = PolicyBrain.ChooseAction(
                logits,
                HeroInput.MoveBranchSize + HeroInput.TurnBranchSize,
                HeroInput.SkillBranchSize,
                skillMask,
                sampler);
            return new HeroInput(move, HeroInput.BranchToTurn(turn), skill);
        }
    }
}
