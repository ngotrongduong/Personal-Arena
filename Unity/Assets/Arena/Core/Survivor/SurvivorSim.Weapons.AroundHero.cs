using System;
using PersonalArena.Core;

namespace PersonalArena.Core.Survivor
{
    /// <summary>Weapons centred on the hero: orbiting axes/orbs/knives, the damage aura and the expanding shockwave (one state each).</summary>
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
        /// <summary>Catalog index of the orbit weapon of the current volley (its cooldown waits while it spins).</summary>
        private int orbitWeaponIndex = -1;

        public int OrbitAxeCount => orbitAxeCount;
        public float OrbitAxeRadius => orbitAxeCount > 0 ? orbitAxeRadius : 0f;
        /// <summary>Catalog index of the spinning orbit weapon, or −1.</summary>
        public int OrbitWeaponIndex => orbitAxeCount > 0 ? orbitWeaponIndex : -1;
        /// <summary>Catalog index of the owned aura weapon, or −1.</summary>
        public int AuraWeaponIndex => OwnedWeaponWithPattern(WeaponPattern.Aura);
        /// <summary>Catalog index of the owned shockwave weapon, or −1.</summary>
        public int ShockwaveWeaponIndex => OwnedWeaponWithPattern(WeaponPattern.Shockwave);
        public float AuraRadius
        {
            get
            {
                int index = AuraWeaponIndex;
                if (index < 0) return 0f;
                ItemDef def = SurvivorCatalog.Get(index);
                return def.BaseRange * (1f + def.RangePerLevel * (inventory.Level(index) - 1)) * stats.AreaMul;
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

        // ---- orbit ----

        private void UpdateOrbitAxes(ItemDef def, int level)
        {
            if (orbitAxeCount == 0)
            {
                if (weaponCooldowns[def.CatalogIndex] > 0f) return;
                orbitAxeCount = Math.Min(VolleyCount(def.CountByLevel, level), orbitAxePositions.Length);
                orbitWeaponIndex = def.CatalogIndex;
                orbitAngle = Hero.Facing; orbitRemaining = def.Duration * stats.DurationMul;
                orbitRadius = def.BaseRange * stats.AreaMul; orbitAxeRadius = def.ProjectileRadius * stats.AreaMul;
                AddEvent(SurvivorEventType.WeaponFired, extra: orbitAxeCount, id: def.CatalogIndex);
                ApplyOrbitHits(def, level);
                return;
            }
            orbitWeaponIndex = def.CatalogIndex; // a chest may evolve the weapon mid-spin
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

        // ---- aura ----

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

        // ---- shockwave ----

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
                if (def.StunSeconds > 0f) Stun(enemy, def.StunSeconds);
            }
            if (shockwaveRadius >= shockwaveMaxRadius) shockwaveActive = false;
        }
    }
}
