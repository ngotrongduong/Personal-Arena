using System;
using PersonalArena.Core;
using PersonalArena.Core.Survivor;

namespace PersonalArena.ML
{
    /// <summary>Turns trainer environment parameters into per-episode Survivor settings.</summary>
    public static class SurvivorEnvFactory
    {
        public const float DefaultRunSeconds = 900f;
        public const float MinimumRunSeconds = 60f;
        public const float MaximumRunSeconds = 900f;
        public const int MaximumBuildLevel = 50;

        public static float RunSeconds(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return DefaultRunSeconds;
            }

            return Math.Max(MinimumRunSeconds, Math.Min(MaximumRunSeconds, (float)Math.Round(value)));
        }

        public static int Tier(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return 1;
            }

            return Math.Max(1, Math.Min(10, (int)Math.Round(value)));
        }

        public static int BuildLevel(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return 0;
            }

            return Math.Max(0, Math.Min(MaximumBuildLevel, (int)Math.Round(value)));
        }

        /// <summary>
        /// Picks the episode's build: the owner's build with probability <paramref name="ownBuildShare"/>
        /// (when one is given), otherwise a random build of up to <paramref name="buildLevelMax"/> points.
        /// </summary>
        public static CharacterBuild CreateBuild(Rng rng, float tierMin, float tierMax, float buildLevelMax,
            float ownBuildShare, CharacterBuild ownBuild)
        {
            if (rng == null)
            {
                throw new ArgumentNullException(nameof(rng));
            }

            int low = Tier(tierMin);
            int high = Math.Max(low, Tier(tierMax));
            if (ownBuild != null && ownBuildShare > 0f && rng.NextFloat() < ownBuildShare)
            {
                return ownBuild.Clone();
            }

            int level = BuildLevel(buildLevelMax);
            if (level == 0 && low == high)
            {
                return new CharacterBuild { Tier = low };
            }

            return BuildRandomizer.Random(rng, level, low, high);
        }

        public static SurvivorClassDef CreateClass(string classId)
        {
            if (!string.Equals(classId, ClassRegistry.WarriorId, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"Class '{classId}' has no Survivor kit yet (arrives in M7).", nameof(classId));
            }

            return SurvivorDefaults.Warrior();
        }
    }
}
