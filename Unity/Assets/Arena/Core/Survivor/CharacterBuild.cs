using System;

namespace PersonalArena.Core.Survivor
{
    public sealed class CharacterBuild
    {
        public int[] Points { get; } = new int[StatInfo.SlotCount];
        public int Tier { get; set; } = 1;
        public int Level { get { int total = 0; for (int i = 0; i < Points.Length; i++) total += Points[i]; return total; } }

        public void Validate()
        {
            if (Tier < 1 || Tier > 10) throw new ArgumentOutOfRangeException(nameof(Tier));
            for (int i = 0; i < Points.Length; i++)
            {
                if (Points[i] < 0 || Points[i] > StatInfo.Cap((StatId)i))
                    throw new ArgumentOutOfRangeException(nameof(Points), "A stat point count is outside its cap.");
            }
        }

        public CharacterBuild Clone()
        {
            CharacterBuild result = new CharacterBuild { Tier = Tier };
            Array.Copy(Points, result.Points, Points.Length);
            return result;
        }
    }

    public static class BuildRandomizer
    {
        public static CharacterBuild Random(PersonalArena.Core.Rng rng, int maxLevel, int tierMin, int tierMax)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            if (maxLevel < 0 || tierMin < 1 || tierMax > 10 || tierMin > tierMax) throw new ArgumentOutOfRangeException();
            CharacterBuild build = new CharacterBuild { Tier = tierMin + rng.NextInt(tierMax - tierMin + 1) };
            int target = rng.NextInt(maxLevel + 1);
            int focusCount = 1 + rng.NextInt(3);
            int[] focus = new int[3];
            for (int i = 0; i < focusCount; i++)
            {
                int candidate;
                bool duplicate;
                do
                {
                    candidate = rng.NextInt(StatInfo.UsedCount);
                    duplicate = false;
                    for (int j = 0; j < i; j++) duplicate |= focus[j] == candidate;
                } while (duplicate);
                focus[i] = candidate;
            }
            for (int point = 0; point < target; point++)
            {
                bool preferFocus = rng.NextFloat() < 0.7f;
                int stat = PickAvailable(rng, build, focus, focusCount, preferFocus);
                if (stat < 0) stat = PickAvailable(rng, build, focus, focusCount, !preferFocus);
                if (stat < 0) break;
                build.Points[stat]++;
            }
            build.Validate();
            return build;
        }

        private static int PickAvailable(PersonalArena.Core.Rng rng, CharacterBuild build, int[] focus, int focusCount, bool inFocus)
        {
            int count = 0;
            int selected = -1;
            for (int stat = 0; stat < StatInfo.UsedCount; stat++)
            {
                bool member = false;
                for (int i = 0; i < focusCount; i++) member |= focus[i] == stat;
                if (member != inFocus || build.Points[stat] >= StatInfo.Cap((StatId)stat)) continue;
                count++;
                if (rng.NextInt(count) == 0) selected = stat;
            }
            return selected;
        }
    }

    public static class XpCurve
    {
        public static int Required(int level)
        {
            if (level < 1) throw new ArgumentOutOfRangeException(nameof(level));
            if (level == 1) return 5;
            if (level <= 19) return 5 + 10 * (level - 1);
            if (level == 20) return 185 + 13 + 600;
            if (level <= 39) return 185 + 13 * (level - 19);
            if (level == 40) return 445 + 16 + 2400;
            return 445 + 16 * (level - 39);
        }
    }
}
