using System;
using PersonalArena.Core;
using Unity.MLAgents.Actuators;

namespace PersonalArena.ML
{
    /// <summary>Allocation-free translation and masking for the shared discrete action space.</summary>
    public static class HeroActionMapper
    {
        public const int MoveBranch = 0;
        public const int TurnBranch = 1;
        public const int SkillBranch = 2;

        public static HeroInput FromBranches(int moveBranch, int turnBranch, int skillBranch)
        {
            if (moveBranch < 0 || moveBranch >= HeroInput.MoveBranchSize)
            {
                throw new ArgumentOutOfRangeException(nameof(moveBranch));
            }

            if (turnBranch < 0 || turnBranch >= HeroInput.TurnBranchSize)
            {
                throw new ArgumentOutOfRangeException(nameof(turnBranch));
            }

            if (skillBranch < 0 || skillBranch >= HeroInput.SkillBranchSize)
            {
                throw new ArgumentOutOfRangeException(nameof(skillBranch));
            }

            return new HeroInput(moveBranch, HeroInput.BranchToTurn(turnBranch), skillBranch);
        }

        public static bool IsSkillActionEnabled(HeroClassDef classDef, HeroState hero, int action)
        {
            if (action == 0)
            {
                return true;
            }

            if (classDef == null)
            {
                throw new ArgumentNullException(nameof(classDef));
            }

            if (hero == null)
            {
                throw new ArgumentNullException(nameof(hero));
            }

            if (action < 0 || action >= HeroInput.SkillBranchSize)
            {
                throw new ArgumentOutOfRangeException(nameof(action));
            }

            SkillDef skill = classDef.Skills[action - 1];
            if (skill == null || skill.Kind == SkillKind.None)
            {
                return false;
            }

            if (skill.Kind == SkillKind.Block && hero.IsBlocking)
            {
                return true;
            }

            return hero.CooldownRemaining[action - 1] <= 0f && hero.Energy >= skill.EnergyCost;
        }

        public static void WriteSkillMask(
            IDiscreteActionMask mask,
            HeroClassDef classDef,
            HeroState hero)
        {
            if (mask == null)
            {
                throw new ArgumentNullException(nameof(mask));
            }

            for (int action = 0; action < HeroInput.SkillBranchSize; action++)
            {
                mask.SetActionEnabled(
                    SkillBranch,
                    action,
                    IsSkillActionEnabled(classDef, hero, action));
            }
        }
    }
}
