using System;
using PersonalArena.Core;

namespace PersonalArena.Core.Survivor
{
    /// <summary>
    /// Ring weapons: bodies that circle the hero all the time at a fixed distance. Unlike the orbit weapons (one
    /// volley state for the whole sim) every ring keeps its own angle, by the inventory slot of its weapon, so a
    /// hero can hold several rings at once. Each ring hits an enemy at most once per <see cref="ItemDef.HitInterval"/>.
    /// </summary>
    public sealed partial class SurvivorSim
    {
        private readonly float[] ringAngles = new float[SurvivorCatalog.MaxWeapons];
        private readonly Vec2[] ringBodies = new Vec2[SurvivorCatalog.MaxVolleyCount];

        /// <summary>Bodies of the ring weapon in inventory slot <paramref name="slot"/>; 0 when that slot holds no ring.</summary>
        public int RingBodyCount(int slot)
        {
            ItemDef def = RingAt(slot);
            return def == null || !Hero.Alive ? 0 : Math.Min(VolleyCount(def.CountByLevel, inventory.Level(def.CatalogIndex)), ringBodies.Length);
        }

        /// <summary>Distance of the ring's bodies from the hero (m); 0 when the slot holds no ring.</summary>
        public float RingRadius(int slot)
        {
            ItemDef def = RingAt(slot);
            return def == null ? 0f : def.BaseRange * stats.AreaMul;
        }

        /// <summary>Radius of one body of the ring (m); 0 when the slot holds no ring.</summary>
        public float RingBodyRadius(int slot)
        {
            ItemDef def = RingAt(slot);
            return def == null ? 0f : def.ProjectileRadius * stats.AreaMul;
        }

        public Vec2 GetRingBodyPosition(int slot, int k)
        {
            int count = RingBodyCount(slot);
            if (k < 0 || k >= count) throw new ArgumentOutOfRangeException(nameof(k));
            return Hero.Position + Vec2.FromAngle(ringAngles[slot] + k * MathF.PI * 2f / count) * RingRadius(slot);
        }

        private ItemDef RingAt(int slot)
        {
            if (slot < 0 || slot >= inventory.WeaponCount || slot >= ringAngles.Length) return null;
            ItemDef def = SurvivorCatalog.Get(inventory.WeaponAt(slot));
            return def != null && def.Pattern == WeaponPattern.Ring ? def : null;
        }

        private void UpdateRing(int slot, ItemDef def, int level)
        {
            if (slot >= ringAngles.Length) return;
            ringAngles[slot] = WrapAngle(ringAngles[slot] + def.AngularSpeedDegrees * MathF.PI / 180f * FixedDeltaTime);
            int bodies = Math.Min(VolleyCount(def.CountByLevel, level), ringBodies.Length);
            float radius = def.BaseRange * stats.AreaMul;
            float bodyRadius = def.ProjectileRadius * stats.AreaMul;
            float damage = def.BaseDamage + def.DamagePerLevel * (level - 1);
            for (int k = 0; k < bodies; k++) ringBodies[k] = Hero.Position + Vec2.FromAngle(ringAngles[slot] + k * MathF.PI * 2f / bodies) * radius;
            for (int i = 0; i < enemyLimit && !IsEnded; i++)
            {
                SurvivorEnemy enemy = enemies[i];
                if (!enemy.Active || Time + 1e-6f < enemy.RingNextHitTimes[slot]) continue;
                float reach = bodyRadius + enemy.Radius; bool hit = false;
                for (int k = 0; k < bodies; k++)
                {
                    if ((ringBodies[k] - enemy.Position).LengthSquared <= reach * reach) { hit = true; break; }
                }
                if (!hit) continue;
                enemy.RingNextHitTimes[slot] = Time + def.HitInterval;
                DamageEnemy(enemy, damage, def.Knockback, AwayFromHero(enemy));
            }
        }

        private void ResetRings()
        {
            // Rings start a little apart so two of them do not begin on the same spoke.
            for (int slot = 0; slot < ringAngles.Length; slot++) ringAngles[slot] = slot * 0.9f;
        }
    }
}
