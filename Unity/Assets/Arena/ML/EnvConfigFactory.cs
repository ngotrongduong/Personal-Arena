using System;
using PersonalArena.Core;

namespace PersonalArena.ML
{
    /// <summary>Builds validated episode configs from float-valued trainer parameters.</summary>
    public static class EnvConfigFactory
    {
        public const float DefaultArenaSize = ArenaConfig.DefaultSize;
        public const int DefaultZombieCount = 1;
        public const float DefaultMultiplier = 1f;

        public static ArenaConfig CreateBaseConfig()
        {
            ArenaConfig config = new ArenaConfig
            {
                Width = DefaultArenaSize,
                Height = DefaultArenaSize,
                ZombieCount = DefaultZombieCount,
                ZombieSpawns = new[] { new ZombieSpawnEntry(DefaultDefs.Walker()) },
                HpMultiplier = DefaultMultiplier,
                DamageMultiplier = DefaultMultiplier,
                SpeedMultiplier = DefaultMultiplier,
                EpisodeSeconds = 120f,
                RespawnKilledZombies = true,
                Seed = 1
            };
            config.Validate();
            return config;
        }

        public static ArenaConfig Create(
            ArenaConfig baseConfig,
            float zombieCount,
            float arenaSize,
            float hpMultiplier,
            float damageMultiplier,
            float speedMultiplier,
            int seed)
        {
            return Create(baseConfig, zombieCount, arenaSize, hpMultiplier, damageMultiplier,
                speedMultiplier, seed, null);
        }

        /// <summary>Same as the other overload, with an explicit zombie mix (null keeps the base mix).</summary>
        public static ArenaConfig Create(
            ArenaConfig baseConfig,
            float zombieCount,
            float arenaSize,
            float hpMultiplier,
            float damageMultiplier,
            float speedMultiplier,
            int seed,
            ZombieSpawnEntry[] spawns)
        {
            if (baseConfig == null)
            {
                throw new ArgumentNullException(nameof(baseConfig));
            }

            int count = Clamp(RoundToInt(FiniteOrDefault(zombieCount, DefaultZombieCount)), 0, 64);
            float size = Clamp(FiniteOrDefault(arenaSize, DefaultArenaSize), 8f, 80f);

            ArenaConfig config = new ArenaConfig
            {
                Width = size,
                Height = size,
                ZombieCount = count,
                ZombieSpawns = CloneSpawns(spawns ?? baseConfig.ZombieSpawns),
                HpMultiplier = Clamp(FiniteOrDefault(hpMultiplier, DefaultMultiplier), 0.25f, 4f),
                DamageMultiplier = Clamp(FiniteOrDefault(damageMultiplier, DefaultMultiplier), 0.25f, 4f),
                SpeedMultiplier = Clamp(FiniteOrDefault(speedMultiplier, DefaultMultiplier), 0.25f, 4f),
                EpisodeSeconds = baseConfig.EpisodeSeconds,
                RespawnKilledZombies = baseConfig.RespawnKilledZombies,
                PotionDropChance = baseConfig.PotionDropChance,
                PotionHeal = baseConfig.PotionHeal,
                PotionLifetime = baseConfig.PotionLifetime,
                MaxPotions = baseConfig.MaxPotions,
                Seed = seed
            };
            config.Validate();
            return config;
        }

        /// <summary>
        /// Builds a weighted zombie mix. Non-positive or non-finite weights drop that type; an
        /// all-zero mix falls back to Walkers only so the config always validates.
        /// </summary>
        public static ZombieSpawnEntry[] CreateSpawns(
            float walkerWeight,
            float runnerWeight,
            float bruteWeight,
            float spitterWeight)
        {
            ZombieTypeDef[] types = DefaultDefs.ZombieTypes();
            float[] weights = { walkerWeight, runnerWeight, bruteWeight, spitterWeight };
            int used = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                weights[i] = Clamp(FiniteOrDefault(weights[i], 0f), 0f, 100f);
                if (weights[i] > 0f)
                {
                    used++;
                }
            }

            if (used == 0)
            {
                return new[] { new ZombieSpawnEntry(DefaultDefs.Walker()) };
            }

            ZombieSpawnEntry[] spawns = new ZombieSpawnEntry[used];
            int next = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                if (weights[i] > 0f)
                {
                    spawns[next++] = new ZombieSpawnEntry(types[i], weights[i]);
                }
            }

            return spawns;
        }

        /// <summary>True when both mixes contain the same types with the same weights.</summary>
        public static bool SameSpawns(ZombieSpawnEntry[] a, ZombieSpawnEntry[] b)
        {
            if (a == null || b == null)
            {
                return a == b;
            }

            if (a.Length != b.Length)
            {
                return false;
            }

            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] == null || b[i] == null)
                {
                    if (a[i] != b[i])
                    {
                        return false;
                    }

                    continue;
                }

                if (a[i].Weight != b[i].Weight ||
                    a[i].Type == null || b[i].Type == null ||
                    a[i].Type.TypeIndex != b[i].Type.TypeIndex)
                {
                    return false;
                }
            }

            return true;
        }

        private static ZombieSpawnEntry[] CloneSpawns(ZombieSpawnEntry[] source)
        {
            if (source == null)
            {
                return null;
            }

            ZombieSpawnEntry[] copy = new ZombieSpawnEntry[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                ZombieSpawnEntry entry = source[i];
                copy[i] = entry == null ? null : new ZombieSpawnEntry(entry.Type, entry.Weight);
            }

            return copy;
        }

        private static int RoundToInt(float value)
        {
            return (int)Math.Floor(value + 0.5f);
        }

        private static float FiniteOrDefault(float value, float defaultValue)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? defaultValue : value;
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            return value < minimum ? minimum : value > maximum ? maximum : value;
        }

        private static float Clamp(float value, float minimum, float maximum)
        {
            return value < minimum ? minimum : value > maximum ? maximum : value;
        }
    }
}
