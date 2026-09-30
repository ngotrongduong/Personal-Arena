using System;
using PersonalArena.Core.Survivor;
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

            behavior.BehaviorName = ClassRegistry.BehaviorName(heroClassId);
            behavior.BrainParameters.VectorObservationSize = SurvivorObservation.Size;
            behavior.BrainParameters.NumStackedVectorObservations = 1;
            behavior.BrainParameters.ActionSpec = ActionSpec.MakeDiscrete(
                SurvivorInput.MoveBranchSize,
                SurvivorInput.SkillBranchSize,
                SurvivorInput.PickBranchSize);
            behavior.BehaviorType = BehaviorType.Default;
        }
    }
}
