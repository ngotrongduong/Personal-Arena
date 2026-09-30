using System;
using PersonalArena.Core.Survivor;
using Unity.MLAgents.Actuators;

namespace PersonalArena.ML
{
    /// <summary>Maps the three ML-Agents discrete branches to Survivor input and masks.</summary>
    public static class SurvivorActionMapper
    {
        public const int MoveBranch = 0;
        public const int SkillBranch = 1;
        public const int PickBranch = 2;

        public static SurvivorInput FromBranches(int move, int skill, int pick)
        {
            return new SurvivorInput(
                Clamp(move, SurvivorInput.MoveBranchSize),
                Clamp(skill, SurvivorInput.SkillBranchSize),
                Clamp(pick, SurvivorInput.PickBranchSize));
        }

        /// <summary>Move action for keyboard axes: 0 = stand, 1..8 = east then counter-clockwise in 45 degree steps.</summary>
        public static int MoveFromAxes(int dx, int dy)
        {
            if (dx == 0 && dy == 0)
            {
                return 0;
            }

            double angle = Math.Atan2(dy, dx);
            int sector = (int)Math.Round(angle / (Math.PI / 4.0));
            return (sector % 8 + 8) % 8 + 1;
        }

        /// <summary>Computes the Core mask and forwards every entry to ML-Agents.</summary>
        public static void WriteMask(IDiscreteActionMask actionMask, SurvivorSim sim, bool[] move, bool[] skill, bool[] pick)
        {
            SurvivorActionMask.WriteMask(sim, move, skill, pick);
            ApplyMask(actionMask, move, skill, pick);
        }

        public static void ApplyMask(IDiscreteActionMask actionMask, bool[] move, bool[] skill, bool[] pick)
        {
            if (actionMask == null)
            {
                throw new ArgumentNullException(nameof(actionMask));
            }

            Apply(actionMask, MoveBranch, move, SurvivorInput.MoveBranchSize);
            Apply(actionMask, SkillBranch, skill, SurvivorInput.SkillBranchSize);
            Apply(actionMask, PickBranch, pick, SurvivorInput.PickBranchSize);
        }

        private static void Apply(IDiscreteActionMask actionMask, int branch, bool[] enabled, int size)
        {
            // ML-Agents rejects a branch with every action disabled; keep "do nothing" as a safety net.
            bool any = false;
            for (int i = 0; i < size; i++)
            {
                any |= enabled[i];
            }

            for (int i = 0; i < size; i++)
            {
                actionMask.SetActionEnabled(branch, i, enabled[i] || (!any && i == 0));
            }
        }

        private static int Clamp(int value, int size)
        {
            return value < 0 || value >= size ? 0 : value;
        }
    }
}
