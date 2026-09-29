using System;
using PersonalArena.Core;

namespace PersonalArena.Core.Survivor
{
    public sealed class SurvivorPilot
    {
        public const int DecisionPeriod = 5;
        private readonly SurvivorObservation writer = new SurvivorObservation();
        private readonly float[] observation = new float[SurvivorObservation.Size];
        private readonly bool[] moveMask = new bool[SurvivorInput.MoveBranchSize];
        private readonly bool[] skillMask = new bool[SurvivorInput.SkillBranchSize];
        private readonly bool[] pickMask = new bool[SurvivorInput.PickBranchSize];
        private readonly float[] logits = new float[SurvivorInput.MoveBranchSize + SurvivorInput.SkillBranchSize + SurvivorInput.PickBranchSize];
        private readonly Rng rng;
        private PolicyBrain brain;
        private SurvivorInput current;
        private int ticksUntilDecision;

        public SurvivorPilot(int seed = 12345) { rng = new Rng(seed); }
        public SurvivorPilot(PolicyBrain brain, int seed = 12345) : this(seed) { SetBrain(brain); }
        public bool Deterministic { get; set; }
        public PolicyBrain Brain => brain;
        public SurvivorInput LastInput => current;
        public int ObservationSize => SurvivorObservation.Size;

        public static string Validate(PolicyBrain candidate)
        {
            if (candidate == null) return "No brain.";
            if (candidate.ObservationSize != SurvivorObservation.Size) return "Brain observation size does not match schema v4.";
            if (candidate.BranchCount != 3 || candidate.BranchSize(0) != SurvivorInput.MoveBranchSize || candidate.BranchSize(1) != SurvivorInput.SkillBranchSize || candidate.BranchSize(2) != SurvivorInput.PickBranchSize)
                return "Brain action branches do not match move/skill/pick.";
            return null;
        }

        public void SetBrain(PolicyBrain value)
        {
            string problem = Validate(value); if (problem != null) throw new ArgumentException(problem, nameof(value));
            brain = value; ResetEpisode();
        }
        public void ClearBrain() { brain = null; ResetEpisode(); }
        public void ResetEpisode() { ticksUntilDecision = 0; current = default; }

        public SurvivorInput NextInput(SurvivorSim sim)
        {
            if (sim == null) throw new ArgumentNullException(nameof(sim));
            if (brain == null) return default;
            if (ticksUntilDecision <= 0 || sim.IsAwaitingPick)
            {
                writer.Write(sim, observation); SurvivorActionMask.WriteMask(sim, moveMask, skillMask, pickMask); brain.Evaluate(observation, logits);
                Rng sampler = Deterministic ? null : rng;
                int move = PolicyBrain.ChooseAction(logits, 0, SurvivorInput.MoveBranchSize, moveMask, sampler);
                int skill = PolicyBrain.ChooseAction(logits, SurvivorInput.MoveBranchSize, SurvivorInput.SkillBranchSize, skillMask, sampler);
                int pick = PolicyBrain.ChooseAction(logits, SurvivorInput.MoveBranchSize + SurvivorInput.SkillBranchSize, SurvivorInput.PickBranchSize, pickMask, sampler);
                current = new SurvivorInput(move, skill, pick); ticksUntilDecision = DecisionPeriod;
            }
            ticksUntilDecision--; return current;
        }
    }
}
