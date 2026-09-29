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
                ZombieSpawns = CloneSpawns(baseConfig.ZombieSpawns),
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
