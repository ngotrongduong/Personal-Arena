using System;
using PersonalArena.Core;
using PersonalArena.Core.Survivor;

namespace PersonalArena.ML
{
    /// <summary>New = random build at the curriculum tier, Own = the owner's build (M5).</summary>
    public enum SurvivorEpisodeKind { New, Review, Hard, Own }

    public sealed class SurvivorEpisode
    {
        public SurvivorEpisodeKind Kind;
        public CharacterBuild Build;
        public int OpeningRing;
    }

    /// <summary>Turns trainer environment parameters into per-episode Survivor settings.</summary>
    public static class SurvivorEnvFactory
    {
        public const float DefaultRunSeconds = 900f;
        public const float MinimumRunSeconds = 60f;
        public const float MaximumRunSeconds = 900f;
        public const int MaximumBuildLevel = 50;
        public const int MaximumJitterMoves = 2;

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
            return CreateBuild(rng, tierMin, tierMax, buildLevelMax, ownBuildShare, ownBuild, out _);
        }

        private static CharacterBuild CreateBuild(Rng rng, float tierMin, float tierMax, float buildLevelMax,
            float ownBuildShare, CharacterBuild ownBuild, out bool usedOwnBuild)
        {
            if (rng == null)
            {
                throw new ArgumentNullException(nameof(rng));
            }

            usedOwnBuild = false;
            int low = Tier(tierMin);
            int high = Math.Max(low, Tier(tierMax));
            if (ownBuild != null && ownBuildShare > 0f && rng.NextFloat() < ownBuildShare)
            {
                usedOwnBuild = true;
                return Jitter(rng, ownBuild);
            }

            int level = BuildLevel(buildLevelMax);
            if (level == 0 && low == high)
            {
                return new CharacterBuild { Tier = low };
            }

            return BuildRandomizer.Random(rng, level, low, high);
        }

        /// <summary>
        /// A copy of the owner's build with up to <see cref="MaximumJitterMoves"/> points moved between used
        /// stats, so the brain does not overfit one exact build. The total, the caps and the tier are kept.
        /// </summary>
        public static CharacterBuild Jitter(Rng rng, CharacterBuild ownBuild)
        {
            CharacterBuild build = ownBuild.Clone();
            int moves = rng.NextInt(MaximumJitterMoves + 1);
            for (int move = 0; move < moves; move++)
            {
                int from = PickStat(rng, build, stat => build.Points[stat] > 0, -1);
                int to = from < 0 ? -1 : PickStat(rng, build, stat => build.Points[stat] < StatInfo.Cap((StatId)stat), from);
                if (to < 0)
                {
                    continue;
                }

                build.Points[from]--;
                build.Points[to]++;
            }

            return build;
        }

        /// <summary>A uniformly random used stat that passes <paramref name="allowed"/> (never <paramref name="except"/>), or -1.</summary>
        private static int PickStat(Rng rng, CharacterBuild build, Func<int, bool> allowed, int except)
        {
            int count = 0;
            for (int stat = 0; stat < StatInfo.UsedCount; stat++)
            {
                if (stat != except && allowed(stat))
                {
                    count++;
                }
            }

            if (count == 0)
            {
                return -1;
            }

            int chosen = rng.NextInt(count);
            for (int stat = 0; stat < StatInfo.UsedCount; stat++)
            {
                if (stat != except && allowed(stat) && chosen-- == 0)
                {
                    return stat;
                }
            }

            return -1;
        }

        public static SurvivorEpisode CreateEpisode(Rng rng, float tierMin, float tierMax,
            float buildLevelMax, float ownBuildShare, float reviewShare, float hardShare,
            CharacterBuild ownBuild)
        {
            if (rng == null)
            {
                throw new ArgumentNullException(nameof(rng));
            }

            float review = Share(reviewShare);
            float hard = Share(hardShare);
            float total = review + hard;
            if (total > 1f)
            {
                review /= total;
                hard /= total;
                total = 1f;
            }

            if (total <= 0f)
            {
                return NewEpisode(rng, tierMin, tierMax, buildLevelMax, ownBuildShare, ownBuild);
            }

            float roll = rng.NextFloat();
            if (roll < hard)
            {
                int tier = Tier(tierMax + 1f);
                return new SurvivorEpisode
                {
                    Kind = SurvivorEpisodeKind.Hard,
                    Build = BuildRandomizer.Random(rng, BuildLevel(buildLevelMax), tier, tier),
                    OpeningRing = 16
                };
            }

            if (roll < hard + review)
            {
                return new SurvivorEpisode
                {
                    Kind = SurvivorEpisodeKind.Review,
                    Build = new CharacterBuild { Tier = 1 }
                };
            }

            return NewEpisode(rng, tierMin, tierMax, buildLevelMax, ownBuildShare, ownBuild);
        }

        private static SurvivorEpisode NewEpisode(Rng rng, float tierMin, float tierMax, float buildLevelMax,
            float ownBuildShare, CharacterBuild ownBuild)
        {
            CharacterBuild build = CreateBuild(rng, tierMin, tierMax, buildLevelMax, ownBuildShare, ownBuild, out bool own);
            return new SurvivorEpisode
            {
                Kind = own ? SurvivorEpisodeKind.Own : SurvivorEpisodeKind.New,
                Build = build
            };
        }

        public static SurvivorClassDef CreateClass(string classId)
        {
            if (!string.Equals(classId, ClassRegistry.WarriorId, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"Class '{classId}' has no Survivor kit yet (arrives in M7).", nameof(classId));
            }

            return SurvivorDefaults.Warrior();
        }

        private static float Share(float value)
        {
            if (float.IsNaN(value)) return 0f;
            return Math.Max(0f, Math.Min(1f, value));
        }
    }
}
