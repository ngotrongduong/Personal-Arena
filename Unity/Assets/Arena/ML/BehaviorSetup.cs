using System;
using PersonalArena.Core;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;

namespace PersonalArena.ML
{
    /// <summary>Configures ML-Agents components before an Agent is enabled.</summary>
    public static class BehaviorSetup
    {
        public static void Configure(BehaviorParameters behavior, string heroClassId)
        {
            if (behavior == null)
            {
                throw new ArgumentNullException(nameof(behavior));
            }

            ObservationBuilder observations = new ObservationBuilder();
            behavior.BehaviorName = ClassRegistry.BehaviorName(heroClassId);
            behavior.BrainParameters.VectorObservationSize = observations.Size;
            behavior.BrainParameters.NumStackedVectorObservations = 1;
            behavior.BrainParameters.ActionSpec = ActionSpec.MakeDiscrete(
                HeroInput.MoveBranchSize,
                HeroInput.TurnBranchSize,
                HeroInput.SkillBranchSize);
            behavior.BehaviorType = BehaviorType.Default;
        }
    }
}
