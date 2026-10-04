using System.Collections.Generic;

namespace PersonalArena.Core.Survivor
{
    /// <summary>
    /// Difficulty-tier rule changes (GDD §3.5). The value is the first tier that has the modifier;
    /// they are cumulative, so tier N has every modifier with a value ≤ N.
    /// </summary>
    public enum TierModifier
    {
        DenserSpawns = 2,
        EarlyElite = 3,
        FastRunners = 4,
        LessMeat = 5,
        EarlyBrutes = 6,
        DoubleElites = 7,
        EnemyRegen = 8,
        BossSummonsFaster = 9,
        Nightmare = 10
    }

    public static class TierModifiers
    {
        public const int FirstModifierTier = 2;
        public const int LastModifierTier = 10;

        public static bool Has(int tier, TierModifier modifier) => tier >= (int)modifier;

        public static string DisplayName(TierModifier modifier)
        {
            switch (modifier)
            {
                case TierModifier.DenserSpawns: return "10% more enemies";
                case TierModifier.EarlyElite: return "An extra elite appears at 1:30";
                case TierModifier.FastRunners: return "Runners are 15% faster";
                case TierModifier.LessMeat: return "Meat drops half as often";
                case TierModifier.EarlyBrutes: return "Brutes appear from minute 1";
                case TierModifier.DoubleElites: return "Twice as many elites";
                case TierModifier.EnemyRegen: return "Enemies slowly heal when not being hit";
                case TierModifier.BossSummonsFaster: return "The boss summons twice as fast";
                case TierModifier.Nightmare: return "Nightmare: all modifiers above, enemies 10% faster, elites +50% HP";
                default: return string.Empty;
            }
        }

        /// <summary>The modifiers active at <paramref name="tier"/>, ascending. Allocates (UI use only).</summary>
        public static IEnumerable<TierModifier> For(int tier)
        {
            List<TierModifier> list = new List<TierModifier>();
            for (int value = FirstModifierTier; value <= LastModifierTier; value++)
                if (tier >= value) list.Add((TierModifier)value);
            return list;
        }
    }
}
