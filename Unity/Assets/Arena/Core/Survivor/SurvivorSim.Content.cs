using System;
using System.Collections.Generic;
using PersonalArena.Core;

namespace PersonalArena.Core.Survivor
{
    public sealed partial class SurvivorSim
    {
        private int orbitAxeCount;
        private float orbitAngle;
        private float orbitRemaining;
        private float orbitRadius;
        private float orbitAxeRadius;
        private bool shockwaveActive;
        private int shockwaveId;
        private float shockwaveRadius;
        private float shockwaveMaxRadius;
        private Vec2 shockwaveCenter;
        private readonly Vec2[] orbitAxePositions = new Vec2[8];

        public int OrbitAxeCount => orbitAxeCount;
        public float OrbitAxeRadius => orbitAxeCount > 0 ? orbitAxeRadius : 0f;
        public float AuraRadius
        {
            get
            {
                int level = inventory.Level(SurvivorCatalog.AuraIndex);
                if (level <= 0) return 0f;
                ItemDef def = SurvivorCatalog.Get(SurvivorCatalog.AuraIndex);
                return def.BaseRange * (1f + def.RangePerLevel * (level - 1)) * stats.AreaMul;
            }
        }
        public float ShockwaveRadius => shockwaveActive ? shockwaveRadius : 0f;
        public Vec2 ShockwaveCenter => shockwaveCenter;
        public float ShockwaveMaxRadius => shockwaveMaxRadius;

        public Vec2 GetOrbitAxePosition(int k)
        {
            if (k < 0 || k >= orbitAxeCount) throw new ArgumentOutOfRangeException(nameof(k));
            return Hero.Position + Vec2.FromAngle(orbitAngle + k * MathF.PI * 2f / orbitAxeCount) * orbitRadius;
        }

        private void ResetContentState()
        {
            orbitAxeCount = 0; orbitAngle = 0f; orbitRemaining = 0f; orbitRadius = 0f; orbitAxeRadius = 0f;
            shockwaveActive = false; shockwaveId = 0; shockwaveRadius = 0f; shockwaveMaxRadius = 0f; shockwaveCenter = Vec2.Zero;
        }

        private bool ThrustSpears(ItemDef def, int level, out Vec2 aim, out int count)
        {
            aim = Vec2.Zero; count = CountAtLevel(def.CountByLevel, level);
            float length = def.BaseRange * stats.AreaMul;
            SurvivorEnemy target = null; float nearest = float.PositiveInfinity;
            for (int i = 0; i < enemyLimit; i++)
            {
                SurvivorEnemy enemy = enemies[i]; if (!enemy.Active) continue;
                Vec2 delta = enemy.Position - Hero.Position; float distanceSquared = delta.LengthSquared;
                float reach = length + enemy.Radius;
                if (distanceSquared <= reach * reach && distanceSquared < nearest) { nearest = distanceSquared; target = enemy; }
            }
            if (target == null) return false;
            aim = (target.Position - Hero.Position).Normalized();
            if (aim.LengthSquared < 1e-8f) aim = Vec2.FromAngle(Hero.Facing);
            float damage = def.BaseDamage + def.DamagePerLevel * (level - 1);
            float halfWidth = def.Width * stats.AreaMul;
            for (int spear = 0; spear < count && !IsEnded; spear++)
            {
                Vec2 direction = Rotate(aim, SurvivorCatalog.ThrustAngleOffset(spear, count));
                for (int i = 0; i < enemyLimit && !IsEnded; i++)
                {
                    SurvivorEnemy enemy = enemies[i]; if (!enemy.Active) continue;
                    if (CircleIntersectsSegment(enemy.Position, enemy.Radius + halfWidth, Hero.Position, direction, length))
                        DamageEnemy(enemy, damage, def.Knockback, direction);
                }
            }
            return true;
        }

        private void UpdateOrbitAxes(ItemDef def, int level)
        {
            if (orbitAxeCount == 0)
            {
                if (weaponCooldowns[def.CatalogIndex] > 0f) return;
                orbitAxeCount = CountAtLevel(def.CountByLevel, level);
                orbitAngle = Hero.Facing; orbitRemaining = def.Duration;
                orbitRadius = def.BaseRange * stats.AreaMul; orbitAxeRadius = def.ProjectileRadius * stats.AreaMul;
                AddEvent(SurvivorEventType.WeaponFired, extra: orbitAxeCount, id: def.CatalogIndex);
                ApplyOrbitHits(def, level);
                return;
            }
            orbitAngle = WrapAngle(orbitAngle + def.AngularSpeedDegrees * MathF.PI / 180f * FixedDeltaTime);
            orbitRemaining -= FixedDeltaTime;
            if (orbitRemaining <= 0f)
            {
                orbitAxeCount = 0; orbitRemaining = 0f;
                weaponCooldowns[def.CatalogIndex] = def.BaseCooldown * stats.CooldownMul;
                return;
            }
            ApplyOrbitHits(def, level);
        }

        private void ApplyOrbitHits(ItemDef def, int level)
        {
            float damage = def.BaseDamage + def.DamagePerLevel * (level - 1);
            int axes = Math.Min(orbitAxeCount, orbitAxePositions.Length);
            for (int axe = 0; axe < axes; axe++) orbitAxePositions[axe] = GetOrbitAxePosition(axe);
            for (int i = 0; i < enemyLimit && !IsEnded; i++)
            {
                SurvivorEnemy enemy = enemies[i];
                if (!enemy.Active || Time + 1e-6f < enemy.OrbitNextHitTime) continue;
                float reach = orbitAxeRadius + enemy.Radius; bool hit = false;
                for (int axe = 0; axe < axes; axe++)
                {
                    if ((orbitAxePositions[axe] - enemy.Position).LengthSquared <= reach * reach) { hit = true; break; }
                }
                if (!hit) continue;
                enemy.OrbitNextHitTime = Time + def.HitInterval;
                DamageEnemy(enemy, damage, def.Knockback, AwayFromHero(enemy));
            }
        }

        private void TickAura(ItemDef def, int level)
        {
            float radius = def.BaseRange * (1f + def.RangePerLevel * (level - 1)) * stats.AreaMul;
            float damage = def.BaseDamage + def.DamagePerLevel * (level - 1);
            for (int i = 0; i < enemyLimit && !IsEnded; i++)
            {
                SurvivorEnemy enemy = enemies[i]; if (!enemy.Active) continue;
                float reach = radius + enemy.Radius;
                if ((enemy.Position - Hero.Position).LengthSquared <= reach * reach)
                    DamageEnemy(enemy, damage, def.Knockback, AwayFromHero(enemy));
            }
        }

        private void UpdateShockwave(ItemDef def, int level)
        {
            if (!shockwaveActive)
            {
                if (weaponCooldowns[def.CatalogIndex] > 0f) return;
                float maxRadius = def.BaseRange * stats.AreaMul;
                if (!HasEnemyInRange(maxRadius)) return;
                shockwaveActive = true; shockwaveCenter = Hero.Position; shockwaveRadius = 0f; shockwaveMaxRadius = maxRadius; shockwaveId++;
                weaponCooldowns[def.CatalogIndex] = (def.BaseCooldown + def.CooldownPerLevel * (level - 1)) * stats.CooldownMul;
                AddEvent(SurvivorEventType.WeaponFired, id: def.CatalogIndex, point: shockwaveCenter);
                return;
            }
            shockwaveRadius = MathF.Min(shockwaveMaxRadius, shockwaveRadius + def.ProjectileSpeed * FixedDeltaTime);
            float damage = def.BaseDamage + def.DamagePerLevel * (level - 1);
            for (int i = 0; i < enemyLimit && !IsEnded; i++)
            {
                SurvivorEnemy enemy = enemies[i]; if (!enemy.Active || enemy.LastShockwaveId == shockwaveId) continue;
                float reach = shockwaveRadius + enemy.Radius;
                if ((enemy.Position - shockwaveCenter).LengthSquared > reach * reach) continue;
                enemy.LastShockwaveId = shockwaveId;
                DamageEnemy(enemy, damage, def.Knockback, AwayFromPoint(enemy.Position, shockwaveCenter));
            }
            if (shockwaveRadius >= shockwaveMaxRadius) shockwaveActive = false;
        }

        private bool HasEnemyInRange(float range)
        {
            for (int i = 0; i < enemyLimit; i++)
            {
                SurvivorEnemy enemy = enemies[i]; if (!enemy.Active) continue;
                float reach = range + enemy.Radius;
                if ((enemy.Position - Hero.Position).LengthSquared <= reach * reach) return true;
            }
            return false;
        }

        private static int CountAtLevel(IReadOnlyList<int> counts, int level) =>
            counts.Count == 0 ? 1 : counts[Math.Max(1, Math.Min(level, counts.Count)) - 1];

        private static Vec2 Rotate(Vec2 value, float angle)
        {
            float c = MathF.Cos(angle), s = MathF.Sin(angle);
            return new Vec2(value.X * c - value.Y * s, value.X * s + value.Y * c);
        }

        private static bool CircleIntersectsSegment(Vec2 center, float radius, Vec2 start, Vec2 direction, float length)
        {
            Vec2 delta = center - start;
            float along = MathF.Max(0f, MathF.Min(length, Vec2.Dot(delta, direction)));
            Vec2 nearest = start + direction * along;
            return (center - nearest).LengthSquared <= radius * radius;
        }

        private static Vec2 AwayFromPoint(Vec2 point, Vec2 origin)
        {
            Vec2 delta = point - origin;
            return delta.LengthSquared > 1e-8f ? delta.Normalized() : new Vec2(1f, 0f);
        }
    }
}
