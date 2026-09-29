using System;

namespace PersonalArena.Core
{
    /// <summary>Weighted zombie type available to an arena.</summary>
    public sealed class ZombieSpawnEntry
    {
        public ZombieTypeDef Type;
        public float Weight = 1f;

        public ZombieSpawnEntry()
        {
        }

        public ZombieSpawnEntry(ZombieTypeDef type, float weight = 1f)
        {
            Type = type;
            Weight = weight;
        }
    }

    /// <summary>
    /// Validated settings for one deterministic arena episode. The arena is a round platform
    /// inscribed in the Width x Height box and surrounded by an abyss.
    /// </summary>
    public sealed class ArenaConfig
    {
        public const float DefaultSize = 28f;

        public float Width = DefaultSize;
        public float Height = DefaultSize;
        public int ZombieCount = 1;
        public ZombieSpawnEntry[] ZombieSpawns =
            { new ZombieSpawnEntry(DefaultDefs.Walker()) };
        public float HpMultiplier = 1f;
        public float DamageMultiplier = 1f;
        public float SpeedMultiplier = 1f;
        public float EpisodeSeconds = 120f;
        public bool RespawnKilledZombies = true;
        public float PotionDropChance = 0.15f;
        public float PotionHeal = 30f;
        public float PotionLifetime = 15f;
        public int MaxPotions = 4;
        public int Seed = 1;

        /// <summary>Centre of the round platform.</summary>
        public Vec2 Center => new Vec2(Width * 0.5f, Height * 0.5f);

        /// <summary>Radius of the round platform; beyond it is the abyss.</summary>
        public float Radius => MathF.Min(Width, Height) * 0.5f;

        public void Validate()
        {
            ValidateRange(Width, 8f, 80f, nameof(Width));
            ValidateRange(Height, 8f, 80f, nameof(Height));
            if (ZombieCount < 0 || ZombieCount > 64)
            {
                throw new ArgumentException("ZombieCount must be between 0 and 64.", nameof(ZombieCount));
            }

            ValidateRange(HpMultiplier, 0.25f, 4f, nameof(HpMultiplier));
            ValidateRange(DamageMultiplier, 0.25f, 4f, nameof(DamageMultiplier));
            ValidateRange(SpeedMultiplier, 0.25f, 4f, nameof(SpeedMultiplier));
            ValidateRange(PotionDropChance, 0f, 1f, nameof(PotionDropChance));
            ValidateRange(PotionHeal, 0f, 1000f, nameof(PotionHeal));
            ValidateRange(PotionLifetime, 0.1f, 600f, nameof(PotionLifetime));
            if (MaxPotions < 0 || MaxPotions > 32)
            {
                throw new ArgumentException("MaxPotions must be between 0 and 32.", nameof(MaxPotions));
            }

            if (EpisodeSeconds <= 0f || float.IsNaN(EpisodeSeconds) || float.IsInfinity(EpisodeSeconds))
            {
                throw new ArgumentException("EpisodeSeconds must be greater than zero.", nameof(EpisodeSeconds));
            }

            if (ZombieCount > 0 && (ZombieSpawns == null || ZombieSpawns.Length == 0))
            {
                throw new ArgumentException("ZombieSpawns must contain at least one entry when zombies are requested.", nameof(ZombieSpawns));
            }

            float totalWeight = 0f;
            if (ZombieSpawns != null)
            {
                for (int i = 0; i < ZombieSpawns.Length; i++)
                {
                    ZombieSpawnEntry entry = ZombieSpawns[i];
                    if (entry == null || entry.Type == null)
                    {
                        throw new ArgumentException("Every ZombieSpawns entry must have a type.", nameof(ZombieSpawns));
                    }

                    if (entry.Weight <= 0f || float.IsNaN(entry.Weight) || float.IsInfinity(entry.Weight))
                    {
                        throw new ArgumentException("Every ZombieSpawns weight must be greater than zero.", nameof(ZombieSpawns));
                    }

                    if (entry.Type.KnockbackResist < 0f || entry.Type.KnockbackResist > 1f ||
                        float.IsNaN(entry.Type.KnockbackResist))
                    {
                        throw new ArgumentException(
                            "Every zombie KnockbackResist must be between zero and one.",
                            nameof(ZombieSpawns));
                    }

                    totalWeight += entry.Weight;
                }
            }

            if (ZombieCount > 0 && totalWeight <= 0f)
            {
                throw new ArgumentException("ZombieSpawns must have positive total weight.", nameof(ZombieSpawns));
            }
        }

        private static void ValidateRange(float value, float min, float max, string name)
        {
            if (value < min || value > max || float.IsNaN(value))
            {
                throw new ArgumentException($"{name} must be between {min} and {max}.", name);
            }
        }
    }
}
