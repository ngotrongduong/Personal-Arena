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
                case TierModifier.DenserSpawns: return "Quái dày hơn 10%";
                case TierModifier.EarlyElite: return "Tinh anh xuất hiện thêm ở phút 1,5";
                case TierModifier.FastRunners: return "Runner nhanh hơn 15%";
                case TierModifier.LessMeat: return "Thịt rớt ít đi một nửa";
                case TierModifier.EarlyBrutes: return "Brute xuất hiện sớm từ phút 1";
                case TierModifier.DoubleElites: return "Tinh anh gấp đôi";
                case TierModifier.EnemyRegen: return "Quái hồi máu chậm khi không bị đánh";
                case TierModifier.BossSummonsFaster: return "Boss gọi quân nhanh gấp đôi";
                case TierModifier.Nightmare: return "Ác mộng: tất cả modifier trên, quái nhanh hơn 10%, tinh anh thêm 50% máu";
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
