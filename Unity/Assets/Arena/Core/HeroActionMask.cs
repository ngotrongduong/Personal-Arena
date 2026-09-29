using System;

namespace PersonalArena.Core
{
    /// <summary>Which discrete skill actions a hero may choose right now (shared by training and playback).</summary>
    public static class HeroActionMask
    {
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

        /// <summary>Writes one flag per skill action into <paramref name="enabled"/>.</summary>
        public static void WriteSkillMask(HeroClassDef classDef, HeroState hero, bool[] enabled)
        {
            if (enabled == null)
            {
                throw new ArgumentNullException(nameof(enabled));
            }

            if (enabled.Length < HeroInput.SkillBranchSize)
            {
                throw new ArgumentException("Mask buffer is too small.", nameof(enabled));
            }

            for (int action = 0; action < HeroInput.SkillBranchSize; action++)
            {
                enabled[action] = IsSkillActionEnabled(classDef, hero, action);
            }
        }
    }
}
