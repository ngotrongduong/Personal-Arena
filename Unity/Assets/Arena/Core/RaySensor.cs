using System;
using System.Collections.Generic;

namespace PersonalArena.Core
{
    /// <summary>Fixed-size 360-degree ray sensor settings.</summary>
    public sealed class RaySensorConfig
    {
        public int RayCount = 72;
        public float MaxDistance = 20f;
        public int ZombieTypeCount = 4;

        public void Validate()
        {
            if (RayCount <= 0)
            {
                throw new ArgumentException("RayCount must be greater than zero.", nameof(RayCount));
            }

            if (MaxDistance <= 0f || float.IsNaN(MaxDistance) || float.IsInfinity(MaxDistance))
            {
                throw new ArgumentException("MaxDistance must be greater than zero.", nameof(MaxDistance));
            }

            if (ZombieTypeCount <= 0)
            {
                throw new ArgumentException("ZombieTypeCount must be greater than zero.", nameof(ZombieTypeCount));
            }
        }
    }

    /// <summary>
    /// Writes platform-edge, zombie, potion and enemy-projectile ray hits without per-call allocations.
    /// Per ray: one-hot category [none, edge, zombie type 0..N-1, potion, enemy projectile],
    /// normalised distance, then three zombie flags (stunned, winding up, back turned to the hero).
    /// </summary>
    public sealed class RaySensor
    {
        private readonly RaySensorConfig config;

        public RaySensor(RaySensorConfig config)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            config.Validate();
        }

        public int CategoryCount => config.ZombieTypeCount + 4;
        public int EdgeCategory => 1;
        public int PotionCategory => config.ZombieTypeCount + 2;
        public int EnemyProjectileCategory => config.ZombieTypeCount + 3;
        public int SizePerRay => CategoryCount + 4;
        public int TotalSize => config.RayCount * SizePerRay;

        public void Write(ArenaSim sim, float[] buffer, int offset)
        {
            if (sim == null)
            {
                throw new ArgumentNullException(nameof(sim));
            }

            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (offset < 0 || offset + TotalSize > buffer.Length)
            {
                throw new ArgumentException("Buffer is too small for the ray observations.", nameof(buffer));
            }

            int categoryCount = CategoryCount;
            for (int rayIndex = 0; rayIndex < config.RayCount; rayIndex++)
            {
                int start = offset + rayIndex * SizePerRay;
                for (int i = 0; i < SizePerRay; i++)
                {
                    buffer[start + i] = 0f;
                }

                float angle = sim.Hero.Facing + rayIndex * MathF.PI * 2f / config.RayCount;
                Vec2 direction = Vec2.FromAngle(angle);
                float nearest = EdgeDistance(sim.Hero.Position, direction, sim.Config.Center, sim.Config.Radius);
                int category = nearest <= config.MaxDistance ? EdgeCategory : 0;
                ZombieState target = null;

                IReadOnlyList<ZombieState> zombies = sim.Zombies;
                for (int zombieIndex = 0; zombieIndex < zombies.Count; zombieIndex++)
                {
                    ZombieState zombie = zombies[zombieIndex];
                    if (!zombie.Alive || zombie.Def.TypeIndex < 0 ||
                        zombie.Def.TypeIndex >= config.ZombieTypeCount)
                    {
                        continue;
                    }

                    float hitDistance = RayCircleDistance(
                        sim.Hero.Position, direction, zombie.Position, zombie.Def.Radius);
                    if (hitDistance >= 0f && hitDistance < nearest && hitDistance <= config.MaxDistance)
                    {
                        nearest = hitDistance;
                        category = 2 + zombie.Def.TypeIndex;
                        target = zombie;
                    }
                }

                IReadOnlyList<PotionState> potions = sim.Potions;
                for (int potionIndex = 0; potionIndex < potions.Count; potionIndex++)
                {
                    PotionState potion = potions[potionIndex];
                    if (!potion.Active)
                    {
                        continue;
                    }

                    float hitDistance = RayCircleDistance(
                        sim.Hero.Position, direction, potion.Position, ArenaSim.PotionRadius);
                    if (hitDistance >= 0f && hitDistance < nearest && hitDistance <= config.MaxDistance)
                    {
                        nearest = hitDistance;
                        category = PotionCategory;
                        target = null;
                    }
                }

                IReadOnlyList<ProjectileState> projectiles = sim.Projectiles;
                for (int projectileIndex = 0; projectileIndex < projectiles.Count; projectileIndex++)
                {
                    ProjectileState projectile = projectiles[projectileIndex];
                    if (!projectile.Active || projectile.FromHero)
                    {
                        continue;
                    }

                    float hitRadius = MathF.Max(projectile.Radius, 0.4f);
                    float hitDistance = RayCircleDistance(
                        sim.Hero.Position, direction, projectile.Position, hitRadius);
                    if (hitDistance >= 0f && hitDistance < nearest && hitDistance <= config.MaxDistance)
                    {
                        nearest = hitDistance;
                        category = EnemyProjectileCategory;
                        target = null;
                    }
                }

                if (nearest > config.MaxDistance)
                {
                    nearest = config.MaxDistance;
                    category = 0;
                    target = null;
                }

                buffer[start + category] = 1f;
                buffer[start + categoryCount] = category == 0 ? 1f : nearest / config.MaxDistance;
                if (target != null)
                {
                    buffer[start + categoryCount + 1] = target.StunRemaining > 0f ? 1f : 0f;
                    buffer[start + categoryCount + 2] =
                        target.AttackPhase == ZombieAttackPhase.Windup ? 1f : 0f;
                    buffer[start + categoryCount + 3] = IsBackFacingHero(target, sim.Hero.Position) ? 1f : 0f;
                }
            }
        }

        /// <summary>
        /// Distance along <paramref name="direction"/> (unit) from <paramref name="origin"/> to the rim
        /// of the platform; 0 when the origin is already over the abyss.
        /// </summary>
        public static float EdgeDistance(Vec2 origin, Vec2 direction, Vec2 centre, float radius)
        {
            Vec2 offset = origin - centre;
            float b = Vec2.Dot(offset, direction);
            float c = offset.LengthSquared - radius * radius;
            if (c >= 0f)
            {
                return 0f;
            }

            return -b + MathF.Sqrt(b * b - c);
        }

        private static float RayCircleDistance(Vec2 origin, Vec2 direction, Vec2 centre, float radius)
        {
            Vec2 toCentre = centre - origin;
            float projection = Vec2.Dot(toCentre, direction);
            float perpendicularSquared = toCentre.LengthSquared - projection * projection;
            float radiusSquared = radius * radius;
            if (perpendicularSquared > radiusSquared)
            {
                return -1f;
            }

            float halfChord = MathF.Sqrt(MathF.Max(0f, radiusSquared - perpendicularSquared));
            float near = projection - halfChord;
            if (near >= 0f)
            {
                return near;
            }

            float far = projection + halfChord;
            return far >= 0f ? far : -1f;
        }

        private static bool IsBackFacingHero(ZombieState zombie, Vec2 heroPosition)
        {
            Vec2 toHero = heroPosition - zombie.Position;
            if (toHero.LengthSquared <= 1e-12f)
            {
                return false;
            }

            float dot = Vec2.Dot(Vec2.FromAngle(zombie.Facing), toHero.Normalized());
            return dot < -0.5f;
        }
    }
}
